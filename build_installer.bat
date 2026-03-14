@echo off
set "ISS_PATH=installer.iss"
set "CO_PATH=C:\Program Files (x86)\Inno Setup 6\ISCC.exe"

if not exist "%CO_PATH%" (
    set "CO_PATH=C:\Program Files\Inno Setup 6\ISCC.exe"
)

if exist "%CO_PATH%" (
    echo Building installer with Inno Setup...
    "%CO_PATH%" "%ISS_PATH%"
) else (
    echo [ERROR] Inno Setup is not installed. Please install it from https://jrsoftware.org/isdl.php
    echo or use the provided .iss script manually.
)
pause
