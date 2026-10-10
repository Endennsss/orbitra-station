@echo off
setlocal

rem Orbitra-Edit: сборка и запуск локального теста голосового чата.
cd /d "%~dp0"

echo [Orbitra] Building Tools client...
dotnet build Content.Client\Content.Client.csproj --configuration Tools --no-restore --nologo
if errorlevel 1 goto :build_failed

echo [Orbitra] Building Tools server...
dotnet build Content.Server\Content.Server.csproj --configuration Tools --no-restore --nologo
if errorlevel 1 goto :build_failed

echo [Orbitra] Starting server...
start "Orbitra Voice Server" /D "%~dp0" "%ComSpec%" /K dotnet run --project Content.Server\Content.Server.csproj --configuration Tools --no-build

rem Даём серверу время открыть порт 1212 до запуска клиентов.
timeout /t 5 /nobreak >nul

echo [Orbitra] Starting client 1...
start "Orbitra Voice Client 1" /D "%~dp0" "%ComSpec%" /K dotnet run --project Content.Client\Content.Client.csproj --configuration Tools --no-build -- --connect --connect-address localhost:1212 --username VoiceTester1

echo [Orbitra] Starting client 2...
start "Orbitra Voice Client 2" /D "%~dp0" "%ComSpec%" /K dotnet run --project Content.Client\Content.Client.csproj --configuration Tools --no-build -- --connect --connect-address localhost:1212 --username VoiceTester2

echo.
echo [Orbitra] Server and two voice-chat clients are starting in separate windows.
exit /b 0

:build_failed
echo.
echo [Orbitra] Build failed. Fix the error above before starting the test.
pause
exit /b 1
