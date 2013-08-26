cd %3
echo %3
del  %3*.nupkg
..\..\..\..\.nuget\AssemblyVersionFinder.exe -s -d %2 > tmpFile
set /p version=<tmpFile

IF %4==Nuget (
	FOR %%X in ("%2nuget\*.nuspec") DO (
	..\..\..\..\.nuget\nuget.exe pack %%X -Version %version%
	)

	FOR %%X in ("%3*.nupkg") DO (
	..\..\..\..\.nuget\nuget.exe push %%X -s %1 XgjOkxvZaaHlvOQXfi7r9WDfOKsjRhnZ
	)
)