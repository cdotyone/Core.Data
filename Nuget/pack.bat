cd %2
echo %2
del  %2*.nupkg
SET PATH=%4.nuget;%PATH%
AssemblyVersionFinder.exe -s -d %4\references > tmpFile
set /p version=<tmpFile
set nugetconfig=%4.nuget\nuget.config
echo %nugetconfig%
IF %3==Nuget (
	FOR %%X in ("%1nuget\*.nuspec") DO (
		ECHO nuget.exe pack %%X -Version %version%
		nuget.exe pack %%X -Version %version%
	)

	FOR %%X in ("%2*.nupkg") DO (
		ECHO nuget.exe push %%X -Config %nugetconfig%
		nuget.exe push %%X -Config %nugetconfig%
	)
)