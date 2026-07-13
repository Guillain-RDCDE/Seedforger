# How it actually works (no code)

[← back to the README](../README.md)

You don't need to read the protocol spec to get the shape of it. (When you *do* want the bytes, see the [from-the-wire deep dive](how-bittorrent-works.md).)

---

**1. The tracker trusts you.** When a real client seeds, every few minutes it sends the tracker a little report: *"I'm client X, I've now uploaded N bytes on this torrent."* The tracker has **no way to independently measure** your upload — it simply adds up the numbers you send. That trust is the entire attack surface, and it's why ratio faking is possible at all.

**2. So why can't you just send a huge number?** Because private trackers run **anti-cheat**, and a lone big number is easy to spot:

| The tracker asks… | …and a naïve faker fails because |
|---|---|
| *Is this a real, allowed client?* | its `peer_id`/`User-Agent` isn't on the whitelist, or they disagree |
| *Is this speed physically possible?* | it "uploaded" faster than any home line, or more than the file's total size |
| *Is the timing human?* | it announces like a metronome, or more often than allowed |
| *Do the numbers add up?* | announce totals don't match the scrape data or the `left` value |
| *Is there a real peer behind this?* | the port never accepts a connection; no monitored peer ever got a byte |

**3. Seedforger's job is to pass every one of those checks.** It layers believability, from cheap to deep:

- **Look like a real client** → accurate, *current* fingerprints (`-qB5230-`, `-TR4130-`, …), optional **client rotation**, even client-specific quirks like Transmission's `peer_id` checksum.
- **Move at a believable speed** → **connection profiles** (ADSL→fibre) set sane limits, a **ramp-up + gentle wobble** replaces the flat line, and a **global upstream budget** makes all your torrents share one uplink like they'd share one real connection.
- **Only feed demand that exists** → **swarm-aware speeds** read the tracker's live leecher/seeder counts. Zero leechers → you trickle (nobody to feed). Lots of competing seeders → your share is diluted. The physics line up.
- **Keep timing human** → **interval jitter** (drift *later*, never earlier than the tracker allows), a **day/night rhythm**, and an **active-hours** window so you're not seeding at 4 a.m. every night forever.
- **Be a connectable peer** → answer inbound handshakes on your port with a full **bitfield + choke**: a visible, complete seeder that happens to transfer nothing.
- **Survive active spies (the deep end)** → a tracker can inject **monitoring peers** that *request* real blocks and check you deliver. The only real answer is to **serve genuine, hash-valid data**. Seedforger can — with a real TCP peer-wire engine — and then leans on one elegant fact: *a spy only sees its own connection.* Serve real data at a steady per-peer rate, and claim a total of `served × plausible peers`; no single observer can refute it. Past this point, "faking convincingly" quietly converges on "being a real, lazy BitTorrent client" — which is the honest note the whole exercise ends on.

**4. The honest limit.** None of this is magic. A tracker that correlates announces against *actual* peer-to-peer connections, or demands client-side checksums, can still catch you. Seedforger makes the tracker-visible story internally consistent and human-shaped — no more, no less.

---

Want the bytes, not the metaphor? The **[from-the-wire guide](how-bittorrent-works.md)** explains every layer above down to the handshake. Or browse the full **[feature catalogue](features.md)**.
