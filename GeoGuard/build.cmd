@echo off
setlocal
cd /d "%~dp0"
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe

"%CSC%" /nologo /target:winexe /platform:x64 /out:GeoGuard.exe /win32manifest:app.manifest ^
  /r:System.dll /r:System.Core.dll /r:System.Xml.dll ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:Microsoft.CSharp.dll ^
  Program.cs TrayApp.cs MonitorForm.cs HistoryForm.cs IpInfoForm.cs CountrySelectForm.cs ^
  AuditWatcher.cs FirewallService.cs CountryIpList.cs GeoDb.cs AsnDb.cs IpRangeSet.cs ^
  NativeTcpTable.cs Throttler.cs Logger.cs Countries.cs Config.cs

if %errorlevel% neq 0 (
  echo Build FAILED
  exit /b 1
)
echo Build OK: GeoGuard.exe
