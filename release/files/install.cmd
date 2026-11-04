@echo off
chcp 65001 >nul
setlocal
rem Lilbar: cài cho tài khoản Windows hiện tại (không cần quyền admin).
set "DEST=%LOCALAPPDATA%\Programs\Lilbar"

rem Tắt bản đang chạy (nếu có) để chép đè được.
taskkill /IM Lilbar.exe >nul 2>&1
timeout /t 2 /nobreak >nul
tasklist /FI "IMAGENAME eq Lilbar.exe" | find /I "Lilbar.exe" >nul && (
  taskkill /F /IM Lilbar.exe >nul 2>&1
  if exist "%DEST%\Lilbar.exe" "%DEST%\Lilbar.exe" --restore
)

if not exist "%DEST%" mkdir "%DEST%"
copy /Y "%~dp0Lilbar.exe" "%DEST%\Lilbar.exe" >nul || (
  echo Không chép được Lilbar.exe vào %DEST%.
  pause
  exit /b 1
)

rem Lối tắt trong Start và khởi động cùng Windows.
powershell -NoProfile -Command "$s = (New-Object -ComObject WScript.Shell).CreateShortcut([Environment]::GetFolderPath('Programs') + '\Lilbar.lnk'); $s.TargetPath = '%DEST%\Lilbar.exe'; $s.WorkingDirectory = '%DEST%'; $s.Description = 'Widget cho taskbar Windows'; $s.Save()"
reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v Lilbar /t REG_SZ /d "\"%DEST%\Lilbar.exe\" --startup" /f >nul

start "" "%DEST%\Lilbar.exe"
echo.
echo Đã cài Lilbar vào %DEST% và bật khởi động cùng Windows.
echo Chuột phải vào widget trên taskbar để mở Cài đặt.
echo.
pause
