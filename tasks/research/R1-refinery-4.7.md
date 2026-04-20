# R1 — Star Citizen 4.7 Refinery Model (Implementation Spec)

**Target:** C# developer updating `RefineryService` in the Golem Mining Suite (WPF).
**Game version covered:** Star Citizen Alpha 4.7.x LIVE (released 24 March 2026; 4.7.1 tracked by UEX as of research date).
**Research date:** 2026‑04‑21.
**Confidence scale used below:** Confirmed / Partial / ⚠️ Verify before shipping.

---

## 0. TL;DR — the headline finding

The **multi‑input refining system with primary + secondary byproducts** (e.g. "iron ore + carbon → steel") that has been widely discussed for 4.7 is **NOT live in 4.7**. It is the future design direction announced by CIG and covered in community guides, but the current 4.7 implementation still uses the **classic 1‑ore → 1‑refined‑material** refinery, with the new **0–1000 quality score** bolted on.

- Source: MMOPIXEL, *Star Citizen Crafting Future – How Refining Works in 4.7 and Beyond*: "Currently in 4.7, refining is still using the old system, and the new multi‑input refining is not in yet." https://www.mmopixel.com/news/star-citizen-crafting-future-how-refining-works-in-star-citizen-4-7-and-beyond
- Cross‑reference: StarZen shadow patch notes for 4.7: refining "separates materials by quality, leading to inefficiencies" — confirms old 1:1 refinery, split per quality tier. https://www.starzen.space/t/star-citizen-shadow-patch-notes-4-7-crafting-and-mining-hidden-changes/35594

**Implication for `RefineryService`:** keep the classic refinery formulae; treat "secondary byproduct" as a forward‑compatibility stub, not a ship‑blocking requirement. A byproduct column in your data model is fine, but every row should currently be NULL or "n/a" with a clear comment. Shipping pre‑populated pairings (iron→carbon, etc.) in 4.7 would produce **wrong** numbers.

---

## 1. Ore → Refined Outputs Table

In 4.7 the ore→refined‑material mapping is unchanged from 4.6 and earlier: each raw ore refines 1:1 to a same‑named (or near‑same‑named) refined commodity. The only 4.7 change is that the ore now carries a 0–1000 quality score, and refined material stacks are split by quality tier.

"Secondary byproduct" column is included because the spec asks for it, but — per Section 0 — **every byproduct cell is currently ⚠️ Verify / not live in 4.7**. The pairings listed (iron+carbon = steel, etc.) are *design intent from CIG/MMOPIXEL*, not shipped game behaviour. Do **not** bake them into production refinery math today.

| Raw Ore | Primary Refined Output (game name) | Secondary Byproduct (planned, ⚠️ not live) | Quality propagates to | Source |
|---|---|---|---|---|
| Aluminum (Ore) | Aluminum | ⚠️ none known / not live | Primary only (secondary N/A) | https://starcitizen.tools/Aluminum_(Ore) (title/snippet only; body 403'd in research) |
| Iron (Ore) | Iron | ⚠️ Carbon (design intent — iron+carbon→steel, future system) | Primary only today | MMOPIXEL future‑refining article (above) |
| Titanium (Ore) | Titanium | ⚠️ none published | Primary only | https://starcitizen.tools/Titanium_(Ore) |
| Copper (Ore) | Copper | ⚠️ none published | Primary only | SC Wiki (title/snippet) |
| Gold (Ore) | Gold | ⚠️ none published | Primary only | https://starcitizen.tools/Gold_(Ore) |
| Tungsten (Ore) | Tungsten | ⚠️ none published | Primary only | https://starcitizen.tools/Tungsten_(Ore) |
| Beryl (Raw) | Beryl | ⚠️ none published | Primary only | UEX `commodities` + SC Wiki |
| Bexalite (Raw) | Bexalite | ⚠️ none published | Primary only | UEX `commodities` + SC Wiki |
| Hephaestanite (Raw) | Hephaestanite | ⚠️ none published | Primary only | SC Wiki |
| Laranite (Raw) | Laranite | ⚠️ none published | Primary only | SC Wiki |
| Agricium (Ore) | Agricium | ⚠️ none published | Primary only | SC Wiki |
| Taranite (Raw) | Taranite | ⚠️ none published | Primary only | SC Wiki |
| Quantainium (Raw) | Quantanium (note alt spelling in game) | ⚠️ none published | Primary only | https://starcitizen.tools/Quantanium ; UEX audit example uses `Quantanium` |
| Borase (Raw) | Borase | ⚠️ none published | Primary only | SC Wiki / UEX |
| Corundum (Raw) | Corundum | ⚠️ none published | Primary only | UEX column `CORU` on refineries table |
| Lindinium (new in 4.7) | Lindinium | ⚠️ none published | Primary only | https://www.mmopixel.com/news/star-citizen-4-7-complete-mining-guide ; UEX `LIND` |
| Tin / Tungsten / Quartz / Savronite / Atacamite / Dolivine / Hadanite (gems) | **Do not refine** — sold raw / used direct | — | N/A | TEST Squadron guide: "Gems such as Hadanite and Dolivine do not need to be refined." https://testsquadron.com/threads/the-test-refinery-deck-refinery-guide-v3-12.18413/ |

