@ECHO OFF
SETLOCAL

SET "SOLUTION=Ambev.DeveloperEvaluation.sln"
SET "CONFIGURATION=Release"
SET "RESULTS_DIR=TestResults\Coverage"
SET "REPORT_DIR=TestResults\CoverageReport"
SET "DOTNET_TOOLS=%USERPROFILE%\.dotnet\tools"
SET "PATH=%PATH%;%DOTNET_TOOLS%"

WHERE dotnet >NUL 2>NUL
IF ERRORLEVEL 1 (
    ECHO The .NET SDK was not found in PATH.
    EXIT /B 1
)

ECHO Checking ReportGenerator...
WHERE reportgenerator >NUL 2>NUL
IF ERRORLEVEL 1 (
    dotnet tool install --global dotnet-reportgenerator-globaltool
    IF ERRORLEVEL 1 EXIT /B 1
)

ECHO Cleaning previous coverage output...
IF EXIST "%RESULTS_DIR%" RMDIR /S /Q "%RESULTS_DIR%"
IF EXIST "%REPORT_DIR%" RMDIR /S /Q "%REPORT_DIR%"

ECHO Restoring solution...
dotnet restore "%SOLUTION%"
IF ERRORLEVEL 1 EXIT /B 1

ECHO Building solution...
dotnet build "%SOLUTION%" --configuration "%CONFIGURATION%" --no-restore
IF ERRORLEVEL 1 EXIT /B 1

ECHO Running tests with coverage...
dotnet test "%SOLUTION%" ^
    --configuration "%CONFIGURATION%" ^
    --no-build ^
    --results-directory "%RESULTS_DIR%" ^
    --collect:"XPlat Code Coverage" ^
    --verbosity normal
IF ERRORLEVEL 1 EXIT /B 1

ECHO Generating HTML coverage report...
reportgenerator ^
    -reports:"%RESULTS_DIR%\**\coverage.cobertura.xml" ^
    -targetdir:"%REPORT_DIR%" ^
    -reporttypes:Html ^
    -assemblyfilters:"+Ambev.DeveloperEvaluation.*;-*.Tests" ^
    -classfilters:"-*.Program;-*.Startup;-*.Migrations.*"
IF ERRORLEVEL 1 EXIT /B 1

ECHO.
ECHO Coverage report generated at %REPORT_DIR%\index.html

ENDLOCAL
