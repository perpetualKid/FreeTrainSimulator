@ECHO OFF
REM Script must be run from Locales directory.

FOR /D %%M IN (.\*) DO (
	FOR %%L IN (%%M\*.mo) DO (
		md .\..\..\Program\Locales\%%~nL
		copy /Y %%L .\..\..\Program\Locales\%%~nL\%%~nxM.mo
))

IF "%~1"=="" PAUSE
