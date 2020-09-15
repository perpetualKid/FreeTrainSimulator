@ECHO OFF

PUSHD %~p0
FOR /D %%M IN (.\*) DO (
	FOR %%L IN (%%M\*.mo) DO (
		md .\..\..\Program\Locales\%%~nL
		copy /Y %%L .\..\..\Program\Locales\%%~nL\%%~nxM.mo
))
POPD

IF "%~1"=="" PAUSE
