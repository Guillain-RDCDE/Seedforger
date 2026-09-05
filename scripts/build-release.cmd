@echo off
REM Build both release single-file executables into .\publish\.
REM Closes any running instance first, because the self-contained (fat) single-file
REM build fails with MSB4018 if the target exe is locked by a running process.

echo Closing any running Seedforger...
taskkill /F /IM Seedforger.exe >nul 2>&1

echo.
echo Building lite (framework-dependent, ~0.5 MB, needs the .NET 8 Desktop Runtime)...
dotnet publish src\Seedforger\Seedforger.csproj -c Release -r win-x64 --self-contained false ^
  -p:PublishSingleFile=true -o publish\lite || exit /b 1

echo.
echo Building fat (self-contained, ~24 MB, needs nothing)...
REM Trimming and compression come from src\Publish.props, not from this script.
dotnet publish src\Seedforger\Seedforger.csproj -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -o publish\fat || exit /b 1

echo.
echo Done:
echo   publish\lite\Seedforger.exe
echo   publish\fat\Seedforger.exe
