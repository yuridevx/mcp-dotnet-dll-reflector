@echo off
echo Starting McpNetDll Web Service...
cd /d "%~dp0"
dotnet run --project McpNetDll.Web/McpNetDll.Web.csproj
pause
