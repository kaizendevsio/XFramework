namespace XFramework.TestInfrastructure;

public static class TestHostWaiter
{
    public static async Task WaitForHealth(
        string url,
        Task? appTask = null,
        int timeoutSeconds = 30)
    {
        using var client = new HttpClient();
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);

        while (DateTime.UtcNow < deadline)
        {
            ThrowIfCrashed(appTask);

            try
            {
                var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                    return;
            }
            catch
            {
                // The host may not have bound its socket yet.
            }

            await Task.Delay(500);
        }

        ThrowIfCrashed(appTask);
        throw new TimeoutException($"Service at {url} did not become healthy within {timeoutSeconds}s");
    }

    private static void ThrowIfCrashed(Task? appTask)
    {
        if (appTask is { IsFaulted: true })
        {
            throw new InvalidOperationException(
                $"Application crashed during startup: {appTask.Exception?.GetBaseException().Message}",
                appTask.Exception?.GetBaseException());
        }

        if (appTask is { IsCompleted: true })
            throw new InvalidOperationException("Application stopped before the health endpoint became available.");
    }
}
