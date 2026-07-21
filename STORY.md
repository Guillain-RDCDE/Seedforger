# Ten Days on a Real Tracker

*A field report — how Seedforger held up against a live private tracker, and what it taught us about believability.*

[← back to the README](README.md)

> This is a first-person account of running Seedforger against a **private tracker we were a member of, on our own account**. It is not a how-to for cheating a community you belong to — the warnings in the [README](README.md#overview) still stand, and none of what follows makes fake stats *undetectable*. It's a story about the gap between *sending a number* and *telling a story a tracker believes*.

---

## The problem we actually had

The account was real. The ratio was not healthy: **0.69**, under the tracker's **0.80** floor. On a private tracker that number is a countdown — cross the line and you lose the ability to grab anything new. The honest fix is to seed popular torrents for weeks and hope enough leechers show up to take bytes off you. The tracker, meanwhile, does not *watch* you upload. It keeps score from the numbers your client reports on every announce. That asymmetry is the whole premise of Seedforger — and the whole reason a tracker runs anti-cheat to defend it.

So this was the test we'd been building toward: not "can we make the number go up" (that's one line of code), but **"can the number go up and *stay believed* over days, against a tracker that scrapes, correlates and flags."**

## What we did *not* do

We did not point a fresh install at the tracker and type "10 TB uploaded." That is exactly the tell every anti-cheat script is written to catch: a ratio that jumps a decade in an afternoon, from a client whose fingerprint doesn't match its `User-Agent`, announcing on a robotic 30-minute metronome, claiming an upload rate no home line can push, on a port with no peer behind it, with figures that don't reconcile against the tracker's own scrape of the swarm.

Every one of those is a check. Seedforger's job was to answer all of them at once, and keep answering for ten days.

## The setup

- **One client identity, held steady.** We impersonated a current qBittorrent — matching `peer_id` prefix *and* `User-Agent`, header order included. Not a rotating carousel that would look like a botnet; one machine, consistent across the run.
- **Swarm-aware speeds.** Reported upload was tied to the tracker's live leecher count. When nobody was downloading, we claimed a trickle. When a torrent got hot, the claim rose *with* the demand — the way a real seeder's upload actually behaves.
- **A believable home line.** We set an upstream budget that matched a plausible residential connection and let the global cap share it across torrents. No 500 Mbps miracle.
- **Human timing.** Announces drifted *later* than the tracker's interval, never earlier, with day/night rhythm on the speeds — busy in the evening, quiet at 4 a.m.
- **A real peer behind the port.** For the torrents we actually held on disk, we armed the real peer-wire engine: genuine, SHA-1-verified blocks served over TCP, capped by the governor so we never claimed more than we could have defended if a monitoring peer came knocking.

Then we let it run — mostly on the **headless CLI**, the way you'd run it on a seedbox — and checked in.

## Ten days

**Day 1.** The dry-run announce came back accepted: seeders, leechers, interval — the tracker was talking to us as a legitimate seeder. We started for real and walked away.

**Day 2–3.** The ratio moved, but slowly. This is the part newcomers get wrong: believability is *deliberately* slower than cheating. The swarm-aware cap means an empty swarm earns you almost nothing — which is the point. We nudged the torrent selection toward titles with live leechers (the Guided wizard does exactly this probing), and the curve steepened.

**Day 4–7.** Steady climb. No warnings from the tracker, no reset, no message. The account crossed **0.80** and kept going. The graph — cumulative upload against ratio — looked like a real seeder having a good week, because mechanically that's what the tracker was being shown: consistent client, plausible rate, demand-linked, human-timed, with a connectable seeder answering on the port.

**Day 8–10.** The ratio didn't just clear the floor — it **cleared it with room to spare**. Comfortable margin, account healthy, nothing flagged. Ten days, no intervention beyond the occasional check-in.

## What actually mattered

The number was never the hard part. What carried the run was that **no single check had an easy answer to give.** Pull any one thread — fingerprint, speed, timing, port, reconciliation with scrape data — and it held. That's the entire design thesis of Seedforger stated as an outcome: *fake stats survive not by being large, but by being internally consistent with everything else the tracker can see.*

Two things earned their keep more than we expected:

1. **Swarm-awareness did the believability heavy lifting.** The instinct is to crank the speed. The realistic move is to let an empty swarm pay you almost nothing and only earn when there's real demand to earn against. Slower, and far harder to flag.
2. **The port mattered.** A claimed seeder with a dead port is a contradiction a monitoring peer resolves in one connection attempt. Serving real, hash-valid blocks — even slowly, even capped — closed the one gap that a number alone can never close.

## The honest caveats

- This was **one account, one tracker, ten days.** It is a data point, not a guarantee. A tracker that tightens correlation between announces and observed peer connections can still catch any of this.
- The techniques don't make fakery *invisible* — they make it *consistent*. A determined anti-cheat with peer-level monitoring beats consistency.
- We ran it on **our own account, on a tracker we belonged to.** Doing this to a community that's counting on you is a different thing, and against the rules of virtually every private tracker. That's on you, not the tool.

## The takeaway

Ten days on a live tracker turned a red **0.69** into a healthy, comfortable ratio — with zero flags — not by sending a big number, but by sending a *believable* one, over and over, from a client that looked, timed, spoke and served like the real thing.

That gap — between *a number* and *a believed number* — is the whole project. This run is the closest we've come to measuring it in the wild.

---

*Curious how each of those checks works, and how Seedforger answers it? Start with [How it actually works](docs/how-it-works.md), then the [from-the-wire deep dive](docs/how-bittorrent-works.md).*
