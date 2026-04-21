# Golem Mining Suite

![Build Status](https://github.com/ErskeN1337/Golem-Mining-Suite/actions/workflows/release.yml/badge.svg)

**Golem Mining Suite** is a comprehensive tool for Star Citizen miners, designed to optimize your mining operations and maximize profits. Whether you prefer surface mining, asteroid belts, or ground vehicle (ROC) mining, this suite provides the data you need.

## 🆕 Latest Update (v1.4.0-beta)
**The Audit & 4.7 Refresh Update**
- **💎 4.7 Quality Score (0-1000)**: Refinery Calculator now reads the new SC 4.7 mining quality stat. Tier badges (Debuff → Endgame) and an Effective Value row show how quality swings your payout.
- **🔥 New Pyro Refineries**: Pyro Gateway, Ruin Station, and Terra Gateway added alongside the existing Stanton refinery stations.
- **📦 Regolith Migration**: Import your Regolith Co. sessions before their June 1, 2026 shutdown — works via file-drop JSON *or* live API key pull.
- **👥 Crew Sessions**: New local crew-session browser with aUEC share tracking. Set your User Handle in Settings.
- **🔔 Desktop Toasts**: Get notified the moment your refinery order is ready.
- **🏴‍☠️ Piracy Route Risk**: Optional checkbox in the Route Optimizer that scores each leg against known Pyro piracy hotspots.
- **📝 Game.log Tailing**: Session / QT / death / stall / disconnect detection running quietly in the background — foundation for future overlays.
- **🛡️ Hardening**: Full .NET 8 polish — IHttpClientFactory, ILogger<T>, ISupabaseService, CsvHelper, TreatWarningsAsErrors, 130+ xUnit tests, secrets off disk.

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

## 💡 About Me

I’m completely new to coding, and this project is my first big endeavor. It’s been a huge learning experience, and I’m excited to continue improving the suite while making it useful for the Star Citizen community.

**Enjoying the tool?**
[☕ Buy me a coffee](https://buymeacoffee.com/ericamcoff)

## 📜 License

This project is licensed under the MIT License - see the LICENSE file for details.

---
*Built with ❤️ for the Star Citizen Community*


![image alt](https://github.com/ErskeN1337/Golem-Mining-Suite/blob/47661279c46602aef7fb3b715f76e93b34229077/Menu.png)
