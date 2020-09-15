@ECHO OFF

dotnet new tool-manifest --force
dotnet tool install gettext.net.extractor

PUSHD %~p0
FOR /D %%M IN (.\..\*) DO (
	dotnet gettext.extractor -s %%M -t .\%%~nxM\%%~nxM.pot
)
POPD
IF "%~1"=="" PAUSE
