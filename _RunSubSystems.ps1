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

Start-Job -ScriptBlock {dotnet run --project "C:\Projects\Net Core\XFramework\XFramework\XFramework.Subsystems\XFramework.StreamFlow\StreamFlow.Stream\StreamFlow.Stream.csproj"}
Start-Job -ScriptBlock {dotnet run --project "C:\Projects\Net Core\XFramework\XFramework\XFramework.Subsystems\XFramework.IdentityServer\IdentityServer.Api\IdentityServer.Api.csproj"}
Start-Job -ScriptBlock {dotnet run --project "C:\Projects\Net Core\XFramework\XFramework\XFramework.Subsystems\XFramework.Wallets\Wallets.Api\Wallets.Api.csproj"}
Start-Job -ScriptBlock {dotnet run --project "C:\Projects\Net Core\XFramework\XFramework\XFramework.Subsystems\XFramework.Messaging\Messaging.Api\Messaging.Api\csproj.Api\Messaging.Api.csproj"}
Start-Job -ScriptBlock {dotnet run --project "C:\Projects\Net Core\XFramework\XFramework\XFramework.Subsystems\XFramework.SmsGateway\SmsGateway.Api\SmsGateway.Api.csproj"}
Start-Job -ScriptBlock {dotnet run --project "C:\Projects\Net Core\XFramework\XFramework\XFramework.Subsystems\XFramework.HealthEssentials\HealthEssentials.Api\HealthEssentials.Api.csproj"}
Start-Job -ScriptBlock {dotnet run --project "C:\Projects\Net Core\XFramework\XFramework\XFramework.Subsystems\XFramework.Community.sln"}

Write-Host ..
Write-Host Services Started Successfully
Write-Host ..

Write-Host Press any key to stop all services..
pause

Write-Host Stopping Services..
Write-Host ..

Stop-Job -State Running
Write-Host Stopping Services.. Done

Get-Job

Write-Host Clearing Jobs..
Write-Host ..

Get-Job | Remove-Job
Write-Host Clearing Jobs.. Done

Write-Host Services Stopped Successfully
