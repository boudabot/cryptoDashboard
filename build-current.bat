@echo off
setlocal enabledelayedexpansion
cd /d "%~dp0"

set "DOTNET_EXE=%USERPROFILE%\.dotnet\dotnet.exe"
if not exist "%DOTNET_EXE%" set "DOTNET_EXE=dotnet"

for /f "usebackq delims=" %%b in (`git branch --show-current`) do set "BRANCH=%%b"
if "%BRANCH%"=="" set "BRANCH=unknown"

set "SAFE_BRANCH=%BRANCH:/=-%"
set "SAFE_BRANCH=%SAFE_BRANCH:\=-%"

if /i "%BRANCH%"=="master" (
    set "OUTPUT=release\localCrypto"
) else (
    set "OUTPUT=release\localCrypto-%SAFE_BRANCH%"
)

echo Branche: %BRANCH%
echo Sortie: %OUTPUT%

set "MSBuildEnableWorkloadResolver=false"
"%DOTNET_EXE%" publish "src\LocalCrypto.App\LocalCrypto.App.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o "%OUTPUT%" -m:1
