@SETLOCAL
@SETLOCAL ENABLEDELAYEDEXPANSION
@REM //////////////////////////////////////////////////////
SET BuildVersion=1.2.9
SET PrereleaseVersion=
@REM SET PrereleaseVersion TO EMPTY VALUE FOR THE OFFICIAL RELEASE.
@REM SET PrereleaseVersion TO "auto" FOR AUTOMATIC PRERELEASE NAME "alpha<date and time><last commit hash>"
@REM \\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\
@SET "params=;%~1;%~2;%~3;%~4;%~5;%~6;%~7;%~8;%~9;"

IF /I "!PrereleaseVersion!" EQU "auto" (
	@REM If the script parameters contain "/RESTORE" switch: Set the PrereleaseVersion to "dev".
	IF "!params:;/RESTORE;=;!" NEQ "!params!" (
		SET "PrereleaseVersion=dev"
	) ELSE (
		SET "PrereleaseVersion=alpha"
		FOR /f "delims=" %%A IN ('powershell get-date -format "{yyMMddHHmm}"') DO SET "PrereleaseVersion=!PrereleaseVersion!%%A"
		FOR /f "delims=" %%A IN ('git rev-parse --short HEAD') DO SET "PrereleaseVersion=!PrereleaseVersion!%%A"
		@REM NuGet limits to 20 characters.
		SET "PrereleaseVersion=!PrereleaseVersion:~0,20!"
	)
)

CALL  ..\..\Rhetos\ChangeRhetosPackageVersion.bat . %BuildVersion% !PrereleaseVersion! || EXIT /B 1
