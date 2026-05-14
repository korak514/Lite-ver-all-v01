=== WPF-LoginForm Auto Installer ===

This is a Ninite.com-style installer. It automatically installs
PostgreSQL, pgAdmin, creates databases, and configures the app.

HOW TO CREATE THE DISTRIBUTION PACKAGE:

1. BUILD (requires Visual Studio):
   Run build.bat → creates "Output" folder with the app

2. DOWNLOAD & PLACE IN "Output" FOLDER:

   PostgreSQL Installer:
     Download from: https://www.enterprisedb.com/downloads/postgresql
     Choose Windows x86-64 - the .exe installer
     Save as: postgresql-installer.exe

   pgAdmin Installer (optional):
     Download from: https://www.pgadmin.org/download/
     Choose Windows x86-64 .exe
     Save as: pgadmin-installer.exe

3. DISTRIBUTE:
   Zip the entire "Output" folder. Users just:
   - Unzip anywhere
   - Run SetupWizard.exe
   - Enter database name and password
   - PostgreSQL installs automatically (silent)
   - pgAdmin installs automatically (silent)
   - Databases and user are created
   - Config is written
   - App launches

WHAT THE SETUP WIZARD DOES:
- Installs PostgreSQL 16 silently (if not already installed)
- Installs pgAdmin 4 silently (if not already installed)
- Creates LoginDb and your database
- Creates "app_user" as superuser
- Writes general_config.json to %%APPDATA%%\WPF_LoginForm
- Launches the main application

REQUIREMENTS:
- Windows 10 or later
- .NET Framework 4.8 (included in Windows 10/11)
- 1GB free disk space
- Admin privileges (required for installation)
