# How BitTorrent actually works — a deep technical guide

> A from-the-wire explanation of the BitTorrent protocol: the `.torrent` file,
> bencoding, the infohash, peer IDs, the tracker protocol (HTTP/UDP), the peer
> wire protocol, DHT/PEX/magnet, encryption, how ratio is measured — and exactly
> where a tool like **Seedforger** plugs in. Written for people who want to
> understand the bytes, not just the buzzwords.

---

## 1. The big picture

BitTorrent turns one-to-many distribution into many-to-many. Instead of everyone
downloading a file from a single server, each downloader also **uploads** the
pieces it already has to other downloaders. The set of everyone sharing one
torrent is the **swarm**.

Roles:

- **Seeder** — a peer that has 100% of the data and only uploads.
- **Leecher** — a peer still downloading (it has 0–99% and both up/downloads).
- **Tracker** — a lightweight server that does *not* hold the data; it only keeps
  a list of which peers are in the swarm and hands newcomers a slice of that list.
- **Peer** — any client in the swarm (seeder or leecher).

Two things coordinate a swarm:

1. **Peer discovery** — *how do I find other peers?* → trackers, DHT, PEX, LSD.
2. **The peer wire protocol** — *how do two peers actually exchange pieces?*

The data itself is split into fixed-size **pieces** (commonly 256 KiB–4 MiB),
and each piece is verified against a SHA-1 hash baked into the `.torrent`. This
is why corruption can't spread: a bad piece fails its hash and is thrown away.

---

## 2. Bencoding — BitTorrent's data format

Everything structural in BitTorrent (the `.torrent` file, tracker responses,
DHT messages) is encoded in **bencode**. It has exactly four types:

| Type | Grammar | Example | Decodes to |
|------|---------|---------|------------|
| Integer | `i<digits>e` | `i42e` | `42` |
| Byte string | `<len>:<bytes>` | `4:spam` | `"spam"` |
| List | `l<items>e` | `l4:spami42ee` | `["spam", 42]` |
| Dictionary | `d<pairs>e` | `d3:bar4:spam3:fooi42ee` | `{"bar":"spam","foo":42}` |

Rules that matter:

- Strings are **length-prefixed byte strings**, not text — they can hold raw
  binary (piece hashes, the infohash, compact peer lists).
- Dictionary **keys are byte strings and must be sorted** lexicographically. This
  ordering is not cosmetic — it makes the encoding *canonical*, which is the whole
  reason the infohash is stable (see §4).
- No floats, no negative-zero, no leading zeros (`i03e` is invalid).

Seedforger ships a small bencode parser/encoder in
[`Seedforger/BitTorrent/BEncode.cs`](../Seedforger/BitTorrent/BEncode.cs); it's
what reads a `.torrent` and decodes tracker replies.

---

## 3. The `.torrent` file

A `.torrent` is a bencoded dictionary. A minimal single-file torrent:

```
d
  8:announce      41:http://tracker.example.org:6969/announce
  10:created by   12:Seedforger/1
  4:info d
      6:length      i1024000e
      4:name        11:ubuntu.iso
      12:piece length i262144e
      6:pieces      <20 * N raw bytes: SHA-1 of each piece>
  e
e
```

Key fields:

- **`announce`** — the primary tracker URL. Optionally **`announce-list`**
  (BEP 12) holds tiers of backup trackers.
- **`info`** — the dictionary that describes the content:
  - `name` — suggested file/folder name.
  - `piece length` — bytes per piece.
  - `pieces` — the concatenation of every piece's 20-byte SHA-1 hash. A 4 GB
    file at 1 MiB pieces has ~4000 pieces → ~80 KB of hashes.
  - `length` (single-file) **or** `files` (multi-file: a list of
    `{length, path[]}` dictionaries).
  - `private` — if `1`, DHT/PEX are disallowed; peers may only come from the
    tracker. This is the flag **private trackers** rely on.

---

## 4. The infohash — a torrent's identity

The **infohash** is the SHA-1 of the **bencoded `info` dictionary** (the value,
re-encoded canonically):

```
infohash = SHA1( bencode(info) )      // 20 bytes
```

It is *the* identifier for a torrent. Because bencode dictionaries are
canonically sorted, everyone computes the same 20 bytes from the same content,
so peers and trackers can agree on "which torrent" without a central registry.

- On the wire and in announces it appears as **20 raw bytes** (URL-encoded).
- Humans usually see it as **40 hex characters** (`4a...`), or **32 base32
  characters** in magnet links.

