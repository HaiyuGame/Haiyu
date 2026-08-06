@echo off
setlocal
cd /d "%~dp0"
title Haiyu.Mobile Android Publish

echo.
echo  Running publish-android.ps1 ...
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0publish-android.ps1" %*
set EXITCODE=%ERRORLEVEL%

echo.
if %EXITCODE% neq 0 (
  echo [FAILED] exit code %EXITCODE%
) else (
  echo [DONE]
)
echo.
pause
exit /b %EXITCODE%
