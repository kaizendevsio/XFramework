Write-Host XFramework Auto Deployment Script
Write-Host Version 4.1.0
Write-Host 

$currentDirectoryName = Split-Path -Path (Get-Location) -Leaf
$currentDirectoryPath = Split-Path -Path (Get-Location)
$answer = Read-Host "Build And Deploy" $currentDirectoryName"? (y -Yes / n -No)"

if ($answer -eq "y") {
   Write-Host XFramework Technologies. MIT License 2022
   Write-Host XFramework Auto Deployment Script
   Write-Host Version 4.1.0
   Write-Host


   Write-Host Deleting Previous Release Versions ...
   Write-Host

   Get-ChildItem -Path "C:\Projects\Published\Release.XnelSystems" -Exclude ".git" | Remove-Item -Recurse -Force

   Write-Host =============== Building Server-Side Solutions ===============
   Write-Host

   Write-Host Building Release.IdentityServer ...
   dotnet publish "XFramework\XFramework.Subsystems\XFramework.IdentityServer\IdentityServer.Api\IdentityServer.Api.csproj" -o "C:\Projects\Published\Release.XnelSystems\XnelSystems.IdentityServer.Published" -c "Release"

   Write-Host Building Release.StreamFlow ...
   dotnet publish "XFramework\XFramework.Subsystems\XFramework.StreamFlow\StreamFlow.Stream\StreamFlow.Stream.csproj" -o "C:\Projects\Published\Release.XnelSystems\XnelSystems.StreamFlow.Published" -c "Release"

   Write-Host Building Release.CentralServer ...
   dotnet publish "XFramework\XFramework\Server\XFramework.Api\XFramework.Api.csproj" -o "C:\Projects\Published\Release.XnelSystems\XnelSystems.CentralServer.Published" -c "Release"

   Write-Host Building Release.Wallets ...
   dotnet publish "XFramework\XFramework.Subsystems\XFramework.Wallets\Wallets.Api\Wallets.Api.csproj" -o "C:\Projects\Published\Release.XnelSystems\XnelSystems.Wallets.Published" -c "Release"

   Write-Host Building Release.Community ...
   dotnet publish "XFramework\XFramework.Subsystems\XFramework.Community\Community.Api\Community.Api.csproj" -o "C:\Projects\Published\Release.XnelSystems\XnelSystems.Community.Published" -c "Release"


   Write-Host Syncing to Github ...
   Set-Location "C:\Projects\Published\Release.XnelSystems"

   git branch
   git add *.*
   git commit -m "One Click Deploy"

   git push

   Write-Host Deployment Completed Successfully.
   Write-Host
   Set-Location $currentDirectoryPath"\"$currentDirectoryName
   pause
   exit
}
else {
   Write-Host
   Write-Host Deployment Aborted
   Write-Host

   pause
   exit
}