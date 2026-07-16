using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Seedforger.BitTorrent;
using Seedforger.Wire;

namespace Seedforger {

  /// <summary>
  /// Opens the announced port and answers incoming BitTorrent handshakes, so the
  /// tracker (and its monitoring peers) see a real, reachable peer behind the
  /// announce. With a verified real file it serves genuine hash-valid pieces
  /// (<see cref="PeerSession"/>); otherwise it looks like a complete-but-choked
  /// seeder (full bitfield + choke). WinForms-free — used by the headless engine.
  /// </summary>
  internal sealed class PeerListener {

    private static readonly Encoding Latin1 = Encoding.GetEncoding(28591);

    private readonly Torrent torrent;
    private readonly byte[] wirePeerId;
    private readonly IPieceSource source;   // null => stingy seeder
    private readonly Governor governor;
    private readonly Func<int> uploadCapBytesPerSec;
    private readonly int port;
    private readonly Action<string> log;

    private TcpListener listener;
    private volatile bool running;

    internal PeerListener(Torrent torrent, byte[] wirePeerId, IPieceSource source, Governor governor,
                          Func<int> uploadCapBytesPerSec, int port, Action<string> log) {
      this.torrent = torrent;
      this.wirePeerId = wirePeerId;
      this.source = source;
      this.governor = governor;
      this.uploadCapBytesPerSec = uploadCapBytesPerSec;
      this.port = port;
      this.log = log;
    }

    internal void Start() {
      try {
        listener = new TcpListener(IPAddress.Any, port);
        listener.Start();
        running = true;
        log?.Invoke("Started TCP listener on port " + port);
        var t = new Thread(AcceptLoop) { IsBackground = true, Name = "PeerListener" };
        t.Start();
      }
      catch (Exception ex) {
        log?.Invoke("TCP listener could not start on port " + port + " (" + ex.Message + ") — announcing only.");
        running = false;
      }
    }

    internal void Stop() {
      running = false;
      try { listener?.Stop(); } catch { }
      listener = null;
    }

    private void AcceptLoop() {
      while (running) {
        Socket socket;
        try { socket = listener.AcceptSocket(); }
        catch { break; }

        if (source != null) {
          // Real-seed: a full peer session serves genuine hash-valid blocks.
          var s = socket;
          var th = new Thread(() => {
            try {
              using var ns = new NetworkStream(s, true);
              new PeerSession(torrent.InfoHash, wirePeerId, source, governor,
                              Math.Max(0, uploadCapBytesPerSec())).Serve(ns);
            }
            catch { }
          }) { IsBackground = true, Name = "PeerSession" };
          th.Start();
          continue;
        }

        // Stingy seeder: handshake, full bitfield, then choke.
        try { StingyRespond(socket); }
        catch { try { socket.Close(); } catch { } }
      }
    }

    private void StingyRespond(Socket socket) {
      using var stream = new NetworkStream(socket, true);
      stream.ReadTimeout = 1000;
      var buffer = new byte[68];
      try { stream.Read(buffer, 0, buffer.Length); } catch { }

      var text = Latin1.GetString(buffer, 0, buffer.Length);
      if (text.IndexOf("BitTorrent protocol", StringComparison.Ordinal) >= 0 &&
          text.IndexOf(Latin1.GetString(torrent.InfoHash), StringComparison.Ordinal) >= 0) {
        var handshake = Handshake();
        stream.Write(handshake, 0, handshake.Length);
        var pieces = torrent.PieceCount;
        if (pieces > 0) {
          var bitfield = PeerWire.FullBitfieldMessage(pieces);
          stream.Write(bitfield, 0, bitfield.Length);
          var choke = PeerWire.ChokeMessage();
          stream.Write(choke, 0, choke.Length);
          log?.Invoke("Answered a peer as a full seeder (bitfield + choke)");
        }
      }
    }

    private byte[] Handshake() {
      const string proto = "BitTorrent protocol";
      var buf = new byte[68]; // 1 + 19 + 8 reserved + 20 infohash + 20 peer_id
      var i = 0;
      buf[i++] = (byte) proto.Length;
      Latin1.GetBytes(proto, 0, proto.Length, buf, i); i += proto.Length;
      i += 8; // reserved
      Buffer.BlockCopy(torrent.InfoHash, 0, buf, i, torrent.InfoHash.Length); i += torrent.InfoHash.Length;
      Buffer.BlockCopy(wirePeerId, 0, buf, i, Math.Min(20, wirePeerId.Length));
      return buf;
    }
  }
}
