# MS Always Open (Megastore Simulator)

Keep your store running without the default night shutdown flow.

## Features

- **24/7 mode**: Store is always considered open.
- **Custom hours mode**: Define your own open/close hours (`0-23`) including overnight windows (example: `23 -> 6`).
- **No night warning popup**: Suppresses the *"No new customers will come at night"* message.
- **Overnight time progression**: Time continues through night instead of skipping (`22:00 -> 23:00 -> 00:00 ... -> 06:00`).
- **Auto new-day handoff**: New day starts at `06:00` in modded modes.

## Requirements

- **BepInEx 5.x** installed for Megastore Simulator.
- Game path used by this build:
  - `G:\SteamLibrary\steamapps\common\Megastore Simulator`

## Installation

1. Install BepInEx into your Megastore Simulator folder.
2. Copy:
   - `MS_AlwaysOpen.dll`
3. To:
   - `...\Megastore Simulator\BepInEx\plugins\MS_AlwaysOpen\MS_AlwaysOpen.dll`
4. Launch the game once to generate config.

## Configuration

Config file:

- `...\Megastore Simulator\BepInEx\config\com.gamea.megastore.alwaysopen.cfg`

Options:

- `AlwaysOpen24x7 = true`
  - Ignores normal store-hour restrictions.
- `UseCustomHours = false`
  - When `true` (and 24/7 is off), uses custom schedule.
- `OpeningHour = 6`
- `ClosingHour = 22`

### Example presets

**True 24/7**

- `AlwaysOpen24x7 = true`

**Custom overnight schedule (23:00 to 06:00)**

- `AlwaysOpen24x7 = false`
- `UseCustomHours = true`
- `OpeningHour = 23`
- `ClosingHour = 6`

## Compatibility

- Should be compatible with most mods that do not patch the same `TimeManager`, `TooltipUI`, or `OpenCloseLabel` methods.
- If another mod changes customer spawn timing or day-end flow, load-order conflicts are possible.

## Troubleshooting

- If nothing changes, fully restart the game after updating DLL/config.
- Check BepInEx log for plugin load line:
  - `MS Always Open loaded...`
- If config does not exist, launch once and then close the game.

## Uninstall

- Remove:
  - `...\BepInEx\plugins\MS_AlwaysOpen\MS_AlwaysOpen.dll`
- Optional: remove config file from `...\BepInEx\config`.

## Changelog

### 1.0.1

- Reworked overnight progression to use the game day routine patch path, preventing shift/day desync.
- Preserved normal day lifecycle signaling on automatic new-day handoff.
- Fixed cashier transient state carry-over that could break late-shift cashiers after the first day.

### 1.0.0

- Initial release.
- Added 24/7 mode.
- Added custom hour windows (including overnight).
- Suppressed night shutdown tooltip.
- Added overnight time progression and auto new-day transition at 06:00.
