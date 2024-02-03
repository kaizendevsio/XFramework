Write-Host XFramework Run Subsystems Script
Write-Host 
Write-Host Building Service Solutions..
Write-Host ..

Write-Host 
Write-Host Preparing Environment Variables..
Write-Host ..
Set-Variable ASPNETCORE_ENVIRONMENT=Development
Write-Host Preparing Environment Variables.. Done

dotnet "build" "XFramework\XFramework.Subsystems\XFramework.Community.sln"
dotnet "build" "XFramework\XFramework.Subsystems\XFramework.HealthEssentials.sln"
dotnet "build" "XFramework\XFramework.Subsystems\XFramework.IdentityServer.sln"
dotnet "build" "XFramework\XFramework.Subsystems\XFramework.Messaging.sln"
dotnet "build" "XFramework\XFramework.Subsystems\XFramework.SmsGateway.sln"
dotnet "build" "XFramework\XFramework.Subsystems\XFramework.StreamFlow.sln"
dotnet "build" "XFramework\XFramework.Subsystems\XFramework.Wallets.sln"

Write-Host Starting Services..
Write-Host ..

Start-Process powershell { dotnet run --project 'XFramework\XFramework.Subsystems\XFramework.IdentityServer\IdentityServer.Api\IdentityServer.Api.csproj' } #-WindowStyle Minimized
Start-Process powershell { dotnet run --project 'XFramework\XFramework.Subsystems\XFramework.Wallets\Wallets.Api\Wallets.Api.csproj' } #-WindowStyle Minimized
Start-Process powershell { dotnet run --project 'XFramework\XFramework.Subsystems\XFramework.Messaging\Messaging.Api\Messaging.Api.csproj' } #-WindowStyle Minimized
Start-Process powershell { dotnet run --project 'XFramework\XFramework.Subsystems\XFramework.SmsGateway\SmsGateway.Api\SmsGateway.Api.csproj' } #-WindowStyle Minimized
Start-Process powershell { dotnet run --project 'XFramework\XFramework.Subsystems\XFramework.Community\Community.Api\Community.Api.csproj' } #-WindowStyle Minimized
# Start-Process powershell { dotnet run --project 'XFramework\XFramework.Subsystems\XFramework.HealthEssentials\HealthEssentials.Api\HealthEssentials.Api.csproj' } #-WindowStyle Minimized
Start-Process powershell { dotnet run --project 'XFramework\XFramework.Subsystems\XFramework.StreamFlow\StreamFlow.Stream\StreamFlow.Stream.csproj' } -NoNewWindow -Wait

Write-Host ..
Write-Host Services Started Successfully