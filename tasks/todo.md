# Golem Mining Suite — Full Improvement Plan (2026-04-21)

**Branch:** `feature/audit-and-4.7-refresh`
**Context:** Combines code audit findings + SC 4.7 research + Regolith-shutdown (June 1, 2026) migration play.

## ⚠️ User actions required (cannot be automated)

- [ ] **Rotate Supabase anon key** at supabase.com → project settings → API → regenerate anon key
- [ ] **Rotate UEX Corp API key** at uexcorp.space → account settings → API keys → revoke + regenerate
- [ ] Decide crew-session backend: extend Supabase (recommended, already integrated) vs. SignalR vs. P2P
- [ ] Create Discord OAuth app at discord.com/developers/applications (needed for crew sessions)

These are the only items I can't do for you. Everything else below, I'm executing.

## Wave 1 — Research ✅ DONE (2026-04-21)

- [x] **R1** → `tasks/research/R1-refinery-4.7.md` — **Headline: byproduct system is NOT live in 4.7.** 4.7 is still 1:1 refinery with a new 0-1000 quality tag bolted on. Shipping the pre-populated byproduct pairings (iron+carbon=steel etc.) would produce **wrong** answers. Scope for Wave 4 narrowed.
- [x] **R2** → `tasks/research/R2-game-log-mining.md` — **Headline: mining events are NOT in Game.log.** No rock scan, fracture, or cargo events since 4.0.2 (CIG logs only client-player-involved events). GameLogService scope narrowed to: session bracketing, QT, death, 30k proxy, ship identity. **OCR remains mandatory for mining data** — Tesseract stays.
- [x] **R3** → `tasks/research/R3-regolith-schema.md` — **Good news: GraphQL schema is open-source MIT at github.com/RegolithCo/RegolithCo-Common.** No bulk-export tool from Regolith team. Users download per-session JSON. Importer supports both file drops and x-api-key API pull.
- [x] **R4** → `tasks/research/R4-pyro-qt-geometry.md` — **Caveat: exact Pyro coords not publicly scrapeable at precision.** Snareplan math is public (point-to-line-segment, 20 km radius). Pyro stations at Lagrange anchors documented. Must crowdsource pull-point coords via existing Supabase backend.

## Wave 2 — Code hardening (single implementer agent, sequential edits)

From the prior audit. All non-feature work to stabilize before building new things.

- [ ] Strip real secrets from `appsettings.json`; replace with `appsettings.Example.json` + env-var / user-secrets loader in `App.xaml.cs`
- [ ] `IHttpClientFactory` + typed clients for PriceService, UEXService, UpdateChecker, AutoUpdater
- [ ] Introduce `ISupabaseService` interface; fix `is PriceService ps` cast in `App.xaml.cs:113`
- [ ] Remove all empty `catch {}` blocks; inject `ILogger<T>` everywhere
- [ ] Convert `async void` UI handlers → `async Task` via `[RelayCommand]`
- [ ] Swap `AsteroidLocationLoader` naive split for `CsvHelper`
- [ ] Delete `debug_etam_loop.txt`, `debug_uex.ps1`, `gitignore.txt` from project folder
- [ ] `.csproj` cleanup: dedupe `Golem.ico`, add `<TreatWarningsAsErrors>`, add analyzer packages
- [ ] Move Serilog to DI-only (drop static `Log.Logger` usage)
- [ ] Fail-loud DI: `GetRequiredService` instead of `GetService` in App startup

## Wave 3 — New foundations (parallel subagents, disjoint new files)

- [ ] `Services/GameLogService.cs` — watch `Game.log`, emit mining events (rock scan, fracture, refinery complete, cargo spawn). Replaces OCR for mining flow.
- [ ] `Services/FractureSolver.cs` + `Models/RockScan.cs` — given mass/instability/resistance/composition, returns safe laser charge band + recommended head
- [ ] `Services/SkipRockPredictor.cs` — probability that a rock is profitably crackable given the scan
- [ ] `Tests/Golem.Mining.Suite.Tests.csproj` — xUnit scaffold with first tests for RefineryService, RouteOptimizerService, TerminalParser

## Wave 4 — Refinery 4.7 rewrite (depends on R1)

- [ ] `Models/Refinery47.cs` — byproduct graph, quality score, updated station list incl. Pyro Gateway + Ruin
- [ ] Rewrite `RefineryService` around the new model (keep legacy API for back-compat via adapter)
- [ ] Update `RefineryViewModel` + `RefineryCalculatorWindow.xaml` to show primary + secondary output, quality badge (< 500 warn / > 700 keeper)
- [ ] Live refinery-workload comparator (UEX `/commodities_prices_history` + workload endpoints)

## Wave 5 — Regolith migration suite (depends on R3)

- [ ] `Services/RegolithImporter.cs` — parse Regolith session JSON export → Golem wallet + logbook
- [ ] `Services/CrewSessionService.cs` — session model, per-member contribution tracking, aUEC split
- [ ] `ViewModels/CrewSessionViewModel.cs` + new `Views/CrewSessionView.xaml`
- [ ] Desktop toast notifications for refinery pickup timers (WinRT ToastNotificationManager)
- [ ] "Import from Regolith" button wired to `RegolithImporter`

## Wave 6 — Piracy QT + polish (depends on R4)

- [ ] `Services/PiracyRouteAnalyzer.cs` — given QT path, flag known pull-point proximity
- [ ] `RouteOptimizerService` integration — optional risk-weighted routing
- [ ] UI toggle: "Highlight piracy risk" on route list

## Wave 7 — Verification

- [ ] `dotnet build` Debug + Release both pass
- [ ] `dotnet test` — all tests green
- [ ] Manual smoke: launch app, open every top-level view, no crashes
- [ ] CI upgrade: `.github/workflows/ci.yml` runs `dotnet test` + `dotnet format --verify-no-changes`
- [ ] Bump `<Version>` in `.csproj` to `1.4.0-beta`
- [ ] Update `CHANGELOG.md`

## Out of scope (explicit non-goals this pass)

- Discord OAuth wiring (needs user to create Discord app first)
- Multi-crew realtime sync (needs backend decision first)
- Voice/TTS output
- Mobile port
- Org fleet dashboard

## Review section (to be filled at end)

_Populated after Wave 7._
