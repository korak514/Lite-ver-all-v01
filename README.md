Here is a comprehensive `README.md` file tailored specifically to the codebase and project structure you provided.

***

# 🏭 Biosun Operational Data Management System

![.NET Framework 4.8](https://img.shields.io/badge/.NET%20Framework-4.8-purple) ![WPF](https://img.shields.io/badge/UI-WPF%20MVVM-blue) ![Database](https://img.shields.io/badge/Database-SQL%20Server%20%7C%20PostgreSQL-green) ![Build](https://img.shields.io/badge/Build-Portable-orange)

**Biosun** is a high-performance, industrial desktop application designed to bridge the gap between raw operational data entry and high-level business intelligence. Built on **WPF** and the **MVVM** pattern, it offers a robust solution for manufacturing environments requiring data visualization, error analytics, and resilient database management.

---

## 🚀 Key Features

### 📊 Dynamic Reporting Engine
*   **High-Performance Grid:** Utilizes UI virtualization to handle large datasets efficiently.
*   **Smart Filtering:** 
    *   **Global Search:** Search across all columns simultaneously.
    *   **Advanced Syntax:** Supports numeric filters (e.g., `> 50`, `< 100`) and text matching.
*   **Excel Integration:** Seamless Import/Export capabilities using **EPPlus**. The app can analyze an uploaded Excel file and automatically create the corresponding SQL table structure.

### 📉 Error Analytics Module
A specialized BI dashboard designed to parse complex industrial log strings (e.g., `0800-40-MA-01-ARIZA`) into actionable metrics:
*   **Pareto Analysis:** visualizes the longest single downtime incidents.
*   **Machine Health:** Pie charts showing failure distribution by machine code.
*   **Drill-Down Capability:** Clicking a chart slice instantly navigates to the raw data view, pre-filtered for that specific machine or time range.

### 🛠️ Technical Resilience
*   **Dual-Database Support:** Provider-agnostic architecture supporting both **Microsoft SQL Server** and **PostgreSQL**.
*   **Auto-Bootstrapper:** On the first run, the application detects missing databases or schemas and automatically initializes them (Zero-SQL setup for end-users).
*   **Network Resilience:** Implements a `DatabaseRetryPolicy` to handle transient network drops and a `ConnectionManager` to resolve backup servers if the primary IP is unreachable.

### 🌍 User Experience
*   **Localization:** Fully localized interface (English 🇺🇸 / Turkish 🇹🇷).
*   **Portable Deployment:** Uses **Costura.Fody** to embed dependencies, resulting in a single `.exe` file for easy drag-and-drop deployment.
*   **Role-Based Security:** Distinct functionality for Admins (Schema editing, User management) vs. Standard Users.

---

## 🏗️ Architecture

The project follows a strict **MVVM (Model-View-ViewModel)** architecture to ensure separation of concerns and testability.

```text
📂 WPF-LoginForm
├── 📂 Models          # Data structures (User, DashboardConfig, ErrorEvent)
├── 📂 ViewModels      # Presentation logic (HomeVM, DatarepVM, ErrorManagementVM)
├── 📂 Views           # XAML UI definitions
├── 📂 Repositories    # Data access layer (SQL/Postgres abstraction)
├── 📂 Services        # Business logic (Excel analysis, Network, Logging)
│   ├── 📂 Database    # Bootstrapper, Factory, Retry Policy
│   └── 📂 Network     # Connection Manager
└── 📂 Styles          # Resource dictionaries for UI theming
```

---

## ⚙️ Setup & Installation

### Prerequisites
*   **OS:** Windows 10 or 11.
*   **Runtime:** [.NET Framework 4.8 Runtime](https://dotnet.microsoft.com/download/dotnet-framework/net48).
*   **Database (Optional):** Microsoft SQL Server (LocalDB or Express) OR PostgreSQL.

### First Run
1.  **Launch:** Run the executable.
2.  **Initialization:** The app will show a splash screen:
    *   It attempts to ping the configured host.
    *   It checks for the existence of `LoginDb` and `MainDataDb`.
    *   If missing, it asks for permission to create them automatically.
3.  **Login:**
    *   **Username:** `admin`
    *   **Password:** `admin`

### Database Configuration
If you need to change the database provider or connection string:
1.  On the Login screen, click **Settings**.
2.  Select **SQL Server** or **PostgreSQL**.
3.  Enter Host, Port, User, and Password.
4.  Click **Test Connection** -> **Save**.
5.  Restart the application.

---

## 📦 Dependencies

*   **LiveCharts.Wpf:** For interactive Dashboards and Analytics charts.
*   **EPPlus:** For high-speed Excel reading and writing.
*   **Npgsql / Microsoft.Data.SqlClient:** Database drivers.
*   **Costura.Fody:** For embedding DLLs into the main executable.
*   **Newtonsoft.Json:** For dashboard configuration serialization.
*   **FontAwesome.Sharp:** For vector UI icons.

---

## 🧩 Usage Guide

### 1. The Dashboard
*   **Customization:** Click the "Gear" icon ⚙️ to configure which charts appear. You can map specific database columns to X/Y axes.
*   **Date Filtering:** Use the range slider at the top to globally filter all data visualizations.

### 2. Reports View (Data Grid)
*   **Import Table:** Use the "Advanced Import" to map Excel headers to Database columns and ignore specific header rows.
*   **Hierarchy Data:** Tables named with the prefix `_Long_` trigger a special "Detailed Entry" form, allowing for hierarchical dropdown selection (Category -> SubCategory -> Item).

### 3. Error Analytics
*   This module specifically looks for data formatted as `StartTime-Duration-Section-Machine-Description`.
*   **Example Data:** `08:00-45-MA-05-ElectricalFailure`.
*   The system parses this into:
    *   **Machine:** 05
    *   **Duration:** 45 minutes
    *   **Type:** Electrical Failure

---

## 📝 License

This project is proprietary software developed for internal operational management. All rights reserved.

---

## 👨‍💻 Developer Notes

*   **Logging:** Logs are stored in the database (`Logs` table) and locally in `%AppData%\WPF_LoginForm` if the DB is unreachable.
*   **Theme:** Colors and Styles are centralized in `Styles/UIColors.xaml`.
*   **Localization:** Edit `Properties/Resources.resx` for English and `Properties/Resources.tr.resx` for Turkish strings.
