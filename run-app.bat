@echo off
setlocal
cd /d "%~dp0"

set "DOTNET_EXE=%USERPROFILE%\.dotnet\dotnet.exe"
if not exist "%DOTNET_EXE%" set "DOTNET_EXE=dotnet"

"%DOTNET_EXE%" run --project "src\LocalCrypto.App\LocalCrypto.App.csproj"
