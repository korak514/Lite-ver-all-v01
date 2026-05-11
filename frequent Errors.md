# Frequent Errors

This file tracks common errors encountered during development and their resolutions.

## XAML Errors

### 1. `Failed to create a 'SelectionMode' from the text 'SharedX'.`
- **Cause**: Using an incorrect enum value name in XAML.
- **Resolution**: Changed `SharedX` to `SharedXValues`.

### 2. `Provide value on 'System.Windows.StaticResourceExtension' threw an exception.`
- **Cause**: Referencing a `StaticResource` (like a Converter) that is not defined in the current file's `Resources` section.
- **Resolution**: Ensure the converter is defined in `<UserControl.Resources>`.

### 3. Settings not saving from UI
- **Cause**: The UI is binding to a property that exists on the Model but hasn't been exposed as a bindable property in the `ViewModel` (using `OnPropertyChanged`).
- **Resolution**: Add the property to the `ViewModel` and ensure it updates the underlying `Model` and calls `OnPropertyChanged()`.


### 4. `Chart 4 PieChart `Hoverable` hardcoded to `False``
- **File**: `WPF-LoginForm\Views\HomeView.xaml:429`
- **Cause**: The PieChart for Chart 4 had `Hoverable="False"` hardcoded, making tooltips never appear even when maximized.
- **Resolution**: Changed `Hoverable="False"` to `Hoverable="{Binding IsChart4Maximized}"` to match Chart 6's behavior.

### 5. `ShowAsKpiCards checkbox invisible on white background`
- **File**: `WPF-LoginForm\Views\ConfigurationView.xaml:116`
- **Cause**: The checkbox used `Foreground="WhiteSmoke"` but the parent container has `Background="White"`, making the text invisible.
- **Resolution**: Changed `Foreground` to `#555` (dark gray) for readability.

### 6. `ParseSafeDouble strips single-dot decimals incorrectly`
- **File**: `WPF-LoginForm\ViewModels\HomeViewModel.cs:405`
- **Cause**: The `else if (lastDot != -1 && lastComma == -1)` branch unconditionally removed all dots (e.g., `"0.56"` → `"056"` → parsed as `56`).
- **Resolution**: Only strip dots when there are multiple dots (thousands separators). Single-dot decimals are left for `TryParse` to handle correctly.

### 7. `Missing null check on BrushConverter.ConvertFrom in ChartDetailViewModel`
- **File**: `WPF-LoginForm\ViewModels\ChartDetailViewModel.cs:211`
- **Cause**: `BrushConverter.ConvertFrom(s.ColorHex)` was called without null check. If `ColorHex` is null or invalid, the cast to `Brush` throws.
- **Resolution**: Added try/catch with fallback to `Brushes.Gray`.

### 8. `LoadDashboardFromSnapshot doesn't guarantee chart position ordering`
- **File**: `WPF-LoginForm\ViewModels\HomeViewModel.cs:1124`
- **Cause**: The saved snapshot's config list might not be in chart position order, but XAML bindings use `DashboardConfigs[N]` by index.
- **Resolution**: Added `.OrderBy(c => c.ChartPosition)` when assigning `_dashboardConfigurations` from snapshot.

---
*Note: New errors will be appended here.*
