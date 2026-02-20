# MS_AlwaysOpen

BepInEx mod for **Megastore Simulator** that supports:

- `24/7` store mode (always treated as open)
- Custom open/close hours

## Requirements

- BepInEx 5.x installed in:
	- `G:\SteamLibrary\steamapps\common\Megastore Simulator`
- .NET SDK 8+ (`dotnet --version`)

## Build

From this folder:

```powershell
dotnet build -c Release
```

Output DLL:

- `bin\Release\netstandard2.1\MS_AlwaysOpen.dll`

## Install

Copy the built DLL to:

- `G:\SteamLibrary\steamapps\common\Megastore Simulator\BepInEx\plugins\MS_AlwaysOpen\MS_AlwaysOpen.dll`

Create folder if needed:

```powershell
New-Item -ItemType Directory -Force "G:\SteamLibrary\steamapps\common\Megastore Simulator\BepInEx\plugins\MS_AlwaysOpen"
Copy-Item ".\bin\Release\netstandard2.1\MS_AlwaysOpen.dll" "G:\SteamLibrary\steamapps\common\Megastore Simulator\BepInEx\plugins\MS_AlwaysOpen\MS_AlwaysOpen.dll" -Force
```

## Configuration

After first launch, edit:

- `G:\SteamLibrary\steamapps\common\Megastore Simulator\BepInEx\config\com.gamea.megastore.alwaysopen.cfg`

Settings:

- `AlwaysOpen24x7 = true`
	- Keeps store always open and removes time-based customer spawn limits.
- `UseCustomHours = false`
	- If enabled (and `AlwaysOpen24x7=false`), uses `OpeningHour` and `ClosingHour`.
- `OpeningHour = 6`
- `ClosingHour = 22`
	- Supports overnight ranges, e.g. `22` to `6`.

Behavior in modded modes (`AlwaysOpen24x7=true` or `UseCustomHours=true`):

- Suppresses the night tooltip: `No new customers will come at night`
- Time continues overnight without skipping (`22:00 -> 23:00 -> 00:00 ... -> 06:00`)
- New day starts automatically at `06:00`

### Examples

- **24/7 mode**
	- `AlwaysOpen24x7 = true`
- **Custom 08:00-23:00**
	- `AlwaysOpen24x7 = false`
	- `UseCustomHours = true`
	- `OpeningHour = 8`
	- `ClosingHour = 23`

## Notes

- This mod patches:
	- `OpenCloseLabel` (forces/stabilizes open state in 24/7 mode)
	- `TimeManager.CanSpawnCustomer()`
	- `TimeManager.CanSpawnWanderer()`
	- `TimeManager.DayRoutine()` (extended full-day cycle in modded modes)
	- `TooltipUI.ShowTooltip()` (`end_day_tooltip` suppression)
	- `TimeManager.ShowNextDayUI()` (auto next day)
	- `Cashier.DeactivateInstant()` (resets transient shift flags)

## Changelog

### 1.0.1

- Reworked overnight progression to run through `TimeManager.DayRoutine()` instead of a parallel `Update()` tick.
- Preserved day lifecycle events during auto day rollover to keep employee shifts and customer systems in sync.
- Fixed cashier transient state carry-over that could break late-shift cashiers after day one.

