# Golem Mining Suite

![Build Status](https://github.com/ErskeN1337/Golem-Mining-Suite/actions/workflows/release.yml/badge.svg)

**Golem Mining Suite** is a comprehensive tool for Star Citizen miners, designed to optimize your mining operations and maximize profits. Whether you prefer surface mining, asteroid belts, or ground vehicle (ROC) mining, this suite provides the data you need.

> **Original project & maintainer:** [**ErskeN1337**](https://github.com/ErskeN1337) — creator of Golem Mining Suite and the driving force behind every release through v1.3.0.
> If this tool helps you, please [☕ buy them a coffee](https://buymeacoffee.com/ericamcoff). The v1.4.0-beta contribution below is a community PR — Erske's vision, design, and codebase made it possible.

## 🆕 Latest Update — v1.4.0-beta · "Audit & 4.7 Refresh"

A community contribution focused on patch 4.7 readiness, a time-boxed Regolith migration play, and a top-to-bottom code-hardening pass. Full changelog in [`Golem Mining Suite/CHANGELOG.md`](Golem%20Mining%20Suite/CHANGELOG.md).

### ⛏️ New player-facing features

| Area | What's new |
|---|---|
| **Refinery Calculator** | 4.7 **Quality Score (0–1000)** input with 5-tier badge (Debuff / Baseline / Good / Keeper / Endgame). New **Effective Value** row shows how quality swings your payout. Pyro Gateway, Ruin Station, and Terra Gateway added to the station list. |
| **Crew Sessions** | New top-menu tile. Local browser of your past crew-mining work with per-member aUEC share tracking. Set your in-game handle in Settings to see your cut. |
| **Regolith Migration** | One-click import of your Regolith Co. sessions before their **June 1, 2026** shutdown — supports both per-session JSON file drop and live `x-api-key` API pull. |
| **Refinery Toasts** | Native Windows desktop notifications fire the moment a refinery order is ready. Survives app restart. |
| **Route Optimizer** | New optional **Piracy Risk** checkbox scores each route leg against known Pyro pull-points using Snareplan-style geometry. Risk column with green/yellow/orange/red badges. |
| **Behind the scenes** | Game.log tailing for session / QT / death / 30k-stall detection (foundation for future overlays — mining events still require OCR). Rock fracture solver + skip-rock predictor available at API level for future UI. |

### 🛡️ Code & security hardening

- Real Supabase + UEX keys removed from source; layered loader reads env vars → `%APPDATA%\Golem Mining Suite\appsettings.json` → shipped placeholder file. `appsettings.Example.json` ships as the template.
- `IHttpClientFactory` with five named clients replaces every `new HttpClient(...)`. `ISupabaseService` interface introduced.
- 16 empty `catch {}` blocks replaced with `ILogger<T>`. Five `async void` methods reduced to one (the XAML-wired handler, now with try/catch).
- `CsvHelper` replaces naive `.Split(',')` for asteroid-location parsing.
- `<TreatWarningsAsErrors>` + `Microsoft.CodeAnalysis.NetAnalyzers` enforced; pre-existing CS8618 warnings fixed.

### 🧪 Test suite

New `Tests/Golem.Mining.Suite.Tests` xUnit project with **130 passing tests** covering:
RefineryService (pre- and post-4.7), RouteOptimizer, TerminalParser, QualityScore boundaries, FractureSolver geometry, SkipRockPredictor heuristics, GameLog regex patterns, RegolithImporter parsing, CrewSessionService persistence, RefineryOrderWatcher timers, PiracyRouteAnalyzer point-to-segment math.

### 🛠️ Build / CI

- Target framework bumped to `net8.0-windows10.0.17763.0` (Windows 10 1809 floor — required for native toast).
- CI runs `restore → build Release → test Release → dotnet format --verify-no-changes` on `windows-latest`.
- Version bumped 1.2.9 → 1.4.0-beta (`AssemblyVersion`/`FileVersion` 1.4.0.0).

## 🌟 Features

- **💎 Surface Mining**: Locate the best planetary deposits for high-value minerals.
- **☄️ Asteroid Mining**: Navigate the belts with precision, knowing exactly where to find specific ores.
- **🪨 ROC Mining**: Find the best spots for gem mining with ground vehicles.
- **💰 Market Prices**: Real-time trading data to help you sell at the best price.
- **🗺️ Route Optimizer**: Find the most profitable trade routes with customizable filters.
- **💰 Wallet & Logbook**: Track your finances in-game with a persistent transaction history.
- **🧮 Calculator Suite**: Calculate cargo value and refinery yields.
- **⚙️ Custom Settings**: Tailor the app to your playstyle with themes and transparency control.
- **🎨 Modern Dark UI**: A sleek, easy-to-read interface designed for low-light gaming environments.

## 🚀 Installation

1. Go to the [Releases](https://github.com/ErskeN1337/Golem-Mining-Suite/releases) page.
2. Download the latest `Golem-Mining-Suite-vX.X.X.zip`.
3. Extract the contents to a folder of your choice.
4. Run `Golem Mining Suite.exe`.

*Note: This application requires .NET 8.0 Desktop Runtime.*

## 🔐 Verification

We take security seriously. All releases are built using GitHub Actions and signed with GitHub Build Provenance.

To verify a release:
1. Download the `.zip` file.
2. Install the [GitHub CLI](https://cli.github.com/).
3. Run the following command:
   ```bash
   gh attestation verify Golem-Mining-Suite-v1.1.6.zip --owner ErskeN1337
   ```

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## 💡 About the Project

Golem Mining Suite was created by [**ErskeN1337**](https://github.com/ErskeN1337) as their first major coding project — a Star Citizen mining companion built from scratch and shipped through v1.3.0. The codebase, vision, and Star Citizen domain knowledge that make this app useful are all theirs.

**Enjoying the tool?**
[☕ Buy ErskeN1337 a coffee](https://buymeacoffee.com/ericamcoff)

## 🙌 Credits

- **Original creator & lead maintainer**: [ErskeN1337](https://github.com/ErskeN1337)
- **v1.4.0-beta contribution**: [justinrmcgowan](https://github.com/justinrmcgowan) — 4.7 refinery quality, Regolith migration suite, piracy route analyzer, GameLog tailer, .NET 8 hardening, test suite, CI upgrades.
- Built on community data from [UEX Corp](https://uexcorp.space), [Star Citizen Wiki](https://starcitizen.tools), and the late great [Regolith Co.](https://regolith.rocks) (RIP June 1, 2026).
- Want on this list? PRs welcome — see Contributing above.

## 📜 License

This project is licensed under the MIT License - see the LICENSE file for details.

---
*Built with ❤️ for the Star Citizen Community*


![image alt](https://github.com/ErskeN1337/Golem-Mining-Suite/blob/47661279c46602aef7fb3b715f76e93b34229077/Menu.png)
