using System;
using System.Net;
using System.Text;
using System.Threading;

namespace Seedforger.Web {

  /// <summary>
  /// A tiny, dependency-free HTTP dashboard for the headless daemon. Serves one
  /// self-contained dark page plus a JSON status endpoint the page polls, over the
  /// built-in <see cref="HttpListener"/> (works on Windows, Linux and macOS). Binds
  /// to localhost by default; an optional stop callback lets the page halt the run.
  /// </summary>
  internal sealed class WebDashboard : IDisposable {

    private readonly HttpListener listener = new HttpListener();
    private readonly Func<string> statusJson;
    private readonly Action stopAll;
    private readonly Action<string> log;
    private Thread thread;
    private volatile bool running;

    public string Url { get; }

    /// <param name="bind">Host to bind (default 127.0.0.1). Use 0.0.0.0 to expose it
    /// on the LAN — on Windows that may need a urlacl / admin rights.</param>
    /// <param name="port">TCP port (default 8080).</param>
    /// <param name="statusJson">Returns the current status as a JSON string.</param>
    /// <param name="stopAll">Invoked when the page asks the daemon to stop (optional).</param>
    public WebDashboard(string bind, int port, Func<string> statusJson, Action stopAll, Action<string> log) {
      this.statusJson = statusJson;
      this.stopAll = stopAll;
      this.log = log;
      if (string.IsNullOrWhiteSpace(bind)) bind = "127.0.0.1";
      // HttpListener wants a hostname wildcard for non-loopback binds.
      var host = bind == "0.0.0.0" || bind == "*" ? "+" : bind;
      Url = $"http://{(host == "+" ? "127.0.0.1" : bind)}:{port}/";
      listener.Prefixes.Add($"http://{host}:{port}/");
    }

    public void Start() {
      if (running) return;
      listener.Start();
      running = true;
      thread = new Thread(Loop) { IsBackground = true, Name = "web-dashboard" };
      thread.Start();
      log?.Invoke($"dashboard: {Url}");
    }

    public void Stop() {
      if (!running) return;
      running = false;
      try { listener.Stop(); } catch { }
      try { listener.Close(); } catch { }
    }

    public void Dispose() => Stop();

    private void Loop() {
      while (running) {
        HttpListenerContext ctx;
        try { ctx = listener.GetContext(); }
        catch { if (!running) return; continue; }
        try { Handle(ctx); }
        catch (Exception ex) { log?.Invoke("dashboard error: " + ex.Message); }
      }
    }

    private void Handle(HttpListenerContext ctx) {
      var path = ctx.Request.Url?.AbsolutePath ?? "/";
      try {
        switch (path) {
          case "/":
          case "/index.html":
            Write(ctx, 200, "text/html; charset=utf-8", Page);
            break;
          case "/api/status":
            Write(ctx, 200, "application/json; charset=utf-8", statusJson());
            break;
          case "/api/stop":
            if (stopAll != null) { stopAll(); Write(ctx, 200, "application/json", "{\"stopped\":true}"); }
            else Write(ctx, 404, "application/json", "{\"error\":\"stop not available\"}");
            break;
          default:
            Write(ctx, 404, "text/plain", "not found");
            break;
        }
      }
      finally { try { ctx.Response.OutputStream.Close(); } catch { } }
    }

    private static void Write(HttpListenerContext ctx, int status, string contentType, string body) {
      var bytes = Encoding.UTF8.GetBytes(body ?? "");
      ctx.Response.StatusCode = status;
      ctx.Response.ContentType = contentType;
      ctx.Response.ContentLength64 = bytes.Length;
      ctx.Response.Headers["Cache-Control"] = "no-store";
      ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
    }