**⚠️ Verify before shipping per row:** the byproduct column is aspirational. Every row in the Byproduct column is currently unreleased / ambiguous. Code the schema to accept NULL, default to NULL, and do not display byproduct UI until CIG confirms the mapping per ore.

Canonical commodity codes used by UEX (helpful when calling the API): AGRI, ALUM, BERY, BEXA, BORA, COPP, CORU, GOLD, HEPH, IRON, LARA, LIND, QUAN, QUAR (quartz), SAVR (savronite), TARA, TITA, TORI (thorium? — confirm), TUNG. Source: UEX `refineries` table column headers (https://uexcorp.space/mining/refineries).

---

## 2. Quality Score Mechanics

### How the 0–1000 score is computed

- **Confirmed range:** 0–1000 per resource instance, with "most rocks tending to fall in the 200–600 quality range."
  - Source: ggwtb.com, *4.7 Mining Guide: Quality System & Profit Tips*. https://ggwtb.com/blog/star-citizen-4-7-mining-guide-best-ships--builds--quality-system-profit-tips (summary captured via WebSearch; page body returned 403 on direct fetch)
- **Per‑rock multi‑quality:** A single rock can contain "multiple quality versions of minerals" — a rock is not one quality, it's a distribution.
  - Source: StarZen shadow patch notes for 4.7. https://www.starzen.space/t/star-citizen-shadow-patch-notes-4-7-crafting-and-mining-hidden-changes/35594
- **Exact computation formula: NOT PUBLISHED.** No public source reveals whether score is derived from composition purity, mass, instability, or an opaque PRNG. Treat as an opaque server‑assigned integer. Do not try to re‑derive.

### How quality propagates through refining

- Current 4.7 (the old 1:1 refinery): "every unique quality level stays separate through the process, and your refined materials will maintain those quality distinctions" — meaning Q=432 ore refines to Q=432 refined metal, and stacks **never merge** across quality tiers.
  - Source: MMOPIXEL future‑refining article. https://www.mmopixel.com/news/star-citizen-crafting-future-how-refining-works-in-star-citizen-4-7-and-beyond
- Future (NOT live in 4.7 — do not implement): primary input sets ceiling for refined quality, secondary determines required **amount**. Formula shape (paraphrased from MMOPIXEL): `refinedQuality ≈ primaryQuality`; `secondaryRequired = f(1 / secondaryQuality)`.

**Recommended `RefineryService` model:** each input stack carries a single integer `Quality ∈ [0,1000]`; refined output preserves it 1:1; do **not** average, do **not** blend, do **not** merge stacks of different quality.

### Crafting quality thresholds

All from ggwtb.com *4.7 Mining Guide* (WebSearch summary, page 403'd on fetch):

| Threshold | Effect | Practical use |
|---|---|---|
| **< 500** | Crafted item comes out **worse** than the store‑bought equivalent — explicit *debuff* | Sell as raw commodity; do not craft |
| **500** | Baseline — equal to store‑bought | Break‑even |
| **≥ 650** | Balanced community rule‑of‑thumb for "use it" | Craft candidate |
| **≥ 700** | "Keeper" threshold for crafting | Stockpile |
| **≥ 800** | Special / worth keeping even without a present need | Reserve for high‑end gear |
| **≥ 900** | High‑end stockpile tier | Endgame crafting |

Cite source: https://ggwtb.com/blog/star-citizen-4-7-mining-guide-best-ships--builds--quality-system-profit-tips

### Does quality affect yield / time / cost?

- **Yield:** Not confirmed. Community sources discuss yield as a function of **refinery method × refinery station bonus × ore type**, not quality. Quality appears to be an *attribute carried through* refining, not an input to the yield formula, in 4.7.
- **Time:** No published evidence quality affects refining time in 4.7.
- **Cost:** No published evidence quality affects refining cost in 4.7.

**Recommendation:** In `RefineryService`, treat quality as an orthogonal metadata tag on input/output stacks. Yield/time/cost math is the legacy `method × station × ore` formula.

---

## 3. Refinery Methods Table

There are **9 methods** in the game (UEX lists all nine). Community sources (UEX, Mining Mom, TEST Squadron) agree that CIG has never published exact numeric multipliers; all public tables are derived from player data runners. UEX exposes only *relative* ratings (1=low, 2=medium, 3=high) via the API.

### UEX qualitative table (confirmed — this is what the API returns)

Source: UEX `/refineries_methods` endpoint + UEX `/mining/methods` page. https://uexcorp.space/mining/methods

| Method | `rating_yield` | `rating_cost` | `rating_speed` | Best for | 4.7 change? |
|---|---|---|---|---|---|
| Cormack Method | Low (1) | Moderate (2) | Fast (3) | Low‑value ores you want out fast; baseline reference for processing time | None observed |
| Dinyx Solventation | High (3) | Low (1) | Slow (1) | High‑value ores when time is no object; baseline reference for cost | None observed |
| Electrostarolysis | Moderate (2) | Moderate (2) | Moderate (2) | Mixed loads; ~½‑day jobs; ~17% less profit than Dinyx per community reports | None observed |
| Ferron Exchange | High (3) | Moderate (2) | Slow (1) | Best practical yield/cost/speed compromise per community | None observed |
| Gaussing | Moderate (2) | High (3) | Fast (3) | Time‑critical high‑value jobs (note: UEX also refers to a "Gaskin Process" row — may be renamed / synonym — ⚠️ verify) | ⚠️ possible rename in 4.7 — source disagrees with wiki |
| Kazen Winnowing | Low (1) | Moderate (2) | Moderate (2) | Niche / specific ores | None observed |
| Pyrometric Chromalysis | High (3) | High (3) | Slow (1) | Exotic ores where max yield matters more than cost | None observed |
| Thermonatic Deposition | Moderate (2) | Moderate (2) | Slow (1) | General‑purpose fallback | None observed |
| XCR Reaction | Low (1) | High (3) | Fast (3) | Only when you need speed and don't care about cost/yield | None observed |

Sources corroborating the qualitative matrix:
- https://uexcorp.space/mining/methods (the canonical list; ratings 1/2/3)
- https://www.scminingmom.com/post-mining/refining (Mining Mom — site body returned minimal content via fetch, but title confirmed)
- https://testsquadron.com/threads/the-test-refinery-deck-refinery-guide-v3-12.18413/ (TEST v3.12 guide; note it is pre‑4.7 and the table uses older naming like "Electrostatic", "Pyrogenic", "Crystalline" — treat as historical)
- Relative quotes from community: "Dinyx Solventation has the lowest cost"; "Cormack has the lowest processing time"; "Ferron Exchange generates only 2.41% less money than Dinyx but is 3× faster"; "Electrostarolysis takes only half‑a‑day but results in 17% less profit."

### Numeric multipliers

⚠️ **No authoritative numeric multipliers exist in public sources for 4.7.** CIG has never shipped them; all historical spreadsheets were reverse‑engineered from player data runs on pre‑4.7 versions. `RefineryService` should either:
1. Query UEX `refineries_yields` and `refineries_audits` at runtime to derive empirical multipliers per ore per station per method, **or**
2. Ship the 1/2/3 relative ratings as ordinal hints only and let users rank methods qualitatively.

Do **not** hardcode historic multipliers (e.g. Dinyx yield = 1.0, Ferron = 0.9759) in a 4.7 calculator — they are unverified and likely stale.

### 4.7‑specific formula changes

MMOPIXEL *4.7 New Mole Guide* reports two material‑refinery changes that indirectly affect the math:
- **Ore volume density reduced.** More ore needed to fill the same ship cargo → longer runs per refinery job.
- **Refinery takes a bigger cut.** Refined output per input has decreased relative to pre‑4.7.
- Source: https://www.mmopixel.com/news/star-citizen-4-7-new-mole-guide

Neither change has a published numeric factor. `RefineryService` built against pre‑4.7 baselines will systematically **overestimate yield** in 4.7 — plan for a calibration pass using UEX `refineries_audits` where `game_version` = `4.7.x`.

---

## 4. Refinery Stations in 4.7

UEX lists **20 stations** serving as refineries across Stanton, Pyro, and Nyx, with typical workload ("time in queue") measured in days. Pyro Gateway and Ruin Station refinery decks are **confirmed live** in 4.7.

Source: https://uexcorp.space/mining/refineries (table columns: Refinery name, Region, Workload %, 19 commodity yield bonus columns, Processing time in days)

| Station | System | Typical workload (days) |
|---|---|---|
| ARC‑L1 | Stanton | 9 |
| ARC‑L2 | Stanton | 9 |
| ARC‑L4 | Stanton | 9 |
| CRU‑L1 | Stanton | 9 |
| HUR‑L1 (Green Glade) | Stanton | 9 |
| HUR‑L2 (Faithful Dream) | Stanton | 9 |
| MIC‑L1 (Shallow Frontier) | Stanton | 9 |
| MIC‑L2 (Long Forest) | Stanton | 9 |
| MIC‑L5 | Stanton | 9 |
| Nyx Gateway (Stanton) | Stanton | 9 |
| **Pyro Gateway (Stanton)** | Stanton | 9 |
| Terra Gateway (Stanton) | Stanton | 8 |
| Checkmate | Pyro | 7 |
| Orbituary | Pyro | 7 |
| **Ruin Station** | Pyro | 7 |
| Stanton Gateway (Pyro) | Pyro | 7 |
| Nyx Gateway (Pyro) | Pyro | 6 |
| Pyro Gateway (Nyx) | Nyx | 6 |
| Stanton Gateway (Nyx) | Nyx | 6 |
| Levski | Nyx | 10 |

**Pyro Gateway and Ruin Station refinery decks confirmed live in 4.7.**
- Pyro Gateway: https://uexcorp.space/terminals/info/name/pyro-gateway-refining-processing/
- Ruin Station: https://starcitizen.tools/Ruin_Station and https://finder.cstone.space/Location1/Pyro%20-%20Terminus%20-%20Ruin%20Station%20-%20Hangar%20Transit%20-%20Refinery%20-%20Shops%20-%20Equipment%20-%20Supplies
- Levski (Nyx) confirmed as primary Nyx refinery hub: https://starcitizen.tools/Levski

**Method sets per station:** UEX's published refinery table **does not** surface which of the 9 methods each station offers — it shows commodity yield bonuses and workload only. Community statement: "All refinery decks offer all methods; stations differ only by per‑commodity yield bonus and workload." ⚠️ Verify — no primary source explicitly states this; don't hard‑code "method X unavailable at station Y" rules without API confirmation.

Source for station naming: https://starcitizen.tools/Refinery_Deck and https://starcitizen.fandom.com/wiki/Refinery_deck (fandom URL 403'd on fetch but surfaced in search results).

---

## 5. UEX API — Refinery Endpoints

Base URL: `https://api.uexcorp.space/2.0/`
Auth: Bearer Token (get via "My Apps" on uexcorp.space). Most refinery GET endpoints are unauthenticated; `user_refineries_jobs_*` require auth.
Rate limits: **172,800 requests/day = 120 req/min.** Cache TTL on most endpoints: **+1 day.** Max 500 rows/response.
Source: https://uexcorp.space/api/documentation/

### 5.1 `GET /refineries_methods`
Lists the 9 methods with qualitative ratings.

```bash
curl -X GET "https://api.uexcorp.space/2.0/refineries_methods"
```

Response fields: `id` (int), `name` (varchar 255), `code` (varchar 255), `rating_yield` (1=low, 2=medium, 3=high), `rating_cost` (same scale), `rating_speed` (1=slow, 2=medium, 3=fast), `date_added`, `date_modified`.
Source: https://uexcorp.space/api/documentation/id/get_refineries_methods/

### 5.2 `GET /refineries_capacities`
Per‑station workload / capacity percentages. This is your **queue‑time** proxy.

```bash
curl -X GET "https://api.uexcorp.space/2.0/refineries_capacities"
```

Key fields: `id_terminal`, `id_star_system`, `id_space_station`, `value` (current % capacity), `value_week` (7‑day avg), `value_month` (30‑day avg), `terminal_name`, `star_system_name`, `space_station_name`. Update frequency: monthly at minimum.
Source: https://uexcorp.space/api/documentation/id/get_refineries_capacities/

### 5.3 `GET /refineries_yields`
Per‑commodity per‑station yield bonus/penalty as a percentage integer.

```json
[
  {
    "id": 1, "id_commodity": 5, "id_star_system": 2, "id_terminal": 25,
    "value": 15, "value_week": 14, "value_month": 13,
    "commodity_name": "Aluminum", "terminal_name": "Trade Terminal",
    "star_system_name": "Stanton", "space_station_name": "Orison"
  }
]
```

```bash
curl -X GET "https://api.uexcorp.space/2.0/refineries_yields"
```

Use `value_month` in `RefineryService` to pick "best station for ore X"; use `value` for a real‑time pick.
Source: https://uexcorp.space/api/documentation/id/get_refineries_yields/

### 5.4 `GET /refineries_audits`
**This is the gold‑standard data source** — every entry is a real work order submitted by a data runner, with the full refining outcome.

```bash
curl -X GET "https://api.uexcorp.space/2.0/refineries_audits"
```

Key fields: `id_commodity`, `id_terminal`, `yield` (bonus %), `capacity` (% at time of job), `method` (int, references `refineries_methods.id`), `quantity` (input units), `quantity_yield` (refined units out), `quantity_inert` (waste units out), `total_cost` (UEC), `total_time` (minutes), `game_version` (e.g. `"4.7.1"`), `datarunner`, `commodity_name`.

**This is how you recover numeric multipliers for 4.7:** filter by `game_version LIKE '4.7%'`, group by `method` × `id_commodity`, and compute empirical mean yield/cost/time per unit. Use that in place of hardcoded multipliers.

Example JSON (from UEX docs):
```json
{
  "data": [{
    "id": 12345, "id_commodity": 5, "yield": 95, "capacity": 85, "method": 2,
    "quantity": 100, "quantity_yield": 95, "quantity_inert": 5,
    "total_cost": 50000, "total_time": 45,
    "game_version": "4.7.1", "datarunner": "PlayerName123",
    "commodity_name": "Quantanium", "terminal_name": "Refinery Terminal 1"
  }],
  "status": "ok"
}
```
Source: https://uexcorp.space/api/documentation/id/get_refineries_audits/

### 5.5 Write endpoints (authenticated)
- `POST /user_refineries_jobs_add` — record a new refinery job for a user
- `DELETE /user_refineries_jobs_remove` — delete one
- Both require Bearer token.
Source: https://uexcorp.space/api/documentation/

### 5.6 Adjacent endpoints you'll want
- `GET /commodities` — commodity master list (to resolve `id_commodity` → name)
- `GET /commodities_prices` — current raw/refined prices for ROI math
- `GET /commodities_raw_prices` — raw ore prices specifically
- `GET /terminals` — resolve `id_terminal` → station/terminal name
- `GET /data_parameters` — enum values (incl. method codes)
Source: https://uexcorp.space/api/documentation/

---

## 6. Gotchas for Implementation

### 6.1 Things that changed in 4.7 that will break a pre‑4.7 calculator

1. **Quality score attached to every ore instance.** Pre‑4.7 data models stored ore as `(commodityId, quantity)`. In 4.7 you need `(commodityId, quantity, quality ∈ [0,1000])` everywhere — inventory, cargo, refinery input/output. Refused stacks of the same commodity but different quality are a UX pitfall.
   - Source: https://www.starzen.space/t/star-citizen-shadow-patch-notes-4-7-crafting-and-mining-hidden-changes/35594
2. **Stacks split by quality.** Refined output of a single job will produce **N separate stacks**, one per distinct input quality bucket. Your UI and CSV export must iterate quality bands.
   - Source: MMOPIXEL future‑refining article; starzen.space shadow notes.
3. **Ore density reduced.** Same SCU fills up with less refined material. Trip planning formulas using SCU ↔ units conversions from 4.6 will be off.
   - Source: https://www.mmopixel.com/news/star-citizen-4-7-new-mole-guide
4. **Refinery takes a bigger cut.** Expected yield per input ore has dropped vs 4.6. Re‑calibrate from `refineries_audits` rows where `game_version LIKE '4.7%'`.
   - Source: MMOPIXEL 4.7 mining guide search snippet; corroborated by community discussion.
5. **Quantanium volatility removed (PTU/some builds).** Scoop timer and volatility indicator were removed in 4.7 PTU. If `RefineryService` had special‑case logic for Quantanium timers or risk, that code path is effectively dead.
   - Source: https://www.starzen.space/t/star-citizen-shadow-patch-notes-4-7-crafting-and-mining-hidden-changes/35594
   - ⚠️ Verify LIVE state — the removal was documented in PTU. Check current LIVE build before deleting code.
6. **New commodity: Lindinium** (and possibly others). Add to commodity master.
   - Source: https://www.mmopixel.com/news/star-citizen-4-7-complete-mining-guide

### 6.2 Edge cases

- **Gems don't refine.** Hadanite, Dolivine and similar gems are sold or used as‑is. Your refinery UI must reject them or redirect to a commodity terminal. Source: TEST Squadron refinery guide.
- **Inert materials are always discarded.** They appear in `refineries_audits.quantity_inert` but have no sale value from refinery output. Do not include in ROI.
  - Source: https://starcitizen.tools/Inert_materials (snippet), https://uexcorp.space/commodities/info/name/inert-materials/
- **Refinery kiosk bug:** a work order started from a ship‑stored resource can fail silently (quote section disappears, job never starts). Workaround: transfer ore pods to local inventory first.
  - Source: https://www.mmopixel.com/news/problems-with-star-citizen-4-7-crafting (via search)
- **People's Service Station Theta (Nyx) is NOT a refinery.** Only Levski is. Do not list Theta as a refining destination.
  - Source: https://starcitizen.tools/Levski (snippet via search)
- **Method availability per station:** no public source confirms method‑per‑station restrictions. Assume all 9 methods available at every refinery until contradicted by UEX data.
- **Commodity code QUAN vs spelling:** the raw ore is often spelled "Quantainium" in guides but the UEX/game code is `QUAN` and the refined output is `Quantanium`. Watch spelling in lookups.

### 6.3 Known bugs as of April 2026 to code defensively against

1. **Refinery kiosk quote disappears** when ore is on a stored ship — fallback: require local inventory before quoting.
2. **Quality‑split stacks cannot be manually merged.** Each quality tier is a separate stack. UI must show them that way; "merge" actions will be rejected by the game.
3. **UEX `refineries_audits` may contain mixed‑version rows.** Always filter `game_version` before computing empirical multipliers.
4. **Cargo elevator issues** (general 4.7 issue, not refinery‑specific) can trap refined output. Known issue per MMOPIXEL. Not a `RefineryService` concern but worth surfacing to users if you display job completion status.

---

## 7. Research Holes (be explicit about what's NOT known)

- **Exact 0–1000 quality formula.** No source reveals the computation. Treat as opaque.
- **Exact numeric yield/cost/time multipliers per method.** No authoritative table exists for 4.7. Derive empirically from `refineries_audits`.
- **Per‑ore byproduct pairings.** The only concrete example publicly discussed is iron + carbon → steel, and that's the **future** system, not 4.7. Do not populate byproduct data yet.
- **Method set per station.** Unconfirmed whether some stations lack some methods.
- **Whether quality feeds into yield %, time, or cost in 4.7.** Current evidence says no, but CIG has not explicitly documented it.
- Several starcitizen.tools and mmopixel.com pages returned HTTP 403 to `WebFetch` on 2026‑04‑21; data was recovered from search snippets instead. Before shipping, re‑verify rows marked ⚠️ directly via a browser or a different fetcher.

---

## 8. Recommended C# Data Model (informational, not prescriptive)

```csharp
public sealed record RawOre(int CommodityId, string Code, string Name);

public sealed record OreStack(RawOre Ore, int Quantity, int Quality /* 0..1000 */);

public sealed record RefineryMethod(
    int Id, string Name, string Code,
    int RatingYield, int RatingCost, int RatingSpeed);

public sealed record RefineryStation(
    int TerminalId, string Name, string SystemName,
    double WorkloadDays);

// Empirical multipliers derived at runtime from /refineries_audits
public sealed record MethodMetrics(
    int MethodId, int CommodityId, int TerminalId,
    double MeanYieldFrac,      // quantity_yield / quantity
    double MeanCostPerUnit,    // total_cost / quantity
    double MeanMinutesPerUnit);// total_time / quantity

public sealed record RefineryQuote(
    RefineryStation Station, RefineryMethod Method, OreStack Input,
    int ExpectedYieldUnits, int Quality /* == Input.Quality */,
    long CostUec, int MinutesToComplete,
    RawOre? SecondaryByproduct = null /* null in 4.7 */);
```

Key invariants:
- `RefineryQuote.Quality == Input.Quality` (1:1 passthrough).
- `SecondaryByproduct` is reserved; must be `null` in 4.7 builds.
- Any yield/cost/time multiplier used should come from `MethodMetrics` computed from UEX audits, not hardcoded.

---

## 9. Primary Source Index

- **UEX API documentation:** https://uexcorp.space/api/documentation/
- **UEX refineries methods page:** https://uexcorp.space/mining/methods
- **UEX refineries stations page:** https://uexcorp.space/mining/refineries
- **UEX home / version tracker (4.7.1 LIVE):** https://uexcorp.space/
- **Star Citizen Wiki — Refining:** https://starcitizen.tools/Refining  *(body 403'd 2026‑04‑21; re‑verify)*
- **Star Citizen Wiki — Update 4.7.0:** https://starcitizen.tools/Update:Star_Citizen_Alpha_4.7.0  *(body 403'd)*
- **Star Citizen Wiki — Ore / Mineral resources:** https://starcitizen.tools/Ore , https://starcitizen.tools/Mineral_resources *(body 403'd)*
- **MMOPIXEL — Crafting Future, 4.7 and Beyond:** https://www.mmopixel.com/news/star-citizen-crafting-future-how-refining-works-in-star-citizen-4-7-and-beyond
- **MMOPIXEL — Mining & Material Quality Changes after 4.7:** https://www.mmopixel.com/news/mining-and-material-quality-changes-after-star-citizen-4-7
- **MMOPIXEL — 4.7 Complete Mining Guide:** https://www.mmopixel.com/news/star-citizen-4-7-complete-mining-guide
- **MMOPIXEL — 4.7 New Mole Guide (density/cut):** https://www.mmopixel.com/news/star-citizen-4-7-new-mole-guide
- **MMOPIXEL — Problems with 4.7 Crafting (kiosk bug, stacking):** https://www.mmopixel.com/news/problems-with-star-citizen-4-7-crafting
- **ggwtb — 4.7 Mining Guide (quality thresholds):** https://ggwtb.com/blog/star-citizen-4-7-mining-guide-best-ships--builds--quality-system-profit-tips
- **StarZen — 4.7 Shadow Patch Notes (hidden changes):** https://www.starzen.space/t/star-citizen-shadow-patch-notes-4-7-crafting-and-mining-hidden-changes/35594
- **The Impound — 4.7 Patch Report (Breaker Stations, Nyx):** https://theimpound.com/blogs/star-citizen-news/inside-star-citizen-alpha-4-7-patch-report
- **RSI — Alpha 4.7 patch notes:** https://robertsspaceindustries.com/en/comm-link/Patch-Notes/21070-Star-Citizen-Alpha-47  *(body 403'd)*
- **RSI Support — 4.7 Known Issues:** https://support.robertsspaceindustries.com/hc/en-us/articles/360056254754-Star-Citizen-Alpha-4-7-Known-Issues  *(body 403'd)*
- **TEST Squadron Refinery Guide (historical context):** https://testsquadron.com/threads/the-test-refinery-deck-refinery-guide-v3-12.18413/
- **SC Mining Mom — Refining page:** https://www.scminingmom.com/post-mining/refining  *(body was empty / required JS)*
- **Regolith Rocks mining tool:** https://regolith.rocks/tables/refinery , https://regolith.rocks/workorder  *(body required JS)*

---

*End R1.*
