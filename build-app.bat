@echo off
setlocal
cd /d "%~dp0"

set "DOTNET_EXE=%USERPROFILE%\.dotnet\dotnet.exe"
if not exist "%DOTNET_EXE%" set "DOTNET_EXE=dotnet"

"%DOTNET_EXE%" publish "src\LocalCrypto.App\LocalCrypto.App.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o "release\localCrypto"
