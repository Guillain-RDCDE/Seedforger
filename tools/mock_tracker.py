#!/usr/bin/env python3
"""A tiny local BitTorrent tracker + .torrent generator, for smoke-testing.

Not a real tracker — it just answers every /announce with a fixed bencoded swarm
and (optionally) logs the events it was sent. Used by the CI daemon smoke test and
handy for local end-to-end runs of Seedforger against something that actually
replies. Standard library only.

Usage:
  python3 mock_tracker.py serve --port 8000 [--complete 7 --incomplete 4 --interval 5]
  python3 mock_tracker.py make-torrent out.torrent http://127.0.0.1:8000/announce
"""
import sys
import hashlib
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer


def bencode(v):
    if isinstance(v, int):
        return b"i" + str(v).encode() + b"e"
    if isinstance(v, bytes):
        return str(len(v)).encode() + b":" + v
    if isinstance(v, str):
        return bencode(v.encode())
    if isinstance(v, list):
        return b"l" + b"".join(bencode(x) for x in v) + b"e"
    if isinstance(v, dict):
        items = sorted(v.items())
        return b"d" + b"".join(bencode(k) + bencode(val) for k, val in items) + b"e"
    raise TypeError(type(v))


def make_torrent(path, announce):
    content = bytes((i * 31 + 7) & 0xFF for i in range(100))
    piece = hashlib.sha1(content).digest()
    meta = {
        "announce": announce,
        "info": {
            "length": len(content),
            "name": "sample.bin",
            "piece length": 16384,
            "pieces": piece,
        },
    }
    with open(path, "wb") as f:
        f.write(bencode(meta))
    print(f"wrote {path} -> {announce}")


def serve(port, complete, incomplete, interval):
    body = bencode({
        "complete": complete,
        "incomplete": incomplete,
        "interval": interval,
        "min interval": max(1, interval // 2),
    })

    class Handler(BaseHTTPRequestHandler):
        def do_GET(self):
            ev = ""
            if "event=" in self.path:
                ev = self.path.split("event=", 1)[1].split("&", 1)[0]
            sys.stderr.write(f"announce event={ev or '(periodic)'}\n")
            sys.stderr.flush()
            self.send_response(200)
            self.send_header("Content-Type", "text/plain")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)

        def log_message(self, *a):
            pass

    srv = ThreadingHTTPServer(("127.0.0.1", port), Handler)
    print(f"mock tracker on http://127.0.0.1:{port}/announce", flush=True)
    srv.serve_forever()


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return 2
    cmd = sys.argv[1]
    if cmd == "make-torrent":
        make_torrent(sys.argv[2], sys.argv[3])
        return 0
    if cmd == "serve":
        args = sys.argv[2:]
        def opt(name, default):
            return args[args.index(name) + 1] if name in args else default
        serve(int(opt("--port", "8000")), int(opt("--complete", "7")),
              int(opt("--incomplete", "4")), int(opt("--interval", "5")))
        return 0
    print(__doc__)
    return 2


if __name__ == "__main__":
    raise SystemExit(main())
