using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Seedforger.Wire {

  /// <summary>Minimal bencode *encoder* for building extension payloads.</summary>
  internal static class Bencode {
    internal static byte[] Encode(object v) {
      using var ms = new MemoryStream();
      Write(ms, v);
      return ms.ToArray();
    }

    private static void Write(Stream s, object v) {
      switch (v) {
        case int i: Write(s, (long) i); break;
        case long l:
          WriteAscii(s, "i" + l + "e");
          break;
        case string str: {
          var b = Encoding.UTF8.GetBytes(str);
          WriteBytes(s, b);
          break;
        }
        case byte[] bytes:
          WriteBytes(s, bytes);
          break;
        case IDictionary<string, object> d: {
          s.WriteByte((byte) 'd');
          var keys = new List<string>(d.Keys);
          keys.Sort(System.StringComparer.Ordinal); // bencode dicts are sorted
          foreach (var k in keys) {
            WriteBytes(s, Encoding.UTF8.GetBytes(k));
            Write(s, d[k]);
          }
          s.WriteByte((byte) 'e');
          break;
        }
        case IEnumerable<object> list:
          s.WriteByte((byte) 'l');
          foreach (var item in list) Write(s, item);
          s.WriteByte((byte) 'e');
          break;
      }
    }

    private static void WriteBytes(Stream s, byte[] b) {
      WriteAscii(s, b.Length + ":");
      s.Write(b, 0, b.Length);
    }

    private static void WriteAscii(Stream s, string text) {
      foreach (var c in text) s.WriteByte((byte) c);
    }
  }

  /// <summary>
  /// BitTorrent extension protocol (BEP 10) message builders: the extended
  /// handshake advertising ut_pex/ut_metadata, a PEX message, and a ut_metadata
  /// piece — so we present like a modern client to a peer that probes extensions.
  /// </summary>
  internal static class ExtensionMessages {

    internal const byte ExtendedId = 20;
    internal const int UtPexId = 1;
    internal const int UtMetadataId = 2;

    /// <summary>8 reserved handshake bytes with the extension-protocol bit set.</summary>
    internal static byte[] ReservedWithExtensions() {
      var r = new byte[8];
      r[5] = 0x10; // BEP 10 extension bit
      return r;
    }

    /// <summary>Extended handshake (extended message, sub-id 0) advertising our
    /// supported extensions.</summary>
    internal static byte[] ExtendedHandshake(string clientName = "Seedforger", int metadataSize = 0) {
      var m = new Dictionary<string, object> {
        ["ut_pex"] = (long) UtPexId,
        ["ut_metadata"] = (long) UtMetadataId,
      };
      var dict = new Dictionary<string, object> {
        ["m"] = m,
        ["v"] = clientName,
        ["reqq"] = (long) 250,
      };
      if (metadataSize > 0) dict["metadata_size"] = (long) metadataSize;
      return Extended(0, Bencode.Encode(dict));
    }

    /// <summary>A ut_pex message carrying compact added peers (6 bytes each).</summary>
    internal static byte[] Pex(byte[] addedCompactPeers, int subId = UtPexId) {
      var flags = new byte[addedCompactPeers.Length / 6]; // one flag byte per peer
      var dict = new Dictionary<string, object> {
        ["added"] = addedCompactPeers,
        ["added.f"] = flags,
      };
      return Extended((byte) subId, Bencode.Encode(dict));
    }

    /// <summary>A ut_metadata "data" message: a header dict + the raw metadata block.</summary>
    internal static byte[] MetadataPiece(int piece, int totalSize, byte[] block, int subId = UtMetadataId) {
      var header = new Dictionary<string, object> {
        ["msg_type"] = (long) 1, // data
        ["piece"] = (long) piece,
        ["total_size"] = (long) totalSize,
      };
      var h = Bencode.Encode(header);
      var payload = new byte[h.Length + block.Length];
      System.Buffer.BlockCopy(h, 0, payload, 0, h.Length);
      System.Buffer.BlockCopy(block, 0, payload, h.Length, block.Length);
      return Extended((byte) subId, payload);
    }

    // <len><id=20><ext-sub-id><payload>
    private static byte[] Extended(byte subId, byte[] payload) {
      var body = new byte[1 + payload.Length];
      body[0] = subId;
      System.Buffer.BlockCopy(payload, 0, body, 1, payload.Length);
      return PeerProtocol.Message(MsgId.Extended, body);
    }
  }
}
