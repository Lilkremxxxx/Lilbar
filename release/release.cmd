@echo off
rem Builds Lilbar.exe and packs release\Lilbar-<version>.zip (Lilbar.exe, install.cmd, uninstall.cmd, HUONG-DAN.txt).
setlocal
set VER=1.0.0
call "%~dp0..\native\build.cmd" || exit /b 1
set "STAGE=%~dp0Lilbar"
if exist "%STAGE%" rmdir /s /q "%STAGE%"
mkdir "%STAGE%"
copy /Y "%~dp0..\native\Lilbar.exe" "%STAGE%\" >nul
copy /Y "%~dp0files\*" "%STAGE%\" >nul
powershell -NoProfile -Command "Compress-Archive -Path '%STAGE%\*' -DestinationPath '%~dp0Lilbar-%VER%.zip' -Force"
rmdir /s /q "%STAGE%"
echo Packed release\Lilbar-%VER%.zip