BitTorrent v2 (BEP 52) switches to SHA-256 and a Merkle-tree `pieces layers`
structure; hybrid torrents carry both a v1 and v2 infohash. Most of the ecosystem
today is still v1/SHA-1.

Seedforger computes the v1 infohash in
[`Seedforger/BitTorrent/Torrent.cs`](../Seedforger/BitTorrent/Torrent.cs) (`InfoHash`),
and for magnet links takes the hash **directly** since there is no `info` dict to
hash (see §10 and `Torrent.SetVirtual`).

---

## 5. Peer IDs — who the client says it is

Each client generates a **20-byte peer ID** per session. There are two common
conventions:

- **Azureus style** (dominant today): `-<2-letter code><4 version chars>-` +
  12 random bytes. Examples:

  | Client | Prefix | Meaning |
  |--------|--------|---------|
  | qBittorrent 5.2.3 | `-qB5230-` | qB, version 5.2.3 |
  | Transmission 4.1.3 | `-TR4130-` | TR, 4.1.3 |
  | Deluge 2.1.1 | `-DE2110-` | DE, 2.1.1 |
  | libtorrent 2.1.0 | `-LT2100-` | LT, 2.1.0 |
  | µTorrent 3.5.5 | `-UT355S-` | UT, 3.5.5 |

  The version chars use `0-9`, then `A-Z`, then `a-z` for values ≥ 10 — this is
  libtorrent's `generate_fingerprint` scheme, which qBittorrent/Deluge/libtorrent
  all share. Transmission uses its own `-TRxyzb-` (major/minor/maintenance/beta).

- **Shadow style** (older): a letter + version chars using `-`-padded base64-ish
  encoding, e.g. `T03I-...` (BitTornado).

The peer ID travels in **two places**: the tracker announce (`peer_id=`) and the
peer wire **handshake**. Private trackers keep a **whitelist of allowed client
prefixes** and reject or ban anything unknown or inconsistent — which is exactly
why emulating a *current, real* client matters. Seedforger's client database
([`DefaultClientProfiles.cs`](../Seedforger/DefaultClientProfiles.cs)) stores the
exact prefix + matching `User-Agent` for each client.

---

## 6. The tracker protocol (HTTP/HTTPS)

This is the part Seedforger actually speaks. To join or update its state in a
swarm, a client sends an HTTP **GET** to the tracker's `announce` URL with a
query string. All binary values are **percent-encoded** byte-by-byte.

### 6.1 The announce request

```
GET /announce?info_hash=%4a%bc...&peer_id=-qB5230-%8c%c0...&port=6881
    &uploaded=0&downloaded=0&left=1024000&corrupt=0&key=1A2B3C4D
    &event=started&numwant=200&compact=1&no_peer_id=1&supportcrypto=1 HTTP/1.1
Host: tracker.example.org
User-Agent: qBittorrent/5.2.3
Accept-Encoding: gzip
Connection: close
```

