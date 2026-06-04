# AI Assistant — Lite Documentation

## App Overview
WPF manufacturing downtime tracking app (C#, .NET 4.8, MVVM). Tracks machine stops, breaks, maintenance in a daily timeline with dashboard analytics.

## Key Modules

### Dashboard (HomeView)
- 6 LiveCharts positions (Line/Bar/Pie) showing downtime analytics
- Config saved to `%AppData%\WPF_LoginForm\dashboard_config.json`
- KPI cards below charts
- Date filter + auto-import from folder

### Daily Timeline (DailyTimelineWindow)
- Drag-drop timeline with 6 lanes, 12-hour shift view
- Day shift (08:00-20:00) / Night shift (20:00-08:00)
- Events stored as `HHmm-Duration-MA-Code-Description` in table error columns
- Favorites bar for quick-add
- Edit panel for modifying events

### Raw Data (DatarepView)
- Table viewer/editor with add/delete/save
- Find/replace, column visibility
- Row limit for performance

### Error Management (ErrorManagementView)
- Error drill-down by date range
- Monthly validation with calendar view
- Category mapping rules

### Settings (SettingsView)
- DB connection config, language, auto-import folder
- Offline mode, user management, encryption

### User Management
- Login with roles (admin/user)
- Offline users for disconnected mode

## AI Rules
1. **NEVER modify any file without user confirmation**
2. **Show the exact changes before applying**
3. **Only read tables the user has explicitly allowed** (see AllowedTables in AI settings)
4. **If unsure, ask the user**
5. **Database queries are READ-ONLY — never write to DB**
6. **For config changes: show the diff, then ask user to refresh/restart**
7. **For detailed info, read AI_DOCS_FULL.md**

## Common Config File Locations
- `%AppData%\WPF_LoginForm\general_config.json` — main settings
- `%AppData%\WPF_LoginForm\dashboard_config.json` — dashboard charts
- `%AppData%\WPF_LoginForm\portal_config.json` — portal buttons
- `%AppData%\WPF_LoginForm\timeline_config.json` — timeline favorites
- `{AppDir}\category_rules.json` — error category rules

## Key Settings (GeneralSettings)
- `DbProvider` — "SqlServer" or "Postgres"
- `AppLanguage` — "en-US" or "tr-TR"
- `ConnectionTimeout` — seconds (default 15)
- `PureOfflineMode` — bypasses DB entirely
- `SuppressOfflineReminder` — hides offline badge
- `DefaultRowLimit` — 0 = all rows

## Chart Config (DashboardConfiguration)
- 6 positions (0-5), each with: `IsEnabled`, `ChartType`, `TableName`, column mappings
- Per-series: `SeriesColorHex`, aggregation, node editor tooltips
- Colors stored as hex strings: `#RRGGBB`

## Database Tables (typical)
- Login tables: `Users`, `LoginDb`
- Data tables: user-defined (e.g. `DurusAnalizi`, `HataKayitlari`)
- Table names are configurable per dashboard position
