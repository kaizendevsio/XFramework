@ECHO OFF

echo XFramework Auto Deployment Script
echo Version 4.1.0
echo.

for %%I in (.) do set CurrDirName=%%~nxI

:choice
set /P c=Build And Deploy %CurrDirName%?[Y/N]
if /I "%c%" EQU "Y" goto :somewhere
if /I "%c%" EQU "N" goto :somewhere_else
goto :choice


:somewhere

cls

set STARTTIME=%TIME%

echo (c) XFramework Technologies. MIT License 2021
echo XFramework Auto Deployment Script
echo Version 4.1.0
echo.

echo =============== Building Server-Side Solutions ===============
echo.

echo Building Release.IdentityServer ...
dotnet publish "C:\Projects\Net Core\XFramework\XFramework\XFramework.Subsystems\XFramework.IdentityServer\IdentityServer.Api\IdentityServer.Api.csproj" -o C:\Projects\Published\Release.XSystems\XFramework.IdentityServer.Published -c "Release"

echo Building Release.StreamFlow ...
dotnet publish "C:\Projects\Net Core\XFramework\XFramework\XFramework.Subsystems\XFramework.StreamFlow\StreamFlow.Stream\StreamFlow.Stream.csproj" -o C:\Projects\Published\Release.XSystems\XFramework.StreamFlow.Published -c "Release"

echo Building Release.CentralServer ...
dotnet publish "C:\Projects\Net Core\XFramework\XFramework\XFramework\Server\XFramework.Api\XFramework.Api.csproj" -o C:\Projects\Published\Release.XSystems\XFramework.CentralServer.Published -c "Release"


echo Syncing to Github ...
cd C:\Projects\Published\Release.XSystems
git add *.*
git commit -m "One Click Deploy"

git push

set ENDTIME=%TIME%

echo.
echo.
rem Change formatting for the start and end times
    for /F "tokens=1-4 delims=:.," %%a in ("%STARTTIME%") do (
       set /A "start=(((%%a*60)+1%%b %% 100)*60+1%%c %% 100)*100+1%%d %% 100"
    )

    for /F "tokens=1-4 delims=:.," %%a in ("%ENDTIME%") do ( 
       IF %ENDTIME% GTR %STARTTIME% set /A "end=(((%%a*60)+1%%b %% 100)*60+1%%c %% 100)*100+1%%d %% 100" 
       IF %ENDTIME% LSS %STARTTIME% set /A "end=((((%%a+24)*60)+1%%b %% 100)*60+1%%c %% 100)*100+1%%d %% 100" 
    )

    rem Calculate the elapsed time by subtracting values
    set /A elapsed=end-start

    rem Format the results for output
    set /A hh=elapsed/(60*60*100), rest=elapsed%%(60*60*100), mm=rest/(60*100), rest%%=60*100, ss=rest/100, cc=rest%%100
    if %hh% lss 10 set hh=0%hh%
    if %mm% lss 10 set mm=0%mm%
    if %ss% lss 10 set ss=0%ss%
    if %cc% lss 10 set cc=0%cc%

    set DURATION=%hh%:%mm%:%ss%,%cc%

    echo Start    : %STARTTIME%
    echo Finish   : %ENDTIME%
    echo          ---------------
    echo Duration : %DURATION%

echo.
echo Deployment Completed Successfully.
echo.
pause
exit

:somewhere_else

echo.
echo Deployment Aborted
echo.

pause
exit