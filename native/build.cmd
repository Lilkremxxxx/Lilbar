@echo off
rem Builds Lilbar.exe with the C# compiler that ships with Windows (.NET Framework 4.8, C# 5). No install needed.
rem WinRT (media, radios) comes from the metadata that ships in System32\WinMetadata.
set W=%WINDIR%\System32\WinMetadata
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /optimize+ /out:"%~dp0Lilbar.exe" ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.Security.dll /r:System.Runtime.dll ^
  /r:"%W%\Windows.Foundation.winmd" /r:"%W%\Windows.Media.winmd" /r:"%W%\Windows.Devices.winmd" /r:"%W%\Windows.Storage.winmd" /r:"%W%\Windows.Networking.winmd" ^
  "%~dp0*.cs"
