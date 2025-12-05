@echo off
setlocal

echo ========================================
echo Kotonoha Assistant Launcher
echo ========================================
echo.

REM Check if A.I. VOICE Editor is installed
set "AIVOICE_DIR=%PROGRAMFILES%\AI\AIVoice\AIVoiceEditor"
if not exist "%AIVOICE_DIR%" (
    echo [ERROR] A.I. VOICE Editor is not installed.
    echo Please install A.I. VOICE Editor from:
    echo https://aivoice.jp/product/kotonoha/
    echo.
    pause
    exit /b 1
)

echo [OK] A.I. VOICE Editor found at:
echo     %AIVOICE_DIR%
echo.

REM Check and copy required DLLs to VoiceServer directory if needed
set "VOICESERVER_DIR=%~dp0KotonohaAssistant.VoiceServer"
set "COPY_NEEDED=0"

if not exist "%VOICESERVER_DIR%\AI.Talk.dll" set "COPY_NEEDED=1"
if not exist "%VOICESERVER_DIR%\AI.Talk.Editor.Api.dll" set "COPY_NEEDED=1"
if not exist "%VOICESERVER_DIR%\AI.Framework.dll" set "COPY_NEEDED=1"
if not exist "%VOICESERVER_DIR%\System.Text.Json.dll" set "COPY_NEEDED=1"
if not exist "%VOICESERVER_DIR%\System.ValueTuple.dll" set "COPY_NEEDED=1"

if %COPY_NEEDED%==1 (
    echo Copying required DLLs...

    copy /Y "%AIVOICE_DIR%\AI.Talk.dll" "%VOICESERVER_DIR%\" >nul
    copy /Y "%AIVOICE_DIR%\AI.Talk.Editor.Api.dll" "%VOICESERVER_DIR%\" >nul
    copy /Y "%AIVOICE_DIR%\AI.Framework.dll" "%VOICESERVER_DIR%\" >nul
    copy /Y "%AIVOICE_DIR%\System.Text.Json.dll" "%VOICESERVER_DIR%\" >nul
    copy /Y "%AIVOICE_DIR%\System.ValueTuple.dll" "%VOICESERVER_DIR%\" >nul

    if %errorlevel% neq 0 (
        echo [ERROR] Failed to copy DLLs.
        pause
        exit /b 1
    )

    echo [OK] DLLs copied successfully.
) else (
    echo [OK] Required DLLs already exist.
)
echo.

REM Start applications
echo Starting applications...
start "" "%~dp0KotonohaAssistant.Alarm\KotonohaAssistant.Alarm.exe"
timeout /t 1 /nobreak >nul

start "" "%~dp0KotonohaAssistant.Vui\KotonohaAssistant.Vui.exe"
timeout /t 1 /nobreak >nul

echo.
echo Starting VoiceServer in this console...
echo Press Ctrl+C to stop VoiceServer and close all apps.
echo.

cd /d "%~dp0KotonohaAssistant.VoiceServer"
KotonohaAssistant.VoiceServer.exe
