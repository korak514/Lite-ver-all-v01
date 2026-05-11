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

## 🏗 Project Structure

- `WPF-LoginForm/`: Main project directory.
  - `ViewModels/`: MVVM ViewModels.
  - `Views/`: XAML Windows and UserControls.
  - `Models/`: Data structures.
  - `Services/`: Business logic and database access.
  - `Repositories/`: Data access layer.
