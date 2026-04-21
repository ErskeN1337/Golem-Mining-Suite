# Manual Smoke Test — v1.4.0-beta

~5 minutes. Run after install to visually validate the Wave 2-6 features. No
real mining required. Stop at the first failure and capture a screenshot + log
line from `%APPDATA%/Golem Mining Suite/logs/`.

## 1. Launch
- [ ] Double-click `Golem Mining Suite.exe` (or `dotnet run` from source).
- [ ] Main menu paints without exceptions; title bar shows `1.4.0-beta`.
- [ ] All pre-existing tiles are present **plus** a new **Crew Sessions** tile.

## 2. Refinery Calculator
- [ ] Open Refinery Calculator from the main menu.
- [ ] **Quality (0-1000)** input is visible; default reads `500`.
- [ ] Tier badge next to Quality reads **Baseline** at 500.
- [ ] Enter a mineral + amount. An **Effective Value** row renders under the
      existing yield row and updates when you change Quality.
- [ ] Change Quality to `900` → badge flips to **Keeper** or **Endgame** and
      Effective Value jumps.
- [ ] Station dropdown includes **Pyro Gateway**, **Ruin Station**, and
      **Terra Gateway**.

## 3. Route Optimizer
- [ ] Open Route Optimizer.
- [ ] **Piracy Risk** checkbox is present (top of the filter panel).
- [ ] Toggle it on → route list re-renders with a Risk column; toggle off →
      column disappears. Neither action throws.

## 4. Crew Sessions
- [ ] Open Crew Sessions from the main menu tile.
- [ ] View loads with an empty-state message (no crash on empty store).

## 5. Settings — User Handle
- [ ] Open Settings.
- [ ] **User Handle** field is present.
- [ ] Enter a value, click Save, reopen Settings → value persists.

## 6. Shutdown
- [ ] Close the app. No background toast; no unhandled-exception popup.
