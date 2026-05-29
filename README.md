# WPF LoginForm - MVVM & SQL Server

This is a WPF application built with C#, MVVM pattern, and SQL Server.

## 🛠 Building and Running

You can build and run the project using the provided PowerShell scripts. These scripts use MSBuild from Visual Studio 2022 to ensure proper compilation of .NET Framework 4.8 resources.

### 🚀 Quick Start

- **Debug Mode (with Build):**
  Open PowerShell in the root directory and run:
  ```powershell
  .\debug.ps1
  ```
  *This will restore packages, build in Debug mode, and launch the app.*

- **Release Mode (with Build):**
  Open PowerShell in the root directory and run:
  ```powershell
  .\release.ps1
  ```
  *This will restore packages, build in Release mode, and launch the app.*

### 🔧 Advanced Usage

You can use the `run.ps1` script for more control:

| Command | Description |
| :--- | :--- |
| `.\run.ps1 -Configuration Debug -Run` | Build and Run Debug |
| `.\run.ps1 -Configuration Release -Run` | Build and Run Release |
| `.\run.ps1 -Restore` | Only Restore NuGet packages |
| `.\run.ps1 -Configuration Debug` | Only Build Debug (no run) |

## 📦 Prerequisites

- **Visual Studio 2022** (Community, Professional, or Enterprise)
- **.NET Framework 4.8 SDK**
- **SQL Server** (For database features)

## 🤖 AI Assistant Usage Rules

These conventions help the AI assistant work effectively with this codebase.

### 1. Mode System
- **`M:PM`** = Planning Mode — AI analyzes, suggests, and asks questions but does NOT modify code.
- **`M:CM`** = Coding Mode — AI implements changes, edits files, and runs builds.
- Always prefix requests with the current mode (`M:CM` or `M:PM`).

### 2. File Reading Before Editing
- AI must read a file before editing it. This prevents accidental overwrites and ensures context is understood.

### 3. Build After Changes
- After any code change, AI runs `.\release.ps1` or `.\debug.ps1` to verify the build.
- If there are errors, AI fixes them and rebuilds until clean.

### 4. Bug Documentation
- All bugs found and their fixes are logged in `frequent Errors.md` with file paths, root causes, and resolutions.

### 5. Dual Language (EN/TR)
- The app supports English and Turkish. Any new label/string must be added to both `Resources.resx` (EN) and `Resources.tr.resx` (TR), plus the corresponding property in `Resources.Designer.cs`.

### 6. Ask When Ambiguous
- If a request is unclear or has multiple valid interpretations, AI asks clarifying questions rather than guessing.

### 7. Follow Existing Conventions
- Code style, naming, patterns (MVVM, ICommand, ObservableCollection, etc.) must match the existing codebase. New components mimic nearby files.

### 8. Minimal, Focused Changes
- Changes are scoped to the specific request. No refactoring or cleanup beyond what's asked unless it directly impacts the task.

### 9. Commit Discipline
- AI does NOT commit changes unless explicitly instructed by the user. All changes remain in the working tree until the user says to commit.
- When the user says **"update my repo"** (e.g. "update my repo v14.5"), this means: **commit all changes and push to GitHub**, with a commit message of the format `"version {X.Y}"` (e.g. `"version 14.5"`).

### 10. UI Element Name → XAML Trace
- When the user reports a UI bug with a specific control name (in any language), immediately search for that name in `*.xaml` files and resource (`.resx`) files. The resource key reveals the binding property. Ignore Turkish/English differences — both `Resources.tr.resx` and `Resources.resx` may be active.

### 11. Dual UI Elements May Share Similar Names
- This app has **both** a "Kullanıcı Yönetimi" / "User Management" **tab** (`Tab_UserManagement`, visibility = `CanManageUsers`) **and** a "Çevrimdışı Kullanıcılar" / "Offline Users" **section** inside the Configuration tab (`Str_OfflineUsers`, visibility = `CanManageOfflineUsers`). They have **different** visibility conditions. When the user reports one is missing, confirm which exact element they mean — do not assume.

### 12. Always Pin Down the Exact User Flow First
- Before investigating a bug, ask or verify the exact steps: "Login first, then navigate" vs "Click Settings button on login screen." These trigger different `AppMode` branches (`OfflineReadOnly` vs `SettingsOnly`) with different `_dataRepository` assignments and different session states.

### 13. Start With the Simplest Hypothesis
- When a UI element is hidden, check its XAML `Visibility` binding first. Search for the bound property name in the ViewModel. If the property is a computed expression (e.g., `bool CanX => A && B`), check each operand individually. This is faster than tracing constructor order, caching, or mode-selection logic.

### 14. Resource String Tracing
- User-facing Turkish text (`"Kullanıcı Yönetimi"`) can be found in `Resources.tr.resx`. The corresponding `name` attribute in the `.resx` file is the programmatic key (e.g., `Tab_UserManagement`). Search the codebase for that key to find where it's used in XAML (`{x:Static p:Resources.Tab_UserManagement}`). The linked binding property is the fix target.

### 15. Verify Fixes Against the Reported Flow
- After making a change, re-read the user's flow description and mentally trace through the code paths to confirm the fix actually applies. If there are multiple possible flows, ask the user to confirm after the first attempt.

## 🏗 Project Structure

- `WPF-LoginForm/`: Main project directory.
  - `ViewModels/`: MVVM ViewModels.
  - `Views/`: XAML Windows and UserControls.
  - `Models/`: Data structures.
  - `Services/`: Business logic and database access.
  - `Repositories/`: Data access layer.
