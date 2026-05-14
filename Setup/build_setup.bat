@echo off
ECHO ========================================
ECHO  Building WPF-LoginForm Setup Package
ECHO ========================================
ECHO.

REM 1. Build both projects in Release
ECHO [1/4] Building WPF-LoginForm...
"%MSBuild%" "..\WPF-LoginForm\WPF-LoginForm.csproj" /p:Configuration=Release /v:q /nologo
IF %ERRORLEVEL% NEQ 0 (ECHO FAILED & PAUSE & EXIT /B 1)
ECHO OK

ECHO [2/4] Building SetupWizard...
"%MSBuild%" "..\SetupWizard\SetupWizard.csproj" /p:Configuration=Release /v:q /nologo
IF %ERRORLEVEL% NEQ 0 (ECHO FAILED & PAUSE & EXIT /B 1)
ECHO OK

REM 2. Create output folder
SET OUTPUT=%~dp0Output
IF EXIST "%OUTPUT%" RMDIR /S /Q "%OUTPUT%"
MKDIR "%OUTPUT%"

REM 3. Copy app files
ECHO [3/4] Copying application files...
XCOPY "..\WPF-LoginForm\bin\Release\*" "%OUTPUT%\" /E /I /Q /Y >NUL

REM 4. Copy setup wizard
ECHO [4/4] Copying setup wizard...
COPY /Y "..\SetupWizard\bin\Release\SetupWizard.exe" "%OUTPUT%\" >NUL

ECHO.
ECHO ========================================
ECHO  Setup package created at:
ECHO  %OUTPUT%
ECHO ========================================
ECHO.
ECHO Next steps:
ECHO   1. Download PostgreSQL portable zip from:
ECHO      https://www.enterprisedb.com/download-postgresql-binaries
ECHO   2. Extract and rename folder to "pgsql"
ECHO   3. Zip it as "postgresql-portable.zip"
ECHO   4. Place it in: %OUTPUT%
ECHO   5. Run SetupWizard.exe
ECHO.
PAUSE