    // A single self-contained page: dark theme, polls /api/status every 2s.
    private const string Page = @"<!doctype html>
<html lang=""en""><head><meta charset=""utf-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1"">
<title>Seedforger — dashboard</title>
<style>
:root{--bg:#0f1216;--card:#171c22;--line:#232a32;--fg:#e6e9ec;--dim:#93a1b0;--accent:#2ea043;--warn:#e3b341}
*{box-sizing:border-box}body{margin:0;font:14px/1.5 -apple-system,Segoe UI,Roboto,sans-serif;background:var(--bg);color:var(--fg)}
header{padding:18px 22px;border-bottom:1px solid var(--line);display:flex;align-items:baseline;gap:12px;flex-wrap:wrap}
h1{font-size:18px;margin:0;font-weight:600}.tag{color:var(--dim);font-size:12px}
.wrap{padding:22px;max-width:1100px;margin:0 auto}
.cards{display:grid;grid-template-columns:repeat(auto-fit,minmax(150px,1fr));gap:14px;margin-bottom:22px}
.card{background:var(--card);border:1px solid var(--line);border-radius:12px;padding:14px 16px}
.card .k{color:var(--dim);font-size:12px;text-transform:uppercase;letter-spacing:.04em}
.card .v{font-size:24px;font-weight:600;margin-top:4px}
.card .v.accent{color:var(--accent)}
table{width:100%;border-collapse:collapse;background:var(--card);border:1px solid var(--line);border-radius:12px;overflow:hidden}
th,td{padding:10px 14px;text-align:left;border-bottom:1px solid var(--line);white-space:nowrap}
th{color:var(--dim);font-size:12px;text-transform:uppercase;letter-spacing:.04em;font-weight:600}
td.name{white-space:normal;max-width:320px;overflow:hidden;text-overflow:ellipsis}
tr:last-child td{border-bottom:0}
.dot{display:inline-block;width:8px;height:8px;border-radius:50%;margin-right:7px;vertical-align:middle}
.dot.on{background:var(--accent)}.dot.off{background:var(--dim)}
.pill{display:inline-block;font-size:11px;padding:1px 8px;border-radius:999px;border:1px solid var(--line);color:var(--dim)}
.pill.real{color:var(--accent);border-color:var(--accent)}
.foot{color:var(--dim);font-size:12px;margin-top:16px;display:flex;gap:14px;align-items:center;flex-wrap:wrap}
button{background:transparent;border:1px solid var(--line);color:var(--dim);border-radius:8px;padding:6px 12px;cursor:pointer;font:inherit}
button:hover{border-color:var(--warn);color:var(--warn)}
a{color:var(--accent)}
</style></head><body>
<header><h1>Seedforger</h1><span class=""tag"" id=""ver""></span><span class=""tag"" id=""up""></span></header>
<div class=""wrap"">
  <div class=""cards"">
    <div class=""card""><div class=""k"">Total uploaded</div><div class=""v accent"" id=""tup"">—</div></div>
    <div class=""card""><div class=""k"">Total downloaded</div><div class=""v"" id=""tdn"">—</div></div>
    <div class=""card""><div class=""k"">Overall ratio</div><div class=""v"" id=""trat"">—</div></div>
    <div class=""card""><div class=""k"">Active</div><div class=""v"" id=""act"">—</div></div>
  </div>
  <table><thead><tr>
    <th>Torrent</th><th>Client</th><th>Uploaded</th><th>Down</th><th>Ratio</th>
    <th>Seeders</th><th>Leechers</th><th>Interval</th><th>Trackers</th>
  </tr></thead><tbody id=""rows""><tr><td colspan=""9"" class=""tag"">loading…</td></tr></tbody></table>
  <div class=""foot"">
    <span id=""status"">connecting…</span>
    <button onclick=""stopAll()"">Stop the daemon</button>
    <span class=""tag"">Local dashboard — figures are what the daemon reports to trackers.</span>
  </div>
</div>
<script>
function fmt(b){if(b==null)return'—';var u=['B','KB','MB','GB','TB'],i=0,v=b;while(v>=1024&&i<u.length-1){v/=1024;i++}return (i?v.toFixed(2):v)+' '+u[i]}
function dur(s){s=s|0;var d=(s/86400)|0,h=((s%86400)/3600)|0,m=((s%3600)/60)|0;return (d?d+'d ':'')+(h?h+'h ':'')+m+'m'}
function esc(s){return (s||'').replace(/[&<>]/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;'}[c]))}
async function tick(){
  try{
    const r=await fetch('/api/status',{cache:'no-store'});const d=await r.json();
    document.getElementById('ver').textContent='v'+d.version;
    document.getElementById('up').textContent='up '+dur(d.uptimeSeconds);
    document.getElementById('tup').textContent=fmt(d.totals.uploaded);
    document.getElementById('tdn').textContent=fmt(d.totals.downloaded);
    document.getElementById('trat').textContent=d.totals.downloaded>0?d.totals.ratio.toFixed(3):'—';
    document.getElementById('act').textContent=d.totals.running+' / '+d.totals.count;
    const rows=d.torrents.map(t=>`<tr>
      <td class=name><span class='dot ${t.running?'on':'off'}'></span>${esc(t.name)} ${t.realSeed?'<span class=\'pill real\'>real seed</span>':''}</td>
      <td>${esc(t.client)}</td><td>${fmt(t.uploaded)}</td><td>${fmt(t.downloaded)}</td>
      <td>${t.downloaded>0?t.ratio.toFixed(3):'—'}</td>
      <td>${t.seeders<0?'—':t.seeders}</td><td>${t.leechers<0?'—':t.leechers}</td>
      <td>${t.interval?dur(t.interval*0+t.interval/60|0)+'m':'—'}</td>
      <td>${t.trackers}</td></tr>`).join('');
    document.getElementById('rows').innerHTML=rows||`<tr><td colspan=9 class=tag>no torrents</td></tr>`;
    document.getElementById('status').textContent='updated '+new Date().toLocaleTimeString();
  }catch(e){document.getElementById('status').textContent='daemon offline';}
}
async function stopAll(){if(!confirm('Stop the daemon and all seeding?'))return;try{await fetch('/api/stop',{method:'POST'});}catch(e){}document.getElementById('status').textContent='stopping…';}
tick();setInterval(tick,2000);
</script>
</body></html>";
  }
}
