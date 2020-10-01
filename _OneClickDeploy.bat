@ECHO OFF

for %%I in (.) do set CurrDirName=%%~nxI

:choice
set /P c=Deploy %CurrDirName%?[Y/N]
if /I "%c%" EQU "Y" goto :somewhere
if /I "%c%" EQU "N" goto :somewhere_else
goto :choice


:somewhere

echo Building Release.CooperVision.API ...
dotnet publish CooperVision.Client.sln -o C:\Projects\Published\Release.CooperVision\CooperVision.Mobile.Published


//echo Building Release.CooperVision.FrontEnd ...
//dotnet publish CooperVision.FrontEnd.sln -o C:\Projects\Published\Release.CooperVision\CooperVision.FrontEnd.Published


echo Deploying to git ...
cd C:\Projects\Published\Release.CooperVision
git add *.*
git commit -m "One Click Deploy"


git push


echo Deployment done. Have a good day :)

pause
exit

:somewhere_else

echo Deployment Canceled
pause
exit