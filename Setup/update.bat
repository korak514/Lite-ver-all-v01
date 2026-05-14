@echo off
REM Auto-detect MSBuild path (same as build.bat)
SET "MSBuild="
IF EXIST "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" (
    SET "MSBuild=C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
) ELSE IF EXIST "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe" (
    SET "MSBuild=C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe"
) ELSE IF EXIST "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe" (
    SET "MSBuild=C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
) ELSE (
    ECHO MSBuild not found. Make sure Visual Studio 2022 is installed.
    PAUSE
    EXIT /B 1
)

ECHO [1/2] Building WPF-LoginForm...
"%MSBuild%" "..\WPF-LoginForm\WPF-LoginForm.csproj" /p:Configuration=Release /v:q /nologo
IF %ERRORLEVEL% NEQ 0 (ECHO FAILED & PAUSE & EXIT /B 1)
ECHO OK

ECHO [2/2] Copying to Output\...
IF NOT EXIST "%~dp0Output" MKDIR "%~dp0Output"
COPY /Y "..\WPF-LoginForm\bin\Release\WPF-LoginForm.exe" "%~dp0Output\WPF-LoginForm.exe" >NUL
IF %ERRORLEVEL% NEQ 0 (ECHO FAILED & PAUSE & EXIT /B 1)
ECHO OK

ECHO.
ECHO Update complete. Copy Output\WPF-LoginForm.exe to other laptops.
PAUSE
