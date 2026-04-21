# Post-Regolith Competitive Landscape

**Research date:** 2026-04-21
**Purpose:** Position Golem Mining Suite v1.4.0-beta for the June 1, 2026 Regolith Co. shutdown migration.
**Creator's goodbye:** Raychaser, 2026-03-10 — https://docs.regolith.rocks/blog/2026/03/10/GoodBye-Regolith/
**Spectrum PSA thread:** https://robertsspaceindustries.com/spectrum/community/SC/forum/65300/thread/psa-community-mining-resource-regolith-is-shutting

---

## Executive Summary

No single surviving tool replaces Regolith's full stack. The closest in *spirit* is **Mining Ops (getallsky)**, which covers live crew state, refinery timers, and payout splits but is lighter on scouting and loadouts. **Rocks Syndicate** is the most polished calculator/data hub, but it is not a session tool. **MFA** is the best fracture/fleet power engine but has no crew/session/scouting/payout layer. Everything else (UEX, SC-Trade, Gallog, scorg.tools, CStone) solves one slice each.

The biggest unfilled gap is **persistent, Discord-identity, shared scouting-find database tied to live session state** — the single Regolith feature nobody is rebuilding end-to-end.

---

## A. Feature-Parity Matrix

Legend: Full = ✔, Partial = ~, None = ✘, N/A for that tool's scope = —

| Feature (Regolith baseline)          | Regolith Co. | Mining Ops | Rocks Syndicate | MFA (Fracture Analyser) | UEX Corp | SC-Trade | scorg.tools | CStone | Gallog | Golem Mining Suite v1.4.0-beta |
|---|---|---|---|---|---|---|---|---|---|---|
| Multi-crew live session tracking     | ✔ | ✔ (IDLE/MINING/HAULING/SCOUTING/HELP) | ✘ | ~ (fleet roster, no crew state) | ✘ | ✘ | ✘ | ✘ | ✘ | ~ (local crew-session browser) |
| Refinery work orders                  | ✔ | ✔ (ore, station, duration, timer) | ~ (calc only) | ✘ | ~ (workload avgs, not personal) | ✘ | ~ (Quantanium only) | ✔ (calc) | ~ (refinery timers) | ✔ |
| Scouting-finds shared DB              | ✔ | ✘ | ~ (ore locations/signatures, static) | ✘ | ✘ | ✘ | ✘ | ✘ | ✘ | ✘ (planned) |
| Per-member aUEC contribution + auto-split | ✔ | ✔ (weighted shares) | ✘ | ✘ | ✘ | ✘ | ✔ (Quantanium focus) | ✘ | ✘ | ✔ (aUEC share tracking) |
| Discord OAuth crew identity           | ✔ | ✘ (no account) | ✘ | ✘ | ✘ | ✘ | ~ (org apps) | ✘ | ✘ | ✘ |
| Refinery pickup timers (countdown)    | ✔ | ✔ (turns green at 0) | ✘ | ✘ | ~ (static workload) | ✘ | ~ (timing calc) | ~ (time/cost calc) | ~ | ✔ (desktop toasts) |
| Ship loadout DB                       | ✔ | ✘ | ✔ (blueprint/fracture calc) | ✔ (laser heads, modules, gadgets) | ~ (vehicle data) | ✘ | ✘ | ✘ | ~ (ship components) | ✘ |
| 4.7 quality score (0-1000)            | ✘ (reason for shutdown) | ~ | ~ | ✔ | ~ | ✘ | ✘ | ~ | ✘ | ✔ (Debuff→Endgame badges) |
| Offline / desktop native              | ✘ | ✘ (web) | ✘ (web) | ✔ (PWA + Windows EXE) | ✘ | ✘ | ✘ | ✘ | ✘ | ✔ (WinForms/.NET 8) |
| Open source                           | ~ (server code only, no DB) | ✘ | ✘ | ✔ (GPL-3.0) | ~ (API) | ✘ | ✘ | ✘ | ✘ | ✔ (MIT) |
| Regolith import path                  | — | ✘ | ✘ | ✘ | ✘ | ✘ | ✘ | ✘ | ✘ | ✔ (JSON file + API key pull) |
| Piracy / risk awareness               | ✘ | ✘ | ✘ | ✘ | ✘ | ~ (routes) | ✘ | ✘ | ✘ | ✔ (Pyro route risk scoring) |
| Game.log integration                  | ✘ | ✘ | ✘ | ~ (OCR of HUD) | ✘ | ✘ | ✘ | ✘ | ✘ | ✔ (QT/death/stall/disconnect) |

Sources: rocksyndicate.com, mining.getallsky.net, github.com/esramos-design/Mining-Fracture-Analyser, uexcorp.space, sc-trade.tools, scorg.tools, finder.cstone.space, gallog.co, regolith.rocks/about/faq.

---

