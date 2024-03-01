param(
    [string]$action = "start", # Default action is "start". Pass "stop" or "status" or "logs" to perform respective actions.
    [string]$serviceName = "" # Used with "logs" action to specify which service's logs to view.
)

# Initialize global storage for service PIDs and log paths
if (-not (Test-Path variable:global:servicePIDs)) {
    $global:servicePIDs = @{}
}
if (-not (Test-Path variable:global:serviceLogs)) {
    $global:serviceLogs = @{}
}

# Function to start services with logging
function Start-Services {
    param(
        [string]$pathSeparator
    )

    $logDir = "ServiceLogs"
    if (-not (Test-Path $logDir)) {
        New-Item -ItemType Directory -Path $logDir | Out-Null
    }

    $projects = @(
	"XFramework${pathSeparator}XFramework.Subsystems${pathSeparator}XFramework.IdentityServer${pathSeparator}IdentityServer.Api${pathSeparator}IdentityServer.Api.csproj",
    	"XFramework${pathSeparator}XFramework.Subsystems${pathSeparator}XFramework.Wallets${pathSeparator}Wallets.Api${pathSeparator}Wallets.Api.csproj",
    	"XFramework${pathSeparator}XFramework.Subsystems${pathSeparator}XFramework.Messaging${pathSeparator}Messaging.Api${pathSeparator}Messaging.Api.csproj",
    	"XFramework${pathSeparator}XFramework.Subsystems${pathSeparator}XFramework.SmsGateway${pathSeparator}SmsGateway.Api${pathSeparator}SmsGateway.Api.csproj",
    	"XFramework${pathSeparator}XFramework.Subsystems${pathSeparator}XFramework.Community${pathSeparator}Community.Api${pathSeparator}Community.Api.csproj",
    	"XFramework${pathSeparator}XFramework.Subsystems${pathSeparator}XFramework.StreamFlow${pathSeparator}StreamFlow.Stream${pathSeparator}StreamFlow.Stream.csproj"
    	# Add or remove projects as necessary
    )

    foreach ($project in $projects) {
        $logPath = Join-Path $logDir "$($project.Replace($pathSeparator, '_')).log"
        $scriptBlock = {
	    param($projectPath, $logFilePath)
	    "Starting project at $(Get-Date): $projectPath" | Out-File $logFilePath -Append
	    dotnet run --project $projectPath >> $logFilePath 2>&1
	    "Project ended at $(Get-Date): $projectPath" | Out-File $logFilePath -Append
 
	   }
        Start-Job -ScriptBlock $scriptBlock -ArgumentList $project, $logPath -Name $project.Replace($pathSeparator, '_')
    }

    Write-Host "Services started as background jobs successfully."
}

# Function to stop services
function Stop-Services {
    param(
        [string]$serviceName = ""
    )

    if ([string]::IsNullOrEmpty($serviceName)) {
        # Stop and remove all jobs if no specific service name is provided
        Get-Job | Stop-Job
        Get-Job | Remove-Job
        Write-Host "All services stopped and jobs removed."
    }
    else {
        # Identify the job by its name and stop & remove it
        $jobName = $serviceName.Replace($pathSeparator, '_')
        $job = Get-Job -Name $jobName -ErrorAction SilentlyContinue
        if ($null -ne $job) {
            Stop-Job -Name $jobName
            Remove-Job -Name $jobName
            Write-Host "Service $serviceName stopped and job removed."
        }
        else {
            Write-Host "No job found for $serviceName. Make sure the service name is correct."
        }
    }
}



# Function to check service status
function Check-ServiceStatus {
    Get-Job | Format-Table Id, Name, State -AutoSize
}


# Function to view logs for a specific service
function View-Logs {
    param(
        [string]$serviceName
    )

    $logPath = Join-Path "ServiceLogs" "$($serviceName.Replace("\", '_')).log"
    if (Test-Path $logPath) {
        Get-Content $logPath -Tail 100 # Adjust the number of lines as needed
    } else {
        Write-Host "Log file not found for $serviceName"
    }
}


# Main script logic
$pathSeparator = if ($IsWindows) { "\" } else { "/" }

switch ($action.ToLower()) {
    "start" {
        Start-Services -pathSeparator $pathSeparator
    }
    "stop" {
        Stop-Services -serviceName $serviceName
    }
    "status" {
        Check-ServiceStatus
    }
    "logs" {
        if (-not [string]::IsNullOrEmpty($serviceName)) {
            View-Logs -serviceName $serviceName
        } else {
            Write-Host "Please specify a service name to view its logs."
        }
    }
    default {
        Write-Host "Invalid action. Please specify 'start', 'stop', 'status', or 'logs'."
    }
}


