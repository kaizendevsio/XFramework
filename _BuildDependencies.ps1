Write-Host XFramework Auto Deployment Script
Write-Host Version 4.1.0
Write-Host 

$currentDirectoryName = Split-Path -Path (Get-Location) -Leaf
$currentDirectoryPath = Split-Path -Path (Get-Location)
$answer = Read-Host "Build Dependencies For" $currentDirectoryName"? (y -Yes / n -No)"

if ($answer -eq "y") {
   Write-Host XFramework Technologies. MIT License 2021
   Write-Host XFramework Auto Deployment Script
   Write-Host Version 4.1.0
   Write-Host


   Write-Host =============== Building Dependencies ===============
   Write-Host

  
   Write-Host Building Release.IdentityServer ...
   dotnet build "XFramework\XFramework.Subsystems\XFramework.IdentityServer\IdentityServer.Domain.Generic\IdentityServer.Domain.Generic.csproj"

   Write-Host Building Release.StreamFlow ...
   dotnet build "XFramework\XFramework.Subsystems\XFramework.StreamFlow\StreamFlow.Domain.Generic\StreamFlow.Domain.Generic.csproj"

   Write-Host Building Release.CentralServer ...
   dotnet build "XFramework\XFramework\Server\XFramework.Domain.Generic\XFramework.Domain.Generic.csproj"

   Write-Host Building Release.Wallets ...
   dotnet build "XFramework\XFramework.Subsystems\XFramework.Wallets\Wallets.Domain.Generic\Wallets.Domain.Generic.csproj"
   
   Write-Host Building Release.Community ...
   dotnet build "XFramework\XFramework.Subsystems\XFramework.Community\Community.Domain.Generic\Community.Domain.Generic.csproj"
  
   Write-Host Building Release.Messaging ...
   dotnet build "XFramework\XFramework.Subsystems\XFramework.Messaging\Messaging.Domain.Generic\Messaging.Domain.Generic.csproj"
  
   Write-Host Building Release.SmsGateway ...
   dotnet build "XFramework\XFramework.Subsystems\XFramework.SmsGateway\SmsGateway.Domain.Generic\SmsGateway.Domain.Generic.csproj"

   Write-Host Building XFramework.Integration ...
   dotnet build "XFramework\XFramework\Server\XFramework.Integration\XFramework.Integration.csproj"

   Write-Host Building XFramework.Client.Shared ...
   dotnet build "XFramework\XFramework\Client\XFramework.Client.Shared\XFramework.Client.Shared.csproj"
   
   Write-Host Building XFramework.Server.Domain.Generic ...
   dotnet build "XFramework\XFramework\Server\XFramework.Domain.Generic\XFramework.Domain.Generic.csproj"

   Write-Host Building Dependencies Completed Successfully.
   Write-Host
   Set-Location $currentDirectoryPath"\"$currentDirectoryName
   
   exit
}
else {
   Write-Host
   Write-Host Deployment Aborted
   Write-Host

   exit
}