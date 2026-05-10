# Project: WPF-LoginForm (Dashboard Application)

## Stack
- **Language:** C# (.NET Framework, WPF)
- **Charts:** LiveCharts.Wpf 0.9.7
- **Serialization:** Newtonsoft.Json
- **Database:** Npgsql (PostgreSQL), Microsoft.Data.SqlClient
- **Excel:** EPPlus, ClosedXML
- **UI Icons:** FontAwesome.Sharp, FontAwesome.WPF
- **Toolkit:** Extended.Wpf.Toolkit (Xceed)

## Project Structure
```
WPF-LoginForm/
├── Models/           # Data models (DashboardDataPoint, SeriesConfiguration, etc.)
├── ViewModels/       # MVVM ViewModels (HomeViewModel, ConfigurationViewModel, etc.)
├── Views/            # XAML views (HomeView, ConfigurationView, CustomChartTooltip, etc.)
├── Services/         # Business logic (DashboardChartService, SmartLabelService, etc.)
├── Converters/       # XAML value converters
├── Properties/       # Resources (localized strings)
```

## Key Files
| File | Purpose |
|------|---------|
| `HomeViewModel.cs` | Main dashboard logic: chart loading, color management, commands |
| `DashboardChartService.cs` | Processes raw data into chart-ready format, color assignment |
| `DashboardDataPoint.cs` | Data point model with Label + 3-part tooltip properties |
| `SmartLabelService.cs` | Collision-avoiding label placement algorithm |
| `SeriesConfiguration.cs` | Per-series config: ColumnName, CustomDetailTitle, SeriesColorHex |
| `DashboardConfiguration.cs` | Per-chart config: ChartPosition, Series list, DataStructureType |
| `CustomChartTooltip.xaml` | Tooltip template for CartesianCharts |
| `HomeView.xaml` | Dashboard page layout (5+1 charts) |
| `run.ps1` | Build & run script |

## Color System
- `_colorMap`: auto-generated/cached colors `Dictionary<(int ChartPosition, string SeriesKey), string>`
- `_userColorMap`: user-chosen colors (loaded from `SeriesColorHex`), takes priority
- `GetColor(title, seriesTitle)` lookup order: userColorMap[title] → userColorMap[seriesTitle] → colorMap[title] → colorMap[seriesTitle] + variation → random
- Color map keys always use `ser.ColumnName` (NOT `CustomDetailTitle`)

## Workflow Rules (must follow)
1. **Do not change more than 3 files per task.**
2. **Make a plan first** — outline approach before writing code.
3. **After 4+ distinct changes, remind user to update the repository.**

## Build
```
.\run.ps1 -Configuration Debug -Restore -Run
```
- Script auto-kills any running `WPF-LoginForm.exe` before building.
- MSBuild path: `C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe`

## Common Issues
- **MSB3027 / file locked:** App is still running. The `run.ps1` script handles this automatically now.
- **NuGet restore failures:** Run `.\run.ps1 -Restore` separately.
- **Pre-existing build errors:** This project has pre-existing NuGet/architecture issues unrelated to feature changes. Check `$LASTEXITCODE` for actual error cause.

## Coding Conventions
- Use `ConcurrentDictionary` for thread-safe caches.
- WPF bindings use MVVM with `INotifyPropertyChanged`.
- Chart data flows through `ProcessChartData()` → `ChartResultDto` → UI binding.
- Labels go through `CleanLabelString()` which replaces `_` with space.
- New string properties in `DashboardDataPoint` automatically replace `_` with space in setter.
- Match existing code style (naming, braces, patterns).
- Prefer editing existing files over creating new ones unless necessary.

## Suggestions
- Use `task` agent for complex multi-step research (e.g., "find all color-related code paths").
- Use `grep` + `glob` for code search instead of browsing.
- Before editing, `git status` and `git diff` to understand working tree state.
- For risky changes, suggest creating a branch first.