## B. Tool-by-Tool Deep Dive

### 1. Rocks Syndicate — https://www.rocksyndicate.com/
**Positioning:** "A Regolith alternative, community-driven, always free." Marketed directly in search indexing as the next step for Regolith users.
**Covers:** fracture calculator (laser/power setup simulation), ore locations, blueprint quality calc, scan signature lookup (signal × rock pool), refinery bonuses, live UEX price sync via API key.
**Does NOT cover:** no live session state, no work-order list per crew member, no payout splitting, no Discord OAuth, no shared scouting finds (only static ore-location reference).
**Dev status:** Active — currently updated for Alpha 4.x including Lyria, Aaron Halo Belt tier-S locations.
**Verdict:** Strongest *data/calculator* replacement, NOT a session tool. Solo miner's dashboard.

### 2. Mining Ops (mining.getallsky.net) — https://mining.getallsky.net
**Positioning:** "Free tool that tracks your Star Citizen mining crew live, manages refinery timers, and auto-splits aUEC profits by share."
**Covers:** Live crew state tracker with explicit IDLE / MINING / HAULING / SCOUTING / NEEDS HELP states (directly mirrors Regolith session model); refinery work-order entry with ore/location/duration + green-at-zero countdown timer; payout split by weighted shares; multi-system (Stanton/Pyro/Nyx) refinery comparison; tier-rated ore prices (S/A/B/C).
**Does NOT cover:** No account/no Discord OAuth (positive for friction, negative for persistent org identity), no shared scouting-find DB, no ship loadout DB, no long-term session history.
**Verdict:** Functionally the closest thing to a drop-in Regolith replacement for *session mechanics*. Weakness: ephemeral, anonymous.

### 3. Mining Fracture Analyser / MFA — https://github.com/esramos-design/mfa.github.io
**Positioning:** "Real-time cooperative mining calculator & fleet manager for Star Citizen."
**Covers:** Total Combined Effective Laser Power (MW) against rock resistance; fleet roster (Prospector, MOLE, Drake Golem); drag-drop screenshot OCR of mining HUD; Google Gemini 2.5 Flash AI advice; PWA + standalone Windows EXE; v5.35 active, 289 commits, GPL-3.0, validated against Regolith.rocks + UEXCorp.
**Does NOT cover:** No crew session state, no work orders, no scouting DB, no Discord auth, no payouts. Author has no public roadmap suggesting expansion into those areas.
**Verdict:** Best *fracture/power* tool on the market. Could theoretically expand, but the repo shows zero movement toward session/crew coordination. Complement, not replacement.

### 4. Announced post-shutdown tools
No new "built to replace Regolith" tool was visible in March-April 2026 indexed content. The Regolith creator explicitly pointed users only at **SC-Trade Tools** and **UEX Corp**, both of which are trade/market tools, not crew tools. The server code for Regolith was open-sourced but the announcement notes it is incomplete (no DB schema/records), making community revival unlikely. Source: lemmy.world/post/44559558, docs.regolith.rocks/blog/2026/03/10/GoodBye-Regolith/.

---

## C. Adjacent / Partial-Coverage Tools

| Tool | URL | Covers | Gap vs Regolith |
|---|---|---|---|
| **UEX Corp** | https://uexcorp.space | Refinery workload averages, commodity prices, player marketplace | No crew sessions, no payouts, no scouting |
| **SC-Trade Tools** | https://www.sc-trade.tools | Trade routes, item finder, mining routes | Zero crew-coordination layer |
| **scorg.tools — Mining** | https://scorg.tools/mining | Quantanium-focused crew payout shares (100-share model) | Single-commodity, no sessions, no scouting |
| **CStone.space** | https://finder.cstone.space | Refinery yield/cost/time/profit calculator | No multiplayer, no sessions |
| **Gallog** | https://gallog.co | Trade routes, refinery timers, commodity charts, Discord bot (GalLog Bot) | Org-internal focus, no scouting/payout |
| **Iridium FleetBot** | https://top.gg/bot/744369194140958740 | Discord fleet tally per org | Fleet inventory, not mining sessions |
| **dastro-bot / sc-janus / Zephyr** | GitHub | Org info in Discord | None of Regolith's features |
| **Digital Taxidermy Miner Share** | https://www.digitaltaxidermy.co.uk/star-citizen-mining-calculator | Simple aUEC share calculator | Calculator only, no state |
| **Mining Material Finder 2.0** | robertsspaceindustries.com community hub | Web + Discord mineral location lookup | Static, Discord bot currently offline |

---

## D. Community Voices (March-April 2026)

Community discussion was less voluminous than expected — most public reaction is on Lemmy, Discord (1,200+ members, off-index), and in short Spectrum replies.

