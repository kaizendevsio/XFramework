Write-Host XFramework Run Subsystems Script
Write-Host 
Write-Host Building Service Solutions..
Write-Host ..

Write-Host 
Write-Host Preparing Environment Variables..
Write-Host ..
Set-Variable ASPNETCORE_ENVIRONMENT=Development
Write-Host Preparing Environment Variables.. Done

dotnet "build" "C:\Projects\Net Core\XFramework\XFramework\XFramework.Subsystems\XFramework.Community.sln"
dotnet "build" "C:\Projects\Net Core\XFramework\XFramework\XFramework.Subsystems\XFramework.HealthEssentials.sln"
dotnet "build" "C:\Projects\Net Core\XFramework\XFramework\XFramework.Subsystems\XFramework.IdentityServer.sln"
dotnet "build" "C:\Projects\Net Core\XFramework\XFramework\XFramework.Subsystems\XFramework.Messaging.sln"
dotnet "build" "C:\Projects\Net Core\XFramework\XFramework\XFramework.Subsystems\XFramework.SmsGateway.sln"
dotnet "build" "C:\Projects\Net Core\XFramework\XFramework\XFramework.Subsystems\XFramework.StreamFlow.sln"
dotnet "build" "C:\Projects\Net Core\XFramework\XFramework\XFramework.Subsystems\XFramework.Wallets.sln"

Write-Host Starting Services..
Write-Host ..

dotnet "run" "C:\Projects\Net Core\XFramework\XFramework\XFramework.Subsystems\XFramework.Community.sln"
dotnet "run" "C:\Projects\Net Core\XFramework\XFramework\XFramework.Subsystems\XFramework.HealthEssentials.sln"
dotnet "run" "C:\Projects\Net Core\XFramework\XFramework\XFramework.Subsystems\XFramework.IdentityServer.sln"
dotnet "run" "C:\Projects\Net Core\XFramework\XFramework\XFramework.Subsystems\XFramework.Messaging.sln"
dotnet "run" "C:\Projects\Net Core\XFramework\XFramework\XFramework.Subsystems\XFramework.SmsGateway.sln"
dotnet "run" "C:\Projects\Net Core\XFramework\XFramework\XFramework.Subsystems\XFramework.StreamFlow.sln"
dotnet "run" "C:\Projects\Net Core\XFramework\XFramework\XFramework.Subsystems\XFramework.Wallets.sln"

Write-Host ..
Write-Host Services Started Successfully
Write-Host ..

pause
