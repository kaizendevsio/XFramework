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


   Write-Host =============== Building Server-Side Solutions ===============
   Write-Host

   Write-Host Building Identity Server Solution ...
   Write-Host

   Write-Host Building Release.PaymentGateways ...
   dotnet publish "C:\Projects\Net Core\LoadManna\LoadManna\LoadManna.Subsystems\LoadManna.PaymentGateways\PaymentGateways.Api\PaymentGateways.Api.csproj" -o C:\Projects\Published\Release.LoadManna\LoadManna.PaymentGateways.Published -c "Release"

   Write-Host Building Release.IdentityServer ...
   dotnet publish "C:\Projects\Net Core\LoadManna\LoadManna\LoadManna.Subsystems\LoadManna.IdentityServer\IdentityServer.Api\IdentityServer.Api.csproj" -o C:\Projects\Published\Release.LoadManna\LoadManna.IdentityServer.Published -c "Release"

   Write-Host Building Release.BillsPayment ...
   dotnet publish "C:\Projects\Net Core\LoadManna\LoadManna\LoadManna.Subsystems\LoadManna.BillsPayment\BillsPayment.Api\BillsPayment.Api.csproj" -o C:\Projects\Published\Release.LoadManna\LoadManna.BillsPayment.Published -c "Release"

   Write-Host Building Release.StreamFlow ...
   dotnet publish "C:\Projects\Net Core\LoadManna\LoadManna\LoadManna.Subsystems\LoadManna.StreamFlow\StreamFlow.Stream\StreamFlow.Stream.csproj" -o C:\Projects\Published\Release.LoadManna\LoadManna.StreamFlow.Published -c "Release"

   Write-Host Building Release.ELoading ...
   dotnet publish "C:\Projects\Net Core\LoadManna\LoadManna\LoadManna.Subsystems\LoadManna.ELoading\ELoading.Api\ELoading.Api.csproj" -o C:\Projects\Published\Release.LoadManna\LoadManna.ELoading.Published -c "Release"

   Write-Host Building Release.CentralServer ...
   dotnet publish "C:\Projects\Net Core\LoadManna\LoadManna\LoadManna\Server\LoadManna.Server\LoadManna.Api\LoadManna.Api.csproj" -o C:\Projects\Published\Release.LoadManna\LoadManna.CentralServer.Published -c "Release"

   Write-Host Building Release.SynchronizationServer ...
   dotnet publish "C:\Projects\Net Core\LoadManna\LoadManna\LoadManna.Subsystems\LoadManna.SynchronizationServer\SynchronizationServer.Api\SynchronizationServer.Api.csproj" -o C:\Projects\Published\Release.LoadManna\LoadManna.SynchronizationServer.Published -c "Release"

   Write-Host Building Release.Wallets ...
   dotnet publish "C:\Projects\Net Core\LoadManna\LoadManna\LoadManna.Subsystems\LoadManna.Wallets\Wallets.Api\Wallets.Api.csproj" -o C:\Projects\Published\Release.LoadManna\LoadManna.Wallets.Published -c "Release"

   Write-Host Building Release.PaymentGateways ...
   dotnet publish "C:\Projects\Net Core\LoadManna\LoadManna\LoadManna.Subsystems\LoadManna.PaymentGateways\PaymentGateways.Api\PaymentGateways.Api.csproj" -o C:\Projects\Published\Release.LoadManna\LoadManna.PaymentGateways.Published -c "Release"

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