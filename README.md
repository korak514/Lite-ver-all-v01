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

## 🏗 Project Structure

- `WPF-LoginForm/`: Main project directory.
  - `ViewModels/`: MVVM ViewModels.
  - `Views/`: XAML Windows and UserControls.
  - `Models/`: Data structures.
  - `Services/`: Business logic and database access.
  - `Repositories/`: Data access layer.
