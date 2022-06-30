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
 
   Write-Host Building StreamFlow.Community ...
   dotnet build "XFramework\XFramework.Subsystems\XFramework.Community\Community.Api\Community.Api.csproj"

   Write-Host Building IdentityServer.Domain.Generic ...
   dotnet build "XFramework\XFramework.Subsystems\XFramework.StreamFlow\StreamFlow.Api\StreamFlow.Api.csproj"

   Write-Host Building Wallets.IdentityServer ...
   dotnet build "XFramework\XFramework.Subsystems\XFramework.IdentityServer\IdentityServer.Api\IdentityServer.Api.csproj"

   Write-Host Building XFramework.PaymentGateways ...
   dotnet build "XFramework\XFramework.Subsystems\XFramework.PaymentGateways\PaymentGateways.Api\PaymentGateways.Api.csproj"

   Write-Host Building XFramework.Records ...
   dotnet build "XFramework\XFramework.Subsystems\XFramework.Records\Records.Api\Records.Api.csproj"

   Write-Host Building XFramework.Wallets ...
   dotnet build "XFramework\XFramework.Subsystems\XFramework.Wallets\Wallets.Api\Wallets.Api.csproj"

   Write-Host Building XFramework.Coins ...
   dotnet build "XFramework\XFramework.Subsystems\XFramework.Coins\Coins.Api\Coins.Api.csproj"


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