Write-Host XFramework Auto Deployment Script
Write-Host Version 4.1.0
Write-Host 

$currentDirectoryName = Split-Path -Path (Get-Location) -Leaf
$currentDirectoryPath = Split-Path -Path (Get-Location)
$answer = Read-Host "Build And Deploy" $currentDirectoryName"? (y -Yes / n -No)"

if($answer -eq "y"){
   Write-Host XFramework Technologies. MIT License 2021
   Write-Host XFramework Auto Deployment Script
   Write-Host Version 4.1.0
   Write-Host

   Write-Host =============== Building Client-Side Solution ===============
   Write-Host

   Write-Host Building Release.LoadManna.Client.Shared ...
   dotnet publish "C:\Projects\Net Core\LoadManna\LoadManna\LoadManna\Client\LoadManna.Shared\LoadManna.Shared.csproj" -o C:\Projects\Published\Release.LoadManna\LoadManna.Client.Shared.Published -c "Release"

   Write-Host Building Release.LoadManna.Client.Web ...
   dotnet publish "C:\Projects\Net Core\LoadManna\LoadManna\LoadManna\Client\LoadManna.Web\LoadManna.Web.csproj" -o C:\Projects\Published\Release.LoadManna\LoadManna.Client.Web.Published -c "Release"

   Write-Host Building Release.LoadManna.Client.Mobile ...
   dotnet publish "C:\Projects\Net Core\LoadManna\LoadManna\LoadManna\Client\LoadManna.Mobile\LoadManna.Mobile.csproj" -o C:\Projects\Published\Release.LoadManna\LoadManna.Client.Mobile.Published -c "Release"


   Write-Host Syncing to Github ...
   cd C:\Projects\Published\Release.LoadManna
   git add *.*
   git commit -m "One Click Deploy"

   git push

   Write-Host Deployment Completed Successfully.
   Write-Host
   cd $currentDirectoryPath"\"$currentDirectoryName
   pause
   exit
}
else{
   Write-Host
   Write-Host Deployment Aborted
   Write-Host

   pause
exit
}