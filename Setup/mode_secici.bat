@echo off
chcp 65001 >nul
title WPF-LoginForm - Kurulum Modu Seçici

set "CONFIG_DIR=%~dp0Output"

echo ============================================
echo   WPF-LoginForm Calisma Modu Secimi
echo ============================================

REM Show current mode
if not exist "%CONFIG_DIR%\general_config.json" (
    echo [UYARI] general_config.json bulunamadi!
    echo Lutfen bu dosyayi %CONFIG_DIR% klasorune kopyalayin.
    echo.
    pause
    exit /b 1
)

for /f "usebackq delims=" %%a in (`powershell -NoProfile -Command "(Get-Content '%CONFIG_DIR%\general_config.json' -Encoding UTF8 | ConvertFrom-Json).PureOfflineMode"`) do set "OFFLINE_MODE=%%a"
set "OFFLINE_MODE=%OFFLINE_MODE: =%"
if /i "%OFFLINE_MODE%"=="true" (
    echo Su anki mod: [1] Tam Offline
) else (
    echo Su anki mod: [2] Online + Offline Yedek
)
echo.

echo Lutfen bir mod secin:
echo.
echo   [1] Tam Offline
echo       - Veritabani baglantisi olmadan calisir
echo       - Tum veriler network paylasimindan okunur
echo       - Baglanti ayarlari gerekmez
echo.
echo   [2] Online + Offline Yedek
echo       - Veritabanina baglanarak calisir
echo       - Offline veriler yedek olarak kullanilir
echo       - Veritabani baglanti bilgileri gerekir
echo.
set /p MODE="Seciminiz (1 veya 2): "

if "%MODE%"=="1" goto offline
if "%MODE%"=="2" goto online
echo Gecersiz secim. Lutfen 1 veya 2 girin.
pause
exit /b 1

:offline
echo.
echo [1] Tam Offline modu secildi.
echo.
echo general_config.json duzenleniyor...
powershell -NoProfile -Command ^
    "$json = Get-Content '%CONFIG_DIR%\general_config.json' -Encoding UTF8 | ConvertFrom-Json; " ^
    "$json.PureOfflineMode = $true; " ^
    "$json.SqlAuthConnString = ''; " ^
    "$json.SqlDataConnString = ''; " ^
    "$json.PostgresDataConnString = ''; " ^
    "$json.PostgresAuthConnString = ''; " ^
    "$json.DbHost = ''; " ^
    "$json.DbPort = ''; " ^
    "$json.DbUser = ''; " ^
    "$json.DbServerName = ''; " ^
    "$json.SuppressOfflineReminder = $true; " ^
    "$json | ConvertTo-Json | Set-Content '%CONFIG_DIR%\general_config.json' -Encoding UTF8"
if %errorlevel% neq 0 (
    echo HATA: general_config.json duzenlenemedi.
    pause
    exit /b 1
)
echo.
echo offline_path.txt duzenleniyor...
echo \\172.16.16.20\08_elektrik_uretim_bakim\WPF-SCADA\MA_VERI > "%CONFIG_DIR%\offline_path.txt"
if %errorlevel% neq 0 (
    echo HATA: offline_path.txt duzenlenemedi.
    pause
    exit /b 1
)
echo.
echo Tam Offline moduna gecildi.
echo Offline yol: \\172.16.16.20\08_elektrik_uretim_bakim\WPF-SCADA\MA_VERI
echo.
pause
exit /b 0

:online
echo.
echo [2] Online + Offline Yedek modu secildi.
echo.
echo Veritabani baglanti bilgilerini girin:
echo.
set /p DBHOST="Sunucu adresi (orn: localhost veya 192.168.1.100): "
set /p DBPORT="Port (PostgreSQL varsayilan: 5432): "
set /p DBUSER="Kullanici adi (varsayilan: postgres): "
set /p DBPASS="Sifre: "
set /p DBNAME="Veritabani adi: "
echo.
echo general_config.json duzenleniyor...
powershell -NoProfile -Command ^
    "$json = Get-Content '%CONFIG_DIR%\general_config.json' -Encoding UTF8 | ConvertFrom-Json; " ^
    "$json.PureOfflineMode = $false; " ^
    "$json.SuppressOfflineReminder = $false; " ^
    "$json.DbProvider = 'PostgreSql'; " ^
    "$json.DbHost = '%DBHOST%'; " ^
    "$json.DbPort = '%DBPORT%'; " ^
    "$json.DbUser = '%DBUSER%'; " ^
    "$json.DbServerName = '%DBHOST%'; " ^
    "$conn = 'Host=%DBHOST%;Port=%DBPORT%;Database=%DBNAME%;Username=%DBUSER%;Password=%DBPASS%'; " ^
    "$json.PostgresAuthConnString = $conn; " ^
    "$json.PostgresDataConnString = $conn; " ^
    "$json.SqlAuthConnString = ''; " ^
    "$json.SqlDataConnString = ''; " ^
    "$json | ConvertTo-Json | Set-Content '%CONFIG_DIR%\general_config.json' -Encoding UTF8"
if %errorlevel% neq 0 (
    echo HATA: general_config.json duzenlenemedi.
    pause
    exit /b 1
)
echo.
echo Online + Offline Yedek moduna gecildi.
echo.
pause
exit /b 0
