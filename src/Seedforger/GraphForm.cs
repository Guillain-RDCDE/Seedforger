using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Seedforger {

  /// <summary>
  /// A small live dashboard that plots the current tab's uploaded bytes over
  /// time (and shows the ratio). Owner-drawn, self-contained, dark by design.
  /// </summary>
  internal sealed class GraphForm : Form {

    private readonly Func<RM> provider;
    private readonly Timer timer;

    private static readonly Color Bg = Color.FromArgb(0x14, 0x16, 0x1A);
    private static readonly Color Grid = Color.FromArgb(0x28, 0x2C, 0x33);
    private static readonly Color Green = Color.FromArgb(0x2E, 0xC9, 0x86);
    private static readonly Color TextCol = Color.FromArgb(0xD7, 0xDB, 0xE2);

    internal GraphForm(Func<RM> provider) {
      this.provider = provider;
      Text = "Seedforger — live graph";
      ClientSize = new Size(580, 320);
      MinimumSize = new Size(380, 240);
      BackColor = Bg;
      DoubleBuffered = true;
      StartPosition = FormStartPosition.CenterParent;
      try { Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath); }
      catch { /* ignore */ }

      timer = new Timer { Interval = 500 };
      timer.Tick += (s, e) => Invalidate();
      timer.Start();
    }

    protected override void OnFormClosed(FormClosedEventArgs e) {
      timer.Stop();
      timer.Dispose();
      base.OnFormClosed(e);
    }

    protected override void OnResize(EventArgs e) {
      base.OnResize(e);
      Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e) {
      base.OnPaint(e);
      var g = e.Graphics;
      g.SmoothingMode = SmoothingMode.AntiAlias;
      g.Clear(Bg);

      int w = ClientSize.Width, h = ClientSize.Height;
      const int padL = 56, padR = 16, padT = 44, padB = 26;
      var plot = Rectangle.FromLTRB(padL, padT, w - padR, h - padB);
      if (plot.Width < 20 || plot.Height < 20) return;

      var rm = provider?.Invoke();
      var hist = rm != null ? rm.UploadHistory.ToArray() : Array.Empty<long>();

      using (var gp = new Pen(Grid, 1))
        for (var i = 0; i <= 4; i++) {
          var y = plot.Top + plot.Height * i / 4;
          g.DrawLine(gp, plot.Left, y, plot.Right, y);
        }

      using (var titleFont = new Font("Segoe UI", 11f, FontStyle.Bold))
      using (var infoFont = new Font("Segoe UI", 9f))
      using (var tb = new SolidBrush(TextCol)) {
        g.DrawString("Uploaded over time", titleFont, tb, padL, 10);
        var ratio = rm?.RatioText ?? "-";
        var upMb = hist.Length > 0 ? hist[hist.Length - 1] / 1048576.0 : 0.0;
        var info = $"Ratio: {ratio}    Uploaded: {upMb:0.0} MB";
        var sz = g.MeasureString(info, infoFont);
        g.DrawString(info, infoFont, tb, w - padR - sz.Width, 16);
      }

      if (hist.Length < 2) {
        using (var f = new Font("Segoe UI", 9f))
        using (var b = new SolidBrush(Grid))
          g.DrawString("Start a torrent to see the graph…", f, b, plot.Left + 8, plot.Top + plot.Height / 2 - 8);
        return;
      }

      long max = 1;
      foreach (var v in hist) if (v > max) max = v;

      using (var f = new Font("Segoe UI", 8f))
      using (var b = new SolidBrush(TextCol))
        for (var i = 0; i <= 4; i++) {
          var val = max * (4 - i) / 4.0 / 1048576.0;
          var y = plot.Top + plot.Height * i / 4;
          g.DrawString($"{val:0.#}", f, b, 6, y - 7);
        }

      var pts = new PointF[hist.Length];
      for (var i = 0; i < hist.Length; i++) {
        var x = plot.Left + (float) plot.Width * i / (hist.Length - 1);
        var y = plot.Bottom - (float) (hist[i] / (double) max) * plot.Height;
        pts[i] = new PointF(x, y);
      }

      var area = new PointF[pts.Length + 2];
      Array.Copy(pts, area, pts.Length);
      area[pts.Length] = new PointF(plot.Right, plot.Bottom);
      area[pts.Length + 1] = new PointF(plot.Left, plot.Bottom);
      using (var ab = new SolidBrush(Color.FromArgb(60, Green))) g.FillPolygon(ab, area);
      using (var lp = new Pen(Green, 2f)) g.DrawLines(lp, pts);
    }
  }
}
