Write-Host XFramework Auto Deployment Script
Write-Host Version 4.1.0
Write-Host 

$currentDirectoryName = Split-Path -Path (Get-Location) -Leaf
$currentDirectoryPath = Split-Path -Path (Get-Location)
$answer = Read-Host "Build Domain Generics of " $currentDirectoryName"? (y -Yes / n -No)"

if($answer -eq "y"){
   Write-Host XFramework Technologies. MIT License 2022
   Write-Host XFramework Auto Deployment Script
   Write-Host Version 4.1.0
   Write-Host



   Write-Host =============== Building Domain Generics ===============
   Write-Host
 
   Write-Host Building XFramework.Domain.Generic ...
   dotnet build "XFramework\XFramework\Server\XFramework.Domain.Generic\XFramework.Domain.Generic.csproj"
   
   Write-Host Building StreamFlow.Domain.Generic ...
   dotnet build "XFramework\XFramework.Subsystems\XFramework.StreamFlow\StreamFlow.Domain.Generic\StreamFlow.Domain.Generic.csproj"

   Write-Host Building IdentityServer.Domain.Generic ...
   dotnet build "XFramework\XFramework.Subsystems\XFramework.IdentityServer\IdentityServer.Domain.Generic\IdentityServer.Domain.Generic.csproj"

   Write-Host Building Wallets.Domain.Generic ...
   dotnet build "XFramework\XFramework.Subsystems\XFramework.Wallets\Wallets.Domain.Generic\Wallets.Domain.Generic.csproj"

   Write-Host Building XFramework.Client.Shared ...
   dotnet build "XFramework\XFramework\Client\XFramework.Client.Shared\XFramework.Client.Shared.csproj"

   Write-Host Building XFramework.Integrations ...
   dotnet build "XFramework\XFramework\Server\XFramework.Integration\XFramework.Integration.csproj"


   Write-Host Build Completed Successfully.
   Write-Host
   Set-Location $currentDirectoryPath"\"$currentDirectoryName
   pause
   exit
}
else{
   Write-Host
   Write-Host Build Aborted
   Write-Host

   pause
exit
}