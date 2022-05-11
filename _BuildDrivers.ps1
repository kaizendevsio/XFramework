Write-Host XFramework Auto Deployment Script
Write-Host Version 4.1.0
Write-Host 

$currentDirectoryName = Split-Path -Path (Get-Location) -Leaf
$currentDirectoryPath = Split-Path -Path (Get-Location)
$answer = Read-Host "Build Domain Generics of " $currentDirectoryName"? (y -Yes / n -No)"

if ($answer -eq "y") {
   Write-Host XFramework Technologies. MIT License 2022
   Write-Host XFramework Auto Deployment Script
   Write-Host Version 4.1.0
   Write-Host



   Write-Host =============== Building Domain Generics ===============
   Write-Host
 
   Write-Host Building XFramework.SmsGateway.Integration ...
   Get-ChildItem -Path "XFramework\XFramework.Subsystems\XFramework.SmsGateway\SmsGateway.Integration\bin" -Exclude ".git" | Remove-Item -Recurse -Force
   Get-ChildItem -Path "XFramework\XFramework.Subsystems\XFramework.SmsGateway\SmsGateway.Integration\obj" -Exclude ".git" | Remove-Item -Recurse -Force
   dotnet build "XFramework\XFramework.Subsystems\XFramework.SmsGateway\SmsGateway.Integration\SmsGateway.Integration.csproj"

   Write-Host Building XFramework.Community.Integration ...
   Get-ChildItem -Path "XFramework\XFramework.Subsystems\XFramework.Community\Community.Integration\bin" -Exclude ".git" | Remove-Item -Recurse -Force
   Get-ChildItem -Path "XFramework\XFramework.Subsystems\XFramework.Community\Community.Integration\obj" -Exclude ".git" | Remove-Item -Recurse -Force
   dotnet build "XFramework\XFramework.Subsystems\XFramework.Community\Community.Integration\Community.Integration.csproj"

   Write-Host Building XFramework.IdentityServer.Integration ...
   Get-ChildItem -Path "XFramework\XFramework.Subsystems\XFramework.IdentityServer\IdentityServer.Integration\bin" -Exclude ".git" | Remove-Item -Recurse -Force
   Get-ChildItem -Path "XFramework\XFramework.Subsystems\XFramework.IdentityServer\IdentityServer.Integration\obj" -Exclude ".git" | Remove-Item -Recurse -Force
   dotnet build "XFramework\XFramework.Subsystems\XFramework.IdentityServer\IdentityServer.Integration\IdentityServer.Integration.csproj"

   Write-Host Building XFramework.HealthEssentials.Integration ...
   Get-ChildItem -Path "XFramework\XFramework.Subsystems\XFramework.HealthEssentials\HealthEssentials.Integration\bin" -Exclude ".git" | Remove-Item -Recurse -Force
   Get-ChildItem -Path "XFramework\XFramework.Subsystems\XFramework.HealthEssentials\HealthEssentials.Integration\obj" -Exclude ".git" | Remove-Item -Recurse -Force
   dotnet build "XFramework\XFramework.Subsystems\XFramework.HealthEssentials\HealthEssentials.Integration\HealthEssentials.Integration.csproj"


   Write-Host Build Completed Successfully.
   Write-Host
   Set-Location $currentDirectoryPath"\"$currentDirectoryName
   pause
   exit
}
else {
   Write-Host
   Write-Host Build Aborted
   Write-Host

   pause
   exit
}