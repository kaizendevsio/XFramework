Write-Host XFramework Auto Deployment Script
Write-Host Version 4.1.0
Write-Host 

$currentDirectoryName = Split-Path -Path (Get-Location) -Leaf
$currentDirectoryPath = Split-Path -Path (Get-Location)
$answer = Read-Host "Build dependencies for" $currentDirectoryName"? (y -Yes / n -No)"

if($answer -eq "y"){
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



   Write-Host Building Dependencies Completed Successfully.
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