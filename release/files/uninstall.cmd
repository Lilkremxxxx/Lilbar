@echo off
chcp 65001 >nul
setlocal
rem Lilbar: gỡ cài đặt. Cấu hình của bạn (%APPDATA%\Lilbar) được giữ lại.
set "DEST=%LOCALAPPDATA%\Programs\Lilbar"

taskkill /IM Lilbar.exe >nul 2>&1
timeout /t 2 /nobreak >nul
tasklist /FI "IMAGENAME eq Lilbar.exe" | find /I "Lilbar.exe" >nul && taskkill /F /IM Lilbar.exe >nul 2>&1
rem Đưa taskbar Windows về nếu Lilbar đang ở chế độ thay taskbar.
if exist "%DEST%\Lilbar.exe" "%DEST%\Lilbar.exe" --restore

reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v Lilbar /f >nul 2>&1
del "%APPDATA%\Microsoft\Windows\Start Menu\Programs\Lilbar.lnk" >nul 2>&1
if exist "%DEST%" rmdir /s /q "%DEST%"

echo.
echo Đã gỡ Lilbar.
echo Cấu hình và key vẫn còn trong %APPDATA%\Lilbar, xoá thư mục đó nếu muốn xoá hết.
echo.
pause
