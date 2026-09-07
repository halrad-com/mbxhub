@echo off
:: Builds hello-spout with the MSVC toolchain and the CMake that Visual Studio bundles.
:: Output: %~dp0build\Release\hello-spout.exe (+ manifest.json copied beside it)
::
:: hello-spout loads mbxspout.dll at runtime. If one is not sitting beside the exe it falls
:: back to ..\..\build\Release\mbxspout.dll - this repo's own build output - so run the
:: repo-root build.cmd first, or pass --dll <path>.
setlocal
set VSROOT=C:\Program Files\Microsoft Visual Studio\2022\Enterprise
:: %ProgramFiles(x86)% cannot appear inside a parenthesised block (the ')' ends the block), so resolve it first.
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if exist "%VSROOT%\VC\Auxiliary\Build\vcvars64.bat" goto :haveVs
for /f "usebackq tokens=*" %%i in (`"%VSWHERE%" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath`) do set "VSROOT=%%i"
:haveVs
call "%VSROOT%\VC\Auxiliary\Build\vcvars64.bat" >nul || (echo vcvars64 not found under "%VSROOT%" & exit /b 1)
set CMAKE=%VSROOT%\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe

"%CMAKE%" -S "%~dp0." -B "%~dp0build" -G "Visual Studio 17 2022" -A x64 || exit /b 1
"%CMAKE%" --build "%~dp0build" --config Release || exit /b 1

echo.
echo Built: %~dp0build\Release\hello-spout.exe
echo.
echo Two windows, two processes:
echo   %~dp0build\Release\hello-spout.exe --send
echo   %~dp0build\Release\hello-spout.exe --recv
