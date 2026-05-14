@echo off
REM Auto-detect MSBuild path
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

ECHO Using: %MSBuild%
ECHO.
CALL "%~dp0build_setup.bat"
