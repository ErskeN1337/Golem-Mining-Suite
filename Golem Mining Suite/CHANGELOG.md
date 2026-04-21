# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.4.0-beta] - 2026-04-21

Large audit + SC 4.7 refresh release. 15+ commits on `feature/audit-and-4.7-refresh`
rolled into one beta drop covering refinery quality scores, Regolith-shutdown
migration tooling, game.log tailing, piracy-route risk analysis, crew sessions,
desktop toasts, and a full .NET 8 hardening pass.

### Added — product / user-facing

- **Refinery Calculator — 4.7 quality score (0-1000)**: new `Quality` input on
  the calculator. Tier badge (Debuff / Baseline / Good / Keeper / Endgame) and
  an Effective Value row render at default Q=500 and respond live to edits.
- **New Pyro refineries**: Pyro Gateway, Ruin Station, Terra Gateway added to
  the refinery station roster alongside the existing Stanton L-points.
- **Rock fracture solver + skip-rock predictor** (API-level, not yet UI-wired):
  `FractureSolver` computes charge band + head + ship-size recommendation with
  a 4-tier risk score; `SkipRockPredictor` combines composition × risk × price
  heuristics into a skip-probability with reasoning. Foundation for the in-ship
  overlay.
- **Regolith migration suite**: `RegolithImporter` ingests your Regolith Co.
  sessions before the June 1, 2026 shutdown. Supports both per-session JSON
  file-drops and live x-api-key GraphQL pulls (schema source:
  github.com/RegolithCo/RegolithCo-Common, MIT).
- **Crew Sessions view**: local session browser with aUEC share tracking.
  Configure your User Handle in Settings so shares attribute correctly.
- **Desktop toast notifications** for refinery pickup completion via
  `Microsoft.Toolkit.Uwp.Notifications`.
- **Piracy route risk analysis** in the Route Optimizer (opt-in checkbox).
  Seeded with 6 known Pyro piracy hotspots via `Assets/Data/piracy-seed.json`
  and scored with snareplan-compatible point-to-segment geometry.
- **Game.log session tailer**: QT events, player death, 30k/stall proxy,
  disconnect detection, ship-identity pickup. Foundation for future features —
  note mining-specific rock/scan events are NOT in Game.log post-4.0.2, so
  OCR (Tesseract) remains the source of truth for mineral data.

### Changed — under the hood

- **Full .NET 8 hardening**:
  - `IHttpClientFactory` + named/typed clients (PriceService, UEXService,
    UpdateChecker, AutoUpdater).
  - `ILogger<T>` everywhere; 16 empty `catch {}` blocks eliminated.
  - New `ISupabaseService` / `IPriceService` interfaces — runtime casts removed.
  - No `async void` outside XAML-wired event handlers (1 intentional residual
    handler, wrapped in try/catch).
  - `CsvHelper` 33.1.0 replaces the naive CSV split in `AsteroidLocationLoader`.
- **Secrets off disk**: `appsettings.json` is gitignored; `appsettings.Example.json`
  ships as a template. Loader reads env-vars → `%APPDATA%` → example, in order.
- **Test suite**: 130+ passing xUnit tests covering Refinery, RouteOptimizer,
  TerminalParser, GameLog patterns, Piracy geometry, CrewSessions,
  RegolithImporter, QualityScore, and RefineryOrderWatcher.
- **Target framework bumped** from `net8.0-windows` to
  `net8.0-windows10.0.17763.0` (Windows 10 1809 floor) — required by the toast
  notifications API surface.
- **`.csproj` tightening**: `<TreatWarningsAsErrors>` enabled,
  `Microsoft.CodeAnalysis.NetAnalyzers` 10.0.202 wired in,
  `WarningsNotAsErrors` narrowed to CS1591 + CA1416 only.
- **CI**: consolidated into three focused workflows —
  `ci.yml` (PR/master build + test + format), `release.yml` (tag-triggered
  official release), `build.yml` (manual-dispatch dev artifact).

### Known limitations

- **Real Pyro station coordinates are still absent.** The piracy risk column
  reads 0 aUEC until the community crowdsources pull-point coords via the
  existing Supabase backend. Math and UI are wired; data is not.
- **Refinery byproducts** (e.g. iron + carbon = steel) from the CIG future
  design are deliberately NOT implemented. Per Wave 1 research (R1), 4.7 is
  still 1:1 refining with a quality tag bolted on. Shipping the pre-populated
  byproduct pairings today would produce wrong answers.
- **Fracture solver + skip predictor** are API-level only in this beta; no
  overlay UI yet.

### Manual smoke test

See `tasks/SMOKE-TEST.md` for a ~5-minute procedure to visually validate the
new views and controls after install.

## [1.2.4] - 2026-02-11

### Changed
- Bumped version to 1.2.4
- Updated version references in build artifacts and dependency files
- Cleaned up temporary build files with outdated version numbers

## [1.2.3] - 2026-02-10

### Added
- Initial version bump from 1.2.0 to 1.2.3

## [1.2.0] - 2026-02-09

### Added
- Initial version 1.2.0 release