| Parameter | Meaning |
|-----------|---------|
| `info_hash` | 20 raw bytes of the infohash, URL-encoded |
| `peer_id` | 20-byte peer ID, URL-encoded |
| `port` | TCP port the client listens on |
| `uploaded` / `downloaded` | **cumulative bytes this session** — the numbers the tracker sums for your ratio |
| `left` | bytes still needed (`0` ⇒ you're a seeder) |
| `event` | `started` (first announce), `stopped` (leaving), `completed` (just finished), or omitted (periodic) |
| `numwant` | how many peers you want back (often 50–200; `0` on stop) |
| `compact` | `1` ⇒ send peers in compact binary form (§6.3) |
| `key` | a client-chosen id so the tracker can recognise you across IP changes |
| `no_peer_id`, `supportcrypto`, `corrupt`, `redundant` | assorted flags real clients send |

> **This is the crux of ratio faking.** The tracker has no way to independently
> measure how much you uploaded — it *believes the `uploaded=` number you send*.
> A client (or Seedforger) simply reports whatever it wants. Everything else is
> about making that report **look like it came from a real client** (right
> fingerprint, plausible speeds, human-like timing).

### 6.2 The announce response

A bencoded dictionary:

```
d
  8:interval    i1800e          # re-announce after this many seconds (min)
  8:complete    i57e            # seeders (from scrape data)
  10:incomplete i12e            # leechers
  5:peers       <compact peer bytes, or a list of dicts>
e
```

On error the tracker returns `d14:failure reason<len>:<text>e` and nothing else —
e.g. *"unregistered torrent"*, *"your client is not allowed"*, *"port not open"*.
Seedforger surfaces this verbatim, and its **Test announce (dry-run)** feature
sends exactly one `started` announce so you can see this reply before committing.

### 6.3 Compact peers (BEP 23)

With `compact=1`, IPv4 peers come back as a byte string where **every 6 bytes** =
4-byte IP + 2-byte big-endian port:

```
peers = <ip0 ip1 ip2 ip3 port_hi port_lo><ip0 ip1 ip2 ip3 port_hi port_lo>...
```

The non-compact form is a list of `d2:ip..7:peer id..4:port i..e` dictionaries.
IPv6 uses `peers6` with 18-byte entries (BEP 7).

### 6.4 Scrape

A parallel endpoint (`/announce` → `/scrape`) returns swarm counts
(`complete`/`downloaded`/`incomplete`) **without** joining the swarm. Trackers can
cross-check scrape vs announce, which is one more reason your announced numbers
should be internally consistent.

---

## 7. UDP trackers (BEP 15)

To cut overhead, many public trackers speak a binary UDP protocol: a
connect handshake (client gets a `connection_id`), then an announce carrying the
same logical fields (infohash, peer_id, downloaded/left/uploaded, event, key,
num_want) as fixed-width binary, and a compact peer list back. Same *semantics*
as HTTP, different framing. (Seedforger currently targets HTTP/HTTPS trackers.)

---

## 8. The peer wire protocol

Once a client has peer addresses, it connects **TCP** (or µTP, a UDP-based
transport) to each and runs the peer protocol. Seedforger does **not** do this —
but understanding it is what makes clear why faking works at the tracker layer.

### 8.1 Handshake

```
<1 byte  = 19>
<19 bytes = "BitTorrent protocol">
<8 bytes  = reserved flags (DHT, extensions, fast, …)>
<20 bytes = infohash>
<20 bytes = peer_id>
```

If the infohash doesn't match what the receiver is serving, the connection is
dropped. The reserved bits advertise extensions (e.g. bit for BEP 10 extension
protocol, used by PEX and magnet metadata exchange).

### 8.2 Messages

After the handshake, length-prefixed messages flow:

```
<4-byte length><1-byte id><payload>
```

| id | message | purpose |
|----|---------|---------|
| — | keep-alive | zero-length, keeps the socket alive |
| 0 | choke | "I won't send you data right now" |
| 1 | unchoke | "you may request from me" |
| 2 | interested | "you have pieces I want" |
| 3 | not interested | |
| 4 | have | "I just completed piece N" |
| 5 | bitfield | which pieces I already have (sent once, right after handshake) |
| 6 | request | give me block (piece index, offset, length) |
| 7 | piece | here's a block of data |
| 8 | cancel | never mind that request |

Pieces are downloaded in **blocks** of typically 16 KiB. Strategy layers on top:

- **Rarest-first** piece selection keeps the swarm healthy.
- **Choking / tit-for-tat**: a peer unchokes the few peers that give it the best
  download rate, plus one **optimistic unchoke** to discover new good peers. This
  is the "reciprocity" that rewards uploaders — and the real-world mechanism that
  ratio economics on private trackers try to emulate socially.

---

## 9. Peer discovery beyond the tracker

- **DHT** (BEP 5) — a Kademlia distributed hash table keyed by infohash; lets
  clients find peers with **no tracker at all**. Disabled for `private` torrents.
- **PEX** (Peer Exchange, BEP 11) — peers gossip lists of other peers to each
  other over the extension protocol. Also disabled for private torrents.
- **LSD** (Local Service Discovery) — multicast on the LAN to find local peers.

Private trackers set `private=1` specifically to switch all of these **off**, so
that the tracker is the single source of truth for who is in the swarm and how
much they transferred — which is what makes ratio accounting enforceable.

---

## 10. Magnet links (BEP 9)

A magnet link carries the identity but **not** the metadata:

```
magnet:?xt=urn:btih:<infohash>&dn=<display name>&tr=<tracker>&tr=<tracker>...
```

- `xt=urn:btih:` — the infohash, as **40 hex** or **32 base32** characters.
- `dn` — display name (cosmetic).
- `tr` — tracker URL(s).

With only the infohash, a client joins the swarm (via trackers/DHT) and then
**downloads the `info` dictionary from peers** using the ut_metadata extension
(BEP 9), verifying it against the infohash it already knows. Because a magnet has
no piece data and no size, a stats simulator must be told the size out-of-band —
which is exactly what Seedforger's **Open magnet…** does (parse in
[`Seedforger/Magnet.cs`](../Seedforger/Magnet.cs), then ask for the size).

---

## 11. Encryption (MSE/PE)

