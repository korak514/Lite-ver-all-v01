# AI Assistant — Full Documentation

## Application Architecture

### Tech Stack
- **Language**: C# (.NET Framework 4.8)
- **UI Pattern**: MVVM (Model-View-ViewModel)
- **Database**: SQL Server or PostgreSQL (configurable)
- **Charting**: LiveCharts.Wpf v0.9.7
- **Serialization**: Newtonsoft.Json v13.0.3
- **Icons**: FontAwesome.WPF v4.7.0
- **Packaging**: Costura.Fody (embedded dependencies)

### Solution Structure
```
WPF-LoginForm/
  Models/           — Data models (POCOs)
  ViewModels/       — MVVM ViewModels + commands
  Views/            — XAML windows + user controls
  Services/         — Business logic, DB, caching
  Repositories/     — Data access layer (IDataRepository)
  Properties/       — Resources.resx (EN/TR), Settings
  Converters/       — XAML value converters
  CustomControls/   — Reusable WPF controls
  Resources/Docs/   — AI documentation (these files)
```

## Configuration System

### GeneralSettings (general_config.json)
Location: `%AppData%\WPF_LoginForm\general_config.json`
Managed by: `GeneralSettingsManager` (singleton)

**Serialized properties:**
| Property | Type | Default | Description |
|---|---|---|---|
| `DbProvider` | string | "SqlServer" | "SqlServer" or "Postgres" |
| `SqlAuthConnString` | string | — | Auth DB connection |
| `SqlDataConnString` | string | — | Data DB connection |
| `PostgresAuthConnString` | string | — | Postgres auth |
| `PostgresDataConnString` | string | — | Postgres data |
| `AppLanguage` | string | "en-US" | UI language |
| `OfflineFolderPath` | string | "" | Offline data dir |
| `AutoImportEnabled` | bool | false | Scan folder for dashboards |
| `ImportIsRelative` | bool | true | Relative to app exe |
| `ImportFileName` | string | "dashboard_config.json" | Default dashboard file |
| `ImportAbsolutePath` | string | "" | Absolute import folder |
| `ConnectionTimeout` | int | 15 | Seconds |
| `TrustServerCertificate` | bool | true | SQL SSL |
| `DbServerName` | string | "" | Backup server name |
| `DbHost` | string | "localhost" | Primary host |
| `DbPort` | string | "1433" | SQL default |
| `DbUser` | string | "admin" | DB user |
| `PureOfflineMode` | bool | false | Skip DB entirely |
| `SuppressOfflineReminder` | bool | false | Hide offline badge |
| `EncryptedMasterPassword` | string | "" | DPAPI-encrypted |
| `EncryptedOfflineAdminPassword` | string | "" | DPAPI-encrypted |
| `EncryptedOfflineUsers` | string | "" | DPAPI-encrypted JSON |

**JsonIgnore properties** (stored in .settings or separate files):
- `ShowDashboardDateFilter`, `DashboardDateTickSize`, `DefaultRowLimit` (in Properties.Settings)
- `CategoryRules` (in `category_rules.json`)
- **NEW: AI Settings** (in general_config.json, serialized)

### Dashboard Configuration (dashboard_config.json)
Location: `%AppData%\WPF_LoginForm\dashboard_config.json`
Managed by: `DashboardStorageService`

Structure:
```json
{
  "DashboardDate": "2026-05-30",
  "StartDate": "2026-05-01",
  "EndDate": "2026-05-30",
  "FilterStartDate": null,
  "FilterEndDate": null,
  "Configurations": [
    {
      "Position": 0,
      "IsEnabled": true,
      "ChartType": "Line",
      "TableName": "DurusAnalizi",
      "DataStructureType": "DailyDate",
      "DateColumn": "Tarih",
      "SplitByColumn": null,
      "AggregationType": "Sum",
      "ValueAggregation": true,
      "ShowLabelsOnChart": true,
      "ShowAsKpiCards": false,
      "Series": [
        {
          "ColumnName": "DurusSuresi",
          "SeriesLabel": "Total Downtime",
          "SeriesColorHex": "#E74C3C",
          "AggregationType": "Sum",
          "IsCombinationLabel": false,
          "ActiveStateIndex": 0,
          "SavedStates": []
        }
      ],
      "IgnoreRowsBelow": "",
      "IsInvariantCulture": false
    }
  ],
  "AxisXFormat": "dd MMM",
  "StackedMode": false
}
```

