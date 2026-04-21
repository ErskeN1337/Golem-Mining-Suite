# R4 — Pyro QT Geometry & Counter-Piracy Route Analyzer

Research feeding the counter-piracy quantum-travel route analyzer feature. Date captured: 2026-04-21, Star Citizen 4.7.1 LIVE.

> **Data-fidelity caveat (read first).** CIG does not publish canonical world-space coordinates for Pyro bodies or stations. The in-game Starmap and all community maps (VerseGuide, SC-Map, starcitizen.tools) expose *relative* distances in **gigameters (Gm)** and Lagrange-point designators rather than `(x, y, z)` vectors. Absolute coordinate precision at the level SnarePlan consumes (it reads the game's own entity-graph data, or users enter body distances manually) is **not publicly scrapeable** at 4.7.1. The implementation therefore needs either (a) a community-maintained JSON dump that players manually refresh when CIG re-tunes the system (scTools / SC-Map repos are candidates), or (b) a **crowdsourced capture path via our existing Supabase backend** where users submit pull-point events with in-game coordinates from their HUD (`/showlocation`-style commands, map-tool exports). See §6.

---

## 1. QT Interdiction Mechanics in 4.7

### 1.1 Pull-point geometry

Quantum travel in SC is a straight line between the jumper's origin and target. Interdiction devices (QEDs) generate a spherical **snare volume**. Any ship whose QT line passes *through the interior of that sphere* will be pulled out at the point of closest approach to the QED source.

The geometric test is the classic point-to-line-segment distance:

```
# Let QT_A = origin of the quantum traveller,
#     QT_B = target,
#     P    = position of the interdictor (QED source),
#     r    = QED pull radius (20 km for Mantis at LIVE values)
#
# d = ||(P - QT_A) - ((P - QT_A) . u) u||,  u = (QT_B - QT_A) / ||QT_B - QT_A||
# The traveller is snareable iff d < r AND the projection t = (P - QT_A) . u
# is within [0, ||QT_B - QT_A||] (i.e. the closest approach lies on the segment,
# not an extrapolation past either endpoint).
```

Snare zone on the QT line (the interval actually pulled) is the chord:

```
half_chord = sqrt(r^2 - d^2)
pull_interval = [ t - half_chord, t + half_chord ]   # parametric along the QT segment
```

This is the core primitive the analyzer needs; it runs per (route, candidate-pull-point) pair.

### 1.2 Published numbers — 4.7 LIVE

| Ship / Device | Pull radius | Charge time | Notes |
|---|---|---|---|
| RSI Mantis (only production snare ship) | **20 km** spherical | ~90 s QED warm-up before snare can engage | High IR + EM signature while active; visible on scanners out to long range |
| QED dampener (any Mantis/future variants) | ~2 km | Instant | Prevents spool-up, not a puller |
| Minimum QT distance (target must be farther than this for a QT to spool) | ~200–500 km depending on drive class | — | Very short legs cannot be interdicted at all |

*(Sources: starcitizen.tools/Mantis, starcitizen.tools/Quantum_enforcement, SnarePlan community posts.)*

### 1.3 SnarePlan's geometric model (reverse-engineered)

SnarePlan (snareplan.dolus.eu, Priton/Dolus community) solves the inverse problem a pirate needs: *given a set of routes I want to interdict, where do I park?* The tool's published description ("applies trigonometry to determine the mathematically perfect point") plus its output — a single 3-D point per scenario — means it is running an **intersection-of-spheres + line-bundle** search:

1. Model each candidate route as a line segment `(A_i, B_i)` between two bodies or markers.
2. For each route, the set of pirate positions that can snare it is a **cylinder of radius r** around that segment (inclusive of hemispherical end-caps).
3. Finding a position that snares *multiple* routes is the **intersection of N such cylinders**. SnarePlan picks the point that maximises the minimum margin (i.e. the Chebyshev-center of the intersection polytope, clipped to navigable space).
4. Special cases: the Aaron Halo and similar belts constrain the pirate to certain radial shells; Lagrange points exploit the fact that dropout distance is higher near large bodies (roughly the `Gravity Well Radius`), effectively enlarging `r` there.

The consequence for *our* analyzer (a defender tool) is the **dual problem**: given a route `(A, B)`, enumerate the *chord through every candidate snare sphere whose centre lies within `r + margin` of the segment*. Our candidate list is the union of (stations, Lagrange points, known hotspot POIs). The output is a list of (POI, chord-midpoint-t, chord-length) tuples.

### 1.4 Do hand-built routes avoid snaring?

**Yes, meaningfully — but only if the custom markers are placed off the "planetary lanes."** A direct body-to-body QT is what SnarePlan (and pirates) assume. When you drop a custom marker at, say, 2 Gm offset perpendicular to the direct line and QT to that first, then to the destination, both legs have different geometry. Pirates camped on the direct line will not catch you unless they re-reposition (costly: Mantis spool + travel).

Practical rules from community PvP footage (BoredGamerUK, Exedore, Spectrum piracy threads):

- **2-hop routes via a Lagrange point or station** usually evade a single fixed interdictor. The cost is a few minutes of extra QT.
- Offsetting by < 1 Mm is not enough — it still falls within most multi-route snare sweet spots.
- Offsetting by **≥ 5 % of the leg length** perpendicular to the direct line is the rule of thumb used by Rough & Ready–aware haulers.
- Routes that *cross the ecliptic plane* (use "up-and-over" markers) are the hardest to interdict because most pirates park in the plane of the system.

Implementation: the analyzer should score a user-built route by running the intersection test against every known POI and return **chord length (seconds of vulnerability) per POI**, plus a recommended offset if any chord exceeds a threshold.

---

## 2. Pyro System Coordinates (best available, April 2026)

### 2.1 Star and planets

| Body | Type | Orbital distance from Pyro | Moons | Notes |
|---|---|---|---|---|
| **Pyro** (star) | K-type main-sequence flare star, ~11.3 Byr old | — | — | Prolonged nova phase; irregular flares |
| **Pyro I** | Dwarf planet (scorched) | **~8.2 Gm** | 0 | Closest body; extreme radiation |
| **Pyro II — Monox** | Coreless terrestrial, CO atmosphere | *not public* | 0 | L4 hosts Checkmate Station |
| **Pyro III — Bloom** | Magma/volcanic terrestrial | *not public* | 0 | L1 hosts Starlight Service Station; L3 Patch City; high orbit hosts Orbituary |
| **Pyro IV** | Rocky planet | *not public* | 0 | Less-trafficked |
| **Pyro V** | Yellow-green gas giant | *not public* | 6 (Adir, Ignis, Fairo, Fuego, Vatra, Vuur) | L5 hosts Rat's Nest |
| **Pyro VI — Terminus** | Protoplanet (outermost) | **~68.3 Gm** | 0 | Orbited by Ruin Station; L3 hosts Endgame |

The intra-planetary radial distances for Pyro II–V are referenced only as relative gigameters on the in-game Starmap. The scwiki.hu Pyro page and SC-Map expose them in their JS bundles (scrape candidate).

### 2.2 Stations and POIs

| Station | Faction | Parent body / location | Refinery | Trading | Typical role |
|---|---|---|---|---|---|
| **Ruin Station** | XenoThreat (contested) | Orbit of Terminus (Pyro VI) | Yes | Yes | Outlaw hub; big refinery ledger |
| **Checkmate Station** | Rough & Ready | PYR2-L4 (Monox L4) | Yes | Yes | Largest R&R hub |
| **Rat's Nest** | Rough & Ready | PYR5-L5 (Pyro V L5) | No (confirmed services: bar, guns, hangar) | Limited | Social / black-market |
| **Starlight Service Station** | Citizens for Prosperity | PYR3-L1 (Bloom L1) | No | Fuel/repair, ship parts, weapons | Only "civilised" rest stop |
| **Patch City** | Rough & Ready | PYR3-L3 (Bloom L3) | — | Yes | Repair / modding |
| **Orbituary** | Rough & Ready | High orbit of Bloom (Pyro III) | **Yes** | Yes | Refinery + fuel for outlaws |
| **Endgame** | Rough & Ready | PYR6-L3 (Terminus L3) | — | Yes | Far-system outlaw waypoint |
| **Ashland** | Headhunters | Ground outpost on **Ignis** (moon of Pyro V) | No | Scrap/parts | Surface-side; requires atmo descent |
| **Pyro Gateway (Stanton)** | Stanton-side | Near Stanton–Pyro jump, Stanton system | **Refinement Processing** | Yes | Transit point inbound from Stanton |
| **Stanton Gateway (Pyro)** | Pyro-side | Pyro end of Stanton–Pyro jump | Yes (listed in UEX refineries) | Yes | First stop after jump from Stanton |
| **Nyx Gateway (Pyro)** | Pyro-side | Pyro end of Nyx–Pyro jump | Yes | Yes | Added 4.4+; less-trafficked |

### 2.3 Jump points

Pyro is currently documented as hosting connections to as many as seven systems (Cano, Castra, Hadrian, Nyx, Oso, Stanton, Terra) per lore, but the **in-game jump points active as of 4.7.1 LIVE** are:

- **Stanton ↔ Pyro** — Medium jump point (the primary traffic lane; both gateway stations sit at its endpoints).
- **Nyx ↔ Pyro** — Added in 4.4; currently temporarily redirected to Nyx↔Stanton per CitizenCon 2025, but the Pyro end remains defined for routing math.
- Other canonical connections (Terra, Castra, etc.) are lore-only in 4.7 and should not be included in route scoring yet.

**Intra-Pyro jump points:** none in 4.7. All intra-system travel is QT-only.

---

## 3. Known Piracy Hotspots (Spectrum / reddit / YouTube, 4.7 era)

Biggest qualitative finding: **4.7 cargo activity is down** (fewer profitable illegal routes → fewer pirates hunting). That said, the geometric hotspots are well documented:

| Rank | Hotspot | Why it's hot | Ship archetypes seen |
|---|---|---|---|
| 1 | **Stanton ↔ Pyro jump corridor** (both Gateway stations and the ~200 Mm approach lane on each side) | Every cross-system trader uses it; predictable endpoints | Mantis + Cutlass Black escort; Caterpillar boarding crew; Hornet/F7A CAP |
| 2 | **Ruin Station approach (Terminus orbit)** | Unique high-traffic refinery in Pyro; XenoThreat squatting | Mantis + any frigate/Cutlass pair |
| 3 | **Checkmate ↔ Ruin route** | Crosses most of the system; snare anywhere in middle works | Same as above; sometimes Connie Taurus group |
| 4 | **Checkmate (Monox L4) approach** | R&R-controlled but frequent ambushes by rival packs | Mantis + Vanguard or Redeemer |
| 5 | **Orbituary (Bloom high-orbit)** | Refinery = loaded cargo exiting | Mantis + boarding Caterpillar |
| 6 | **Bloom L-points generally (Starlight / Patch City)** | Short legs between them invite crossing ambushes | Lighter pirates; Buccaneer/Cutlass |

**Canonical ambush ship:** **RSI Mantis** is the only QED-equipped vessel in LIVE — all interdiction revolves around it. Typical kill group = **1 Mantis + 1-3 combatants** (Cutlass Black, Hornet F7A Mk II, Redeemer, Vanguard Harbinger). **Caterpillar** shows up as the *boarding/looting* platform once the target is snared and disabled, not as the interdictor itself.

**Time-of-day pattern:** US/EU prime (18:00–23:00 local each region), Friday/Saturday nights. Reports are nearly zero during US/EU early morning. Server population drives encounter rate far more than patch version.

Citations worth tracking manually: Spectrum thread "Fishing Ships — SnarePlan and Quantum Interdiction" (forum/65300), the MMOPIXEL 4.7 "Riskiest Cargo Routes" write-up (which reports essentially no encounters during their testing — i.e. risk is bursty, not steady), and the Community Hub post "Lawless Pyro: Stealth PvP Tactics & Survival Skill." YouTube: BoredGamerUK piracy recaps, Exedore Pyro patrol footage.

---

## 4. Refinery-Relevant Routes — the Miner's Typical Pyro Run

A standard mining loop has three legs:

1. **Mine site → Refinery station** (haul raw ore in Prospector / Mole / ROC hauler)
2. **Refinery dwell** (job queue, 30 min – several hours)
3. **Refinery station → Sell terminal** (haul refined cargo in Hull series / Caterpillar / C2)

Refineries in Pyro confirmed via UEX: **Ruin Station, Checkmate, Orbituary, Stanton Gateway (Pyro), Nyx Gateway (Pyro)**. (Plus Pyro Gateway on the *Stanton* side for runs that refine back home.)

The common leg combinations we should score:

| Leg ID | From | To | Typical cargo | Rough length (Gm) | Notoriety |
|---|---|---|---|---|---|
| PY-MINE-1 | Surface of **Bloom** (dense mineral rush zone) | **Orbituary** | Raw ore | ~ "surface-to-orbit" + 0 intra-lagrange | Low (same-body) |
| PY-MINE-2 | **Ignis** / Pyro V moons surface | **Checkmate** (PYR2-L4) | Raw ore | Long (cross-system) | HIGH |
| PY-MINE-3 | **Terminus** surface / orbit | **Ruin Station** | Raw ore | Short (same body) | Medium (XenoThreat local) |
| PY-MINE-4 | Asteroid belt around any Pyro body | nearest station refinery | Raw ore | Variable | Medium |
| PY-REFSELL-1 | **Ruin Station** | **Checkmate** | Refined | Long (cross-system) | HIGH (the classic pirate corridor) |
| PY-REFSELL-2 | **Orbituary** | **Checkmate** | Refined | Medium | Medium–High |
| PY-REFSELL-3 | Any Pyro refinery | **Stanton Gateway (Pyro)** → jump → Stanton markets | Refined | Long + jump | HIGH (jump corridor) |
| PY-REFSELL-4 | **Checkmate** | **Rat's Nest** / **Endgame** (price spikes occur) | Refined | Medium–Long | Medium |
| PY-REFSELL-5 | **Orbituary** | **Starlight** (for fuel/QT) → **Stanton Gateway** | Refined | Multi-hop (recommended as evasion) | Low–Medium (offset route) |

The **PY-REFSELL-1 (Ruin→Checkmate)** run is the single highest-value target in the system and the route the analyzer should prioritise when suggesting offsets or custom markers.

---

## 5. `piracy-risk.json` — Data File Shape

Proposed schema for the shipped data file (lives alongside the mining-commodity data the suite already consumes):

```jsonc
{
  "schema_version": "0.1",
  "system": "PYRO",
  "patch": "4.7.1-LIVE",
  "updated": "2026-04-21T00:00:00Z",
  "source_notes": "Distances in Gm where known; relative-only otherwise. Coordinate vectors require crowdsourced capture (see README).",

  "bodies": [
    {
      "id": "pyro_star",   "name": "Pyro",        "type": "star",    "parent": null,
      "orbit_radius_gm": 0, "position_xyz_gm": null
    },
    {
      "id": "pyro_i",      "name": "Pyro I",      "type": "planet",  "parent": "pyro_star",
      "orbit_radius_gm": 8.2, "position_xyz_gm": null
    },
    {
      "id": "pyro_ii",     "name": "Monox",       "type": "planet",  "parent": "pyro_star",
      "orbit_radius_gm": null, "position_xyz_gm": null, "lagrange_points": ["L1","L2","L3","L4","L5"]
    },
    {
      "id": "pyro_iii",    "name": "Bloom",       "type": "planet",  "parent": "pyro_star",
      "orbit_radius_gm": null, "position_xyz_gm": null, "lagrange_points": ["L1","L2","L3","L4","L5"]
    },
    { "id": "pyro_iv",  "name": "Pyro IV",   "type": "planet", "parent": "pyro_star", "orbit_radius_gm": null },
    { "id": "pyro_v",   "name": "Pyro V",    "type": "gas_giant", "parent": "pyro_star", "orbit_radius_gm": null,
      "lagrange_points": ["L1","L2","L3","L4","L5"] },
    { "id": "ignis",    "name": "Ignis",     "type": "moon", "parent": "pyro_v" },
    { "id": "pyro_vi",  "name": "Terminus",  "type": "protoplanet", "parent": "pyro_star",
      "orbit_radius_gm": 68.3, "lagrange_points": ["L1","L2","L3","L4","L5"] }
  ],

  "stations": [
    { "id": "ruin_station",      "name": "Ruin Station",            "parent": "pyro_vi",
      "anchor": "orbit",       "faction": "XenoThreat", "services": ["refinery","trade","medical"],
      "position_xyz_gm": null, "notes": "Pull-point prior to dropout: 20km sphere from approach vector"},
    { "id": "checkmate",         "name": "Checkmate Station",       "parent": "pyro_ii",
      "anchor": "L4",          "faction": "RoughAndReady", "services": ["refinery","trade"] },
    { "id": "rats_nest",         "name": "Rat's Nest",              "parent": "pyro_v",
      "anchor": "L5",          "faction": "RoughAndReady", "services": ["bar","shops"] },
    { "id": "starlight",         "name": "Starlight Service Station","parent": "pyro_iii",
      "anchor": "L1",          "faction": "CitizensForProsperity", "services": ["fuel","repair","shops"] },
    { "id": "patch_city",        "name": "Patch City",              "parent": "pyro_iii",
      "anchor": "L3",          "faction": "RoughAndReady", "services": ["repair","trade"] },
    { "id": "orbituary",         "name": "Orbituary",               "parent": "pyro_iii",
      "anchor": "high_orbit",  "faction": "RoughAndReady", "services": ["refinery","fuel","trade"] },
    { "id": "endgame",           "name": "Endgame",                 "parent": "pyro_vi",
      "anchor": "L3",          "faction": "RoughAndReady", "services": ["trade"] },
    { "id": "ashland",           "name": "Ashland",                 "parent": "ignis",
      "anchor": "surface",     "faction": "Headhunters", "services": ["scrap","shops"] },
    { "id": "stanton_gateway_pyro","name": "Stanton Gateway (Pyro side)","parent": "jump_stanton_pyro_pyro_end",
      "anchor": "jump",        "services": ["refinery","trade"] },
    { "id": "nyx_gateway_pyro",  "name": "Nyx Gateway (Pyro side)", "parent": "jump_nyx_pyro_pyro_end",
      "anchor": "jump",        "services": ["refinery","trade"] }
  ],

  "jump_points": [
    { "id": "jp_stanton_pyro", "endpoints": ["STANTON","PYRO"], "size": "medium", "active": true },
    { "id": "jp_nyx_pyro",     "endpoints": ["NYX","PYRO"],     "size": "small",  "active": true,
      "notes": "Temporarily redirected per CitizenCon 2025; verify at build time" }
  ],

  "pull_points": [
    // Each entry is a candidate snare position.  Client computes the point-to-segment
    // distance for the user's planned route against every entry and flags vulnerability.
    {
      "id": "pp_ruin_approach",
      "label": "Ruin Station approach cone",
      "parent": "ruin_station",
      "position_xyz_gm": null,              // CROWDSOURCE REQUIRED
      "relative_offset_km": { "from": "ruin_station", "vector_hint": "sunward", "distance": 50 },
      "pull_radius_km": 20,                 // Mantis LIVE
      "notoriety_score": 0.92,              // 0..1; 1 = almost certainly camped US/EU prime
      "faction_tag": "XenoThreat",
      "evidence_links": ["spectrum/65300", "yt:BoredGamerUK/pyro-piracy-2026"],
      "last_confirmed": "2026-04-10"
    },
    {
      "id": "pp_sjp_corridor",
      "label": "Stanton-Pyro jump corridor midpoint",
      "parent": "jp_stanton_pyro",
      "position_xyz_gm": null,
      "pull_radius_km": 20,
      "notoriety_score": 0.95,
      "faction_tag": "mixed",
      "last_confirmed": "2026-04-18"
    }
    // ...one entry per hotspot enumerated in §3.
  ],

  "recommended_alternates": [
    {
      "for_leg": { "from": "ruin_station", "to": "checkmate" },
      "waypoints": [
        { "type": "lagrange", "body": "pyro_v", "point": "L1", "offset_km": 0 },
        { "type": "body",     "id": "checkmate" }
      ],
      "extra_qt_time_est_sec": 180,
      "rationale": "Breaks the direct Terminus-Monox line; Pyro V L1 lies ~perpendicular to the ambush cone described in §3."
    },
    {
      "for_leg": { "from": "orbituary", "to": "stanton_gateway_pyro" },
      "waypoints": [
        { "type": "station", "id": "starlight" },
        { "type": "station", "id": "stanton_gateway_pyro" }
      ],
      "extra_qt_time_est_sec": 90,
      "rationale": "Two shorter legs through civilised Starlight reduce single-snare coverage."
    }
    // ...
  ]
}
```

Notes on the shape:

- `position_xyz_gm` is intentionally `null` everywhere. The analyzer should treat `null` coords as **inferred-from-parent** using orbit and Lagrange anchors, and only upgrade to real vectors when crowdsourced captures come in. Anchor types `orbit | L1..L5 | high_orbit | surface | jump` cover every real station.
- `notoriety_score` is a simple scalar so the front-end can weight route-risk as `sum_over_snared_pull_points(score * chord_length_fraction)`.
- `evidence_links` and `last_confirmed` are part of the schema from day one — this data decays quickly as CIG rebalances, and provenance needs to ship with the data.

---

## 6. Public Data Sources We Can Pull From Automatically

| Source | Endpoint / Path | What we get | Auth | Fit |
|---|---|---|---|---|
| **UEX Corp API 2.0** | `GET /api/2.0/star_systems`, `/planets`, `/moons`, `/space_stations`, `/outposts`, `/poi`, `/orbits`, `/orbits_distances`, `/refineries_methods`, `/refineries_yields`, `/refineries_capacities` | Station/body metadata incl. orbit distances and refinery bonuses. **No piracy/safety rating endpoint** exists — confirmed from the endpoint catalogue. | Bearer token (free app key; 120 req/min, 172.8 k req/day) | **Primary data backbone.** Pulls cover sections 2.1, 2.2, 4 of this doc. |
| **RSI Starmap** | `https://robertsspaceindustries.com/api/starmap/star-systems/PYRO` (undocumented JSON endpoint) | Canonical system graph incl. approximate body distances and jump-point metadata. | None (public) | Useful cross-check. Schema can change without notice — wrap in try/catch, cache aggressively. |
| **starcitizen.tools** (MediaWiki) | `https://starcitizen.tools/api.php?action=parse&page=Pyro_system&format=json` | Wiki text for Pyro system article + station sub-articles. | None | Good for authoritative descriptive data (faction, services). Returns 403 to raw `GET` in our tests — use the MediaWiki JSON API, not `WebFetch`. |
| **starcitizen.fandom.com** | Same MediaWiki API | Backup wiki with looser access controls; occasionally has fields the main wiki lacks. | None | Fallback. |
| **SC-Map / citizen-history.com** | `https://citizen-history.com/other/sc-map` + its underlying gist | Community gigameter-distance map data. | None | Good for the gigameter numbers missing from UEX. Schema is the author's own. |
| **VerseGuide** (`verseguide.com`) | No public API documented | Interactive location data — browser-scrape only. | None | Low priority; UEX covers 90% of what it does. |
| **Community GitHub repos** | e.g. `github.com/uexcorp` (their own mirror), `github.com/SubliminalsChannel/Scripts`, various fan tools | Loadouts, ship specs, occasional station JSON dumps. | None (most) | Great for ship-capability tables (QED radius, drive classes). Snapshot, don't live-depend. |
| **SnarePlan** (`snareplan.dolus.eu`) | No public API | Web-only calculator. | None | Inspiration only — not a data provider. We reimplement the geometry (§1.3). |
| **Spectrum** (`robertsspaceindustries.com/spectrum/...`) | HTML-only; 403-prone to bots | Qualitative piracy intel. | None, but rate-limited | Manual curation into `evidence_links`. Not automatable at scale. |
| **Reddit** (`r/starcitizen`, `r/starcitizenpiracy`) | Reddit API (free with app) | Piracy anecdotes, hotspot confirmations. | OAuth (free) | Optional: a weekly cron that re-scores `notoriety_score` based on post frequency by hotspot. |

### 6.1 Crowdsourcing recommendation (Supabase)

Because **absolute `(x, y, z)` coordinates in Gm are not publicly scrapeable** at the precision SnarePlan-grade geometry requires, stand up a Supabase table:

```
pull_point_reports (
  id uuid primary key,
  reporter_id uuid references auth.users,
  system text,                       -- "PYRO"
  reported_at timestamptz,
  patch text,                        -- "4.7.1-LIVE"
  position_x_gm double precision,    -- from in-game HUD / developer overlay
  position_y_gm double precision,
  position_z_gm double precision,
  parent_body text,                  -- nearest entity id from our body table
  ambush_confirmed boolean,          -- did an actual pull happen or near-pull?
  ship_seen text,                    -- "Mantis", "Cutlass Black", etc.
  faction_tag text,
  screenshot_url text,               -- storage bucket reference
  verified boolean default false     -- community / staff review
)
```

Aggregate into `pull_points[].position_xyz_gm` nightly: median of last 30 days of verified reports, drop outliers > 3σ. The same path solves the "we don't have body coords either" problem — every report that names a parent body and a relative offset narrows the body's effective centre.

This plugs into the existing suite's Supabase backend with no new infrastructure.

---

## Sources (primary)

- snareplan.dolus.eu — SnarePlan tool
- starcitizen.tools/Pyro_system, /Mantis, /Interdiction, /Quantum_enforcement, /Ruin_Station, /Checkmate_Station, /Rat's_Nest, /Starlight_Service_Station, /Patch_City, /Orbituary, /Endgame, /Ashland, /Pyro_Gateway_(Stanton), /Stanton_Gateway_(Pyro)
- robertsspaceindustries.com Galactapedia (Pyro system, Monox, Checkmate)
- robertsspaceindustries.com/spectrum — "Fishing Ships — SnarePlan and Quantum Interdiction" (forum/65300)
- robertsspaceindustries.com Community Hub — "Lawless Pyro: Stealth PvP Tactics & Survival Skill"; "4.0: Jumps from Stanton to PYRO"
- uexcorp.space/api/documentation — UEX 2.0 endpoint catalogue
- mmopixel.com — "Star Citizen 4.7: Riskiest and Deadliest Cargo Routes" (qualitative traffic data)
- YouTube PvP channels (BoredGamerUK, Exedore, SubliminalsTV) — observational ship-loadout data
- citizen-history.com/other/sc-map — community gigameter distance map
