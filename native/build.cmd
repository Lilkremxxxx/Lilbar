@echo off
rem Builds Lilbar.exe with the C# compiler that ships with Windows (.NET Framework 4.8, C# 5). No install needed.
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /optimize+ /out:"%~dp0Lilbar.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll "%~dp0*.cs"