### Portal Configuration (portal_config.json)
Location: `%AppData%\WPF_LoginForm\portal_config.json`
6 buttons, each with: `Title`, `Description`, `DashboardFileName` (maps to a dashboard JSON file)

### Category Rules (category_rules.json)
Location: `{AppDir}\category_rules.json`
Array of: `{ "StartsWith": "...", "MapTo": "..." }`
Used by `CategoryMappingService` to categorize error descriptions.

### Offline Mode
When `_repository is OfflineDataRepository`:
- Data loaded from CSV files in `OfflineFolderPath`
- Uses `OfflineDataCache` for in-memory storage
- No DB connection required
- Settings can still be modified

## Daily Timeline Deep Dive

### Window: DailyTimelineWindow.xaml
**ViewModel**: `DailyTimelineViewModel`
**Repository dependency**: `IDataRepository` (injected)

### Shift Logic
- Day shift: 08:00-20:00 (`IsNightShift = false`)
- Night shift: 20:00-08:00 (`IsNightShift = true`)
- Canvas shows 12 hours, zoomable via slider (2-12h window)

### Data Format
Events stored in table columns (matching pattern: `hata_kodu*`, `error_code*`, `code*`):
```
{HHmm}-{Duration}-MA-{MachineCode}-{Description}
```
Example: `0800-37-MA-00-GENEL-TEMİZLİK`

### TimelineBlockModel Properties
| Property | Description |
|---|---|
| `OriginalEvent` | ErrorEventModel with StartTime, EndTime, DurationMinutes, MachineCode, ErrorDescription |
| `LaneIndex` | 0-5 (6 lanes) |
| `StartMinuteInShift` | Minutes from shift start |
| `DurationMinutes` | Event duration |
| `IsNewUnsaved` | True for new events not yet saved |
| `IsReferenceBlock` | True for ruler/REF blocks |
| `IsBypass` | Bypass flag (codes 33-37) |
| `SourceRow` | DataRow reference in DataTable |
| `SourceColumn` | Column name in DataTable |

### Color Rules
- `#2ECC71` — Green (mola/break/yemek)
- `#F39C12` — Orange (bakım/maint)
- `#E74C3C` — Red (other errors)
- `#8E44AD` — Purple (bypass)
- `#80F39C12` — Amber (reference blocks)

### Machine Codes & Bypass
Codes 33, 34, 35, 36, 37 are auto-flagged as bypass.

### Save Flow
1. `ExecuteApplyToTimeline()` modifies `_currentData` DataTable
2. `SetDirty()` → `IsDirty = true` → enables Save button
3. User clicks Save → `ExecuteSaveToDatabase()` → `_repository.SaveChangesAsync()`
4. On success: `_currentData.AcceptChanges()`, `IsDirty = false`

### KPI Calculations (RecalculateSavings)
- `AutoCalculatedMolaKazanimi` = overlap minutes (sum of durations - unique stopped minutes)
- `ManualBypassKazanimi` = sum of bypass block durations
- `AutoFiiliSure` = shift total - unique stopped minutes
- Raw values read from DB columns (kazanim/fiili columns)

## Dashboard System

### Chart Types
- **Line**: Cartesian with point geometry, 2.5 thickness
- **Bar/Column**: Cartesian columns
- **Pie**: Pie slices

### Color Resolution (DashboardChartService.GetColor)
1. User-specified `SeriesColorHex` (highest priority)
2. Fallback via series title lookup in user color map
3. Cached color from session dictionary
4. Stable variation from series base color
5. Random color (cached)

### Data Processing Flow
1. `HomeViewModel.LoadDashboardFromSnapshot()`
2. `InitializeDashboardAsync()` — validates configs
3. `ProcessChartConfigurationAsync()` — queries DB + processes
4. `DashboardChartService.ProcessChartData()` — aggregates, splits, colors
5. `ApplyChartResultToUI()` — creates LiveCharts SeriesCollection

### Filtering
- Global date filter (Start/End date)
- Per-chart filtering via `DefaultRowLimit`
- `IgnoreRowsBelow` — filters out rows with low values

### DashboardStorageService
Saves/loads `DashboardSnapshot` (full state: configs + chart data + filters + dates).

