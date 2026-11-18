@echo off
setlocal EnableExtensions

rem ---------------------------------------------------------------------------
rem SkinService - SPT 4.1.6 (SPTushonka 4.1) release builder
rem   * client plugin : BepInEx 5 / Mono / .NET Framework 4.7.2
rem   * server mod    : SPT 4.1 C# server / .NET 10
rem ---------------------------------------------------------------------------

set "ROOT=%~dp0"
set "CLIENT=%ROOT%SkinService"
set "SERVER=%ROOT%Build\SkinService_ServerMod_CSharp"
set "OUT=%ROOT%Output"
set "RELEASE=%ROOT%Release"
set "REFS=%ROOT%References"

set "MSBUILD=%ProgramFiles%\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
if not exist "%MSBUILD%" set "MSBUILD=%ProgramFiles%\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe"
if not exist "%MSBUILD%" set "MSBUILD=%ProgramFiles%\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
if not exist "%MSBUILD%" set "MSBUILD=%ProgramFiles(x86)%\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"

echo ============================================
echo SkinService build - SPT 4.1.6
echo ============================================
echo.

echo [1/4] Building client plugin...
if not exist "%MSBUILD%" (
    echo ERROR: MSBuild for Visual Studio 2022 was not found.
    goto :error
)
"%MSBUILD%" "%CLIENT%\SkinService.csproj" -p:Configuration=Release -p:Platform=AnyCPU -v:minimal -nologo
if errorlevel 1 goto :error
echo Client plugin built.

echo.
echo [2/4] Building server mod...
pushd "%SERVER%"
dotnet build -c Release -v minimal --nologo
if errorlevel 1 (
    popd
    goto :error
)
popd
echo Server mod built.

echo.
echo [3/4] Assembling output...
if exist "%RELEASE%" rmdir /s /q "%RELEASE%"

mkdir "%OUT%\SkinService_Client\BepInEx\plugins\kmyuhkyuk-SkinService\bundles" 2>nul
mkdir "%OUT%\SkinService_Client\BepInEx\plugins\kmyuhkyuk-SkinService\localized" 2>nul
mkdir "%OUT%\SkinService_Server\SPT_Runtime\user\mods\kmyuhkyuk-SkinService" 2>nul

copy /y "%CLIENT%\bin\Release\SkinService.dll" "%OUT%\SkinService_Client\BepInEx\plugins\kmyuhkyuk-SkinService\" >nul
copy /y "%ROOT%Build\localized\*.json" "%OUT%\SkinService_Client\BepInEx\plugins\kmyuhkyuk-SkinService\localized\" >nul
copy /y "%ROOT%bundles\skinservice.bundle" "%OUT%\SkinService_Client\BepInEx\plugins\kmyuhkyuk-SkinService\bundles\" >nul

copy /y "%SERVER%\bin\Release\SkinService.dll" "%OUT%\SkinService_Server\SPT_Runtime\user\mods\kmyuhkyuk-SkinService\" >nul

rem KmyTarkovApi plugin (built from the 4.1 port in KmyTarkovApi-upstream)
mkdir "%OUT%\KmyTarkovApi_Plugin\bundles" 2>nul
mkdir "%OUT%\KmyTarkovApi_Plugin\localized" 2>nul
copy /y "%REFS%\KmyTarkov*.dll" "%OUT%\KmyTarkovApi_Plugin\" >nul
copy /y "%REFS%\Crc32.NET.dll" "%OUT%\KmyTarkovApi_Plugin\" >nul
copy /y "%REFS%\HtmlAgilityPack.dll" "%OUT%\KmyTarkovApi_Plugin\" >nul

echo Done.

echo.
echo [4/4] Creating release folder...
mkdir "%RELEASE%\BepInEx\plugins\kmyuhkyuk-SkinService\bundles" 2>nul
mkdir "%RELEASE%\BepInEx\plugins\kmyuhkyuk-SkinService\localized" 2>nul
mkdir "%RELEASE%\BepInEx\plugins\kmyuhkyuk-KmyTarkovApi" 2>nul
mkdir "%RELEASE%\SPT_Runtime\user\mods\kmyuhkyuk-SkinService" 2>nul

xcopy /e /i /y "%OUT%\SkinService_Client\BepInEx" "%RELEASE%\BepInEx" >nul
xcopy /e /i /y "%OUT%\KmyTarkovApi_Plugin" "%RELEASE%\BepInEx\plugins\kmyuhkyuk-KmyTarkovApi" >nul
copy /y "%OUT%\SkinService_Server\SPT_Runtime\user\mods\kmyuhkyuk-SkinService\SkinService.dll" "%RELEASE%\SPT_Runtime\user\mods\kmyuhkyuk-SkinService\" >nul
copy /y "%ROOT%Build\ReadMe.txt" "%RELEASE%\ReadMe.txt" >nul 2>nul

echo Release folder: %RELEASE%
echo.
echo Install:
echo   BepInEx\plugins\kmyuhkyuk-SkinService  - client plugin
echo   BepInEx\plugins\kmyuhkyuk-KmyTarkovApi - required API/configuration UI
echo   SPT_Runtime\user\mods\kmyuhkyuk-SkinService - server mod
echo.
echo Build complete.
goto :eof

:error
echo.
echo BUILD FAILED
exit /b 1