- **macniel@feddit.org (Lemmy, Mar 2026):** "I started to rely on this nifty Website especially for the mining jobs... The UI was also beautifully crafted so its sad to see it go." Specifically flagged the incomplete GitHub (no DB records) as blocking community revival. Source: https://lemmy.world/post/44559558
- **Regolith's own farewell (Raychaser, 2026-03-10):** "I just don't have the time to keep up with the pace of Star Citizen's development anymore" — and explicitly endorsed only SC-Trade Tools and UEX Corp, leaving crew/session space unnamed.
- **Lemmy community sidebar consensus** surfaces only: UEXCorp, Gallog, Universal Item Finder (CStone), Erkul — none cover session/crew. Signal: users are *discovering* they have no direct replacement.
- **CIG direction** (referenced in Regolith shutdown post + Spectrum): Star Citizen 1.0 is expected to contain in-game tooling for "everything Regolith currently does" — meaning third-party tools have a window of 12-24 months before CIG subsumes the baseline.

**Priorities users are expressing (in rank order of mention frequency):**
1. Clean UI / low-friction session setup
2. Payout share automation (most "daily use" feature)
3. Scouting finds shared across crew
4. Discord identity / org sync
5. Offline / not-dependent-on-some-guy's-server (burned by shutdown)
6. Open-source / forkable

Item 5 is the shutdown's unique teaching moment and a **strong native-desktop tailwind for Golem.**

---

## E. Golem Mining Suite Position (Internal Strategy)

### Strengths vs. the field (v1.4.0-beta)
- **Only tool with a Regolith import path** (file + live API) — this is a migration play that nobody else has built. Window is March-June 2026.
- **Desktop-native + offline-capable** directly addresses the "burned by shutdown" sentiment. Web tools cannot match this.
- **4.7 quality score (0-1000) + tier badges** matches MFA and ahead of Regolith (which never shipped it — literally a shutdown reason).
- **Refinery pickup desktop toasts** beats Mining Ops' in-browser timer for players with minimized windows.
- **Piracy route risk** is unique — no competitor scores legs against Pyro hotspots.
- **Game.log tailing** (QT/death/stall/disconnect) is foundation only, but no web competitor can do this.
- **MIT-licensed** and .NET 8 hardened (130+ xUnit tests, IHttpClientFactory, ILogger, CsvHelper, secrets off disk) — meaningful trust signal post-shutdown.

### Weaknesses / gaps
- **No live multi-crew sync.** Golem's crew-session browser is *local*. Mining Ops and Regolith both have real-time multi-client sync; this is the single biggest feature gap.
- **No Discord OAuth / org identity.** Users losing Regolith also lose persistent cross-crew identity.
- **No shared scouting-finds DB.** This is a moat-worthy feature no post-Regolith tool has claimed.
- **No ship loadout DB** (Rocks Syndicate and MFA both have one).
- **Desktop-only = Windows only.** Mining Ops runs anywhere.

### Recommended next wave ("kill the competition" feature)
**Shared scouting finds + crew sessions backed by a lightweight sync layer (Supabase already listed in v1.4.0-beta stack via `ISupabaseService`).**
- This is the single Regolith feature nobody has claimed.
- Golem already has the Supabase client, desktop toasts, and local crew browser — the jump to "optional shared session via Supabase Row-Level Security + Discord OAuth" is the smallest feasible step to parity.
- Pair with **"Import your Regolith scouting finds"** as a migration trigger (Regolith's scouting endpoints are already schema-mapped in `tasks/research/_regolith_schema/scouting.gql`).

### Secondary move
Publish a **Regolith-to-Golem migration guide** during April-May 2026 while the Regolith API is still live. The Discord community of 1,200+ is actively looking and there is no other tool positioning directly for this handoff.

### Tertiary move
Loadout DB — lowest marginal user value but highest parity-matrix optics when users compare side-by-side.

---

## F. Sources

- https://docs.regolith.rocks/blog/2026/03/10/GoodBye-Regolith/
- https://robertsspaceindustries.com/spectrum/community/SC/forum/65300/thread/psa-community-mining-resource-regolith-is-shutting
- https://lemmy.world/post/44559558
- https://regolith.rocks/about/faq
- https://www.rocksyndicate.com/
- https://mining.getallsky.net
- https://github.com/esramos-design/Mining-Fracture-Analyser
- https://github.com/esramos-design/mfa.github.io
- https://uexcorp.space/
- https://uexcorp.space/mining/refineries
- https://www.sc-trade.tools/mining
- https://scorg.tools/mining
- https://finder.cstone.space/
- https://dutchdemons.com/tool/cstone-space/
- https://gallog.co/
- https://www.gallog.co/articles/gallogbot-faq
- https://www.digitaltaxidermy.co.uk/star-citizen-mining-calculator
- https://scfocus.org/resources/
- https://github.com/ErskeN1337/Golem-Mining-Suite
- https://regolith.rocks/survey
- https://regolith.rocks/workorder