Message Stream Encryption / Protocol Encryption obfuscates the peer connection
(RC4-based handshake) to dodge ISP throttling of BitTorrent. It's *obfuscation*,
not authentication — trackers don't rely on it. The `supportcrypto=1` announce
flag simply advertises that the client can do it; Seedforger sends it because
real clients do.

---

## 12. How "ratio" is actually computed

Your **ratio = total uploaded ÷ total downloaded**, as accumulated by the tracker
from your announces. Public trackers usually don't care. **Private trackers** do:

- They keep a per-user, per-torrent tally from every announce's `uploaded=` /
  `downloaded=` deltas.
- They enforce minimum ratios, seeding time, or a credit ("bonus") economy.
- They run **anti-cheat** because the numbers are self-reported and therefore
  forgeable.

### What anti-cheat looks for

| Signal | What raises a flag |
|--------|--------------------|
| **Client whitelist** | `peer_id` prefix / `User-Agent` not in the allowed list, or the two disagreeing |
| **Impossible speed** | uploading faster than your line could physically manage, or more than the torrent's total size |
| **Announce cadence** | announcing like clockwork, or more often than `interval` allows |
| **Consistency** | announce numbers that don't line up with scrape, or `left`/`downloaded` that don't add up |
| **Network reality** | a port that's never actually connectable, IP/geo mismatches, no real peer connections |

---

## 13. Where Seedforger sits

Seedforger implements **only §6 — the tracker announce/scrape** — and nothing
else. It never connects to peers (§8), transfers no data, and hashes no pieces.
Concretely, for each announce it:

1. Builds a **real client fingerprint** — the exact `peer_id` prefix + random
   tail and matching `User-Agent` for a chosen, current client
   (`TorrentClientFactory` + `clients.json`).
2. Sends the announce with **fabricated `uploaded` / `downloaded`** values that
   grow over time.
3. Shapes those values to be believable — a **ramp-up** and mean-reverting
   variation instead of a flat line (`SpeedShaper`), a **day/night rhythm** and
   **active-hours** window (`Stealth`), **interval jitter** so announces aren't
   clockwork, optional **client rotation**, and a **believability guard** that
   warns on physically implausible speeds.
4. Ties the numbers to **swarm reality** — reads the tracker's leecher/seeder
   counts and scales upload/download so you only "feed" demand that exists
   (`SwarmModel`), and caps your **total** upload across tabs to one uplink
   (`Bandwidth`).
5. Optionally acts as a **connectable, complete seeder** if a peer connects to
   the advertised port: it answers the handshake, sends a full bitfield and
   chokes (`PeerWire`) — visible and legitimate, transferring nothing.

Mapping the anti-cheat table (§12) to Seedforger's features:

| Anti-cheat signal | Seedforger countermeasure |
|-------------------|---------------------------|
| Client whitelist | accurate, current fingerprints (`-qB5230-`, `-TR4130-`, …), **client rotation**, and client-specific quirks like **Transmission's peer_id checksum** |
| Impossible speed | **connection profiles** (ADSL→fibre) + **believability warnings** + a **global upstream budget** shared across all tabs (one uplink) |
| **Uploading to nobody** | **swarm-aware speeds**: read the tracker's leecher/seeder counts and scale accordingly — 0 leechers ⇒ a trickle, your share diluted by competing seeders |
| Announce cadence | **interval jitter** (drift later, never earlier than the tracker's `interval`) |
| Flat/robotic stats | **ramp-up** + smoothed variation + **day/night** rhythm + **active hours** |
| Ghost peer / not connectable | **connectable seeder**: answer inbound handshakes with a full **bitfield** then **choke** — connectable and complete, transferring nothing |

None of this makes fake stats *undetectable* — a tracker that correlates against
real peer connections, or requires client-reported checksums, can still catch it.
It makes the tracker-visible story internally consistent and human-shaped.

> **Ethics & scope.** Seedforger is a protocol-education and security-research
> tool. Faking ratio breaks the rules of virtually every private tracker and can
> get you banned. Only use it where you're allowed to.

---

## 14. References

- BEP 3 — The BitTorrent Protocol Specification
- BEP 5 — DHT Protocol · BEP 9 — Metadata exchange (magnets) · BEP 10 — Extension protocol
- BEP 11 — PEX · BEP 12 — Multitracker · BEP 15 — UDP tracker · BEP 23 — Compact peers
- BEP 20 — Peer ID conventions · BEP 52 — BitTorrent v2
- `wiki.theory.org/BitTorrentSpecification` — the community reference
- libtorrent `generate_fingerprint` and Transmission `tr_peerIdInit` — the source
  of the peer-id encodings Seedforger reproduces
