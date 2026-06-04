@echo off
chcp 65001 >nul
title WPF-LoginForm - Kurulum Modu Seçici
echo ============================================
echo   WPF-LoginForm Çalışma Modu Seçimi
echo ============================================
echo.
echo Lütfen bir mod seçin:
echo.
echo   [1] Tam Offline
echo       - Veritabanı bağlantısı olmadan çalışır
echo       - Tüm veriler network paylaşımından okunur
echo       - Bağlantı ayarları gerekmez
echo.
echo   [2] Online + Offline Yedek
echo       - Veritabanına bağlanarak çalışır
echo       - Offline veriler yedek olarak kullanılır
echo       - Veritabanı bağlantı bilgileri gerekir
echo.
set /p MODE="Seçiminiz (1 veya 2): "

if "%MODE%"=="1" goto offline
if "%MODE%"=="2" goto online
echo Geçersiz seçim. Lütfen 1 veya 2 girin.
pause
exit /b 1

:offline
echo.
echo [1] Tam Offline modu seçildi.
echo.
echo general_config.json düzenleniyor...
powershell -NoProfile -Command ^
    "$json = Get-Content '%~dp0general_config.json' -Encoding UTF8 | ConvertFrom-Json; " ^
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
    "$json | ConvertTo-Json | Set-Content '%~dp0general_config.json' -Encoding UTF8"
if %errorlevel% neq 0 (
    echo HATA: general_config.json düzenlenemedi.
    pause
    exit /b 1
)
echo.
echo Tam Offline moduna geçildi.
echo NOT: offline_path.txt dosyasındaki klasör yolunu kontrol edin.
echo.
pause
exit /b 0

:online
echo.
echo [2] Online + Offline Yedek modu seçildi.
echo.
echo Veritabanı bağlantı bilgilerini girin:
echo.
set /p DBHOST="Sunucu adresi (örn: localhost veya 192.168.1.100): "
set /p DBPORT="Port (PostgreSQL varsayılan: 5432): "
set /p DBUSER="Kullanıcı adı (varsayılan: postgres): "
set /p DBPASS="Şifre: "
set /p DBNAME="Veritabanı adı: "
echo.
echo general_config.json düzenleniyor...
powershell -NoProfile -Command ^
    "$json = Get-Content '%~dp0general_config.json' -Encoding UTF8 | ConvertFrom-Json; " ^
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
    "$json | ConvertTo-Json | Set-Content '%~dp0general_config.json' -Encoding UTF8"
if %errorlevel% neq 0 (
    echo HATA: general_config.json düzenlenemedi.
    pause
    exit /b 1
)
echo.
echo Online + Offline Yedek moduna geçildi.
echo.
pause
exit /b 0
