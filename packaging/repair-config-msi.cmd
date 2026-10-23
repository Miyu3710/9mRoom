@echo off
setlocal
net session >nul 2>&1
if errorlevel 1 (
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

echo Repairing D:\Config.Msi permissions...
if not exist "D:\Config.Msi" mkdir "D:\Config.Msi"
takeown /f "D:\Config.Msi" /r /d y
icacls "D:\Config.Msi" /grant *S-1-5-32-545:(OI)(CI)F /T /C

echo.
echo Testing write access as the current user...
set "TESTFILE=D:\Config.Msi\9mroom-write-test.txt"
> "%TESTFILE%" echo ok
if exist "%TESTFILE%" (
    del /f /q "%TESTFILE%"
    echo SUCCESS: D:\Config.Msi is writable.
) else (
    echo FAILED: D:\Config.Msi is still not writable.
)

echo.
pause
