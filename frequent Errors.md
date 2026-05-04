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


---
*Note: New errors will be appended here.*