## Database Layer

### IDataRepository Interface
Key methods:
- `GetTableNamesAsync()` — list all tables
- `GetTableDataAsync(tableName, limit)` — get DataTable
- `GetDataAsync(tableName, columns, dateColumn, start, end)` — filtered
- `SaveChangesAsync(changes, tableName)` — write changes
- `GetErrorDataAsync(start, end, tableName)` — error analytics

### Table Detection
The app detects tables via `GetTableNamesAsync()`. Tables can be user-created.
Column naming conventions:
- Date columns: contains "tarih" or "date"
- Shift columns: contains "vardiya" or "shift"
- Error columns: starts with "hata_kodu", "error_code", or "code"
- Kazanim columns: contains "kazanim" or "kazanım"

### Online/Offline Detection
```csharp
IsOnlineMode => !(_repository is OfflineDataRepository)
```

## ErrorEventModel Parsing
Parse format: `{HHmm}-{Duration}-MA-{Code}-{Description}`
Regex: `^(\d{4})\s*-\s*(\d+)\s*-\s*([A-Za-z]+)\s*-\s*([A-Za-z0-9]+)\s*-\s*(.*)$`
Fallback: Split by `-` and parse parts.

## AI Assistant Rules

### Core Rules
1. **CONFIRMATION REQUIRED**: Never change any config file, database, or setting without showing the user exactly what will change and getting explicit confirmation.
2. **READ-ONLY DATABASE**: All database queries are read-only. Never execute INSERT, UPDATE, DELETE, DROP, ALTER, or any write operations.
3. **TABLE ACCESS CONTROL**: Only read data from tables in the user's allowed list (see AiSettings.AllowedTables). If a table is not in the whitelist, ask the user to add it in AI Settings first.
4. **SHOW YOUR WORK**: When making changes, always show:
   - The current value
   - The new value
   - The file that will be modified
   - What the user needs to do to apply changes (restart app or refresh page)
5. **RESTRICTED OPERATIONS**: Never change passwords, user accounts, or security settings unless explicitly instructed by an admin user.

### Change Workflow
1. User requests a change
2. AI reads the relevant config file
3. AI calculates the change needed
4. AI presents: "I will change X from A to B in file Y. After this change, please [restart the app / refresh the page]. Confirm?"
5. User confirms → AI executes the change
6. AI tells the user what action to take to see the change

### Query Workflow
1. User asks about data
2. AI checks if the table is in AllowedTables
3. If not: "Table 'X' is not in my allowed list. Please add it in Settings > AI Settings > Allowed Tables."
4. If yes: AI constructs a SELECT query and reads the data
5. AI analyzes and returns the answer

## Common AI Tasks Reference

### Change Chart Color
1. Read `dashboard_config.json`
2. Find position + series index
3. Modify `SeriesColorHex` to new value
4. User refreshes dashboard

### Find Outliers
1. Use `GetDataAsync(table, columns, dateColumn, startDate, endDate)`
2. Calculate mean + standard deviation
3. Find values > 3σ from mean
4. Return results

### Modify Timeline Favorites
1. Read `timeline_config.json`
2. Modify `Favorites` array
3. User re-opens timeline page

### Change Database Connection
1. Read `general_config.json`
2. Modify DbHost, DbPort, DbUser, etc.
3. User restarts app

### Update Category Rules
1. Read `category_rules.json`
2. Add/modify rules array
3. User re-opens error management page

## File System Access Summary

| File | Read | Write | Apply |
|---|---|---|---|
| `general_config.json` | ✅ | ✅ | Restart app |
| `dashboard_config.json` | ✅ | ✅ | Refresh dashboard |
| `portal_config.json` | ✅ | ✅ | Refresh portal |
| `timeline_config.json` | ✅ | ✅ | Re-open timeline |
| `category_rules.json` | ✅ | ✅ | Re-open error mgmt |
| `print_report_settings.json` | ✅ | ✅ | Re-open print dialog |
| Database tables (whitelisted) | ✅ | ❌ | N/A |
| `FodyWeavers.xml` | ❌ | ❌ | Build tool config |
| `Resources.resx` / `.tr.resx` | ❌ | ❌ | Source code |
| Any `.cs` / `.xaml` file | ❌ | ❌ | Source code |
