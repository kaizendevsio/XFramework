using System.Diagnostics;

namespace XFramework.Client.Shared.Core.Services;

public class IndexedDbService
{
    private IIndexedDbFactory IndexedDbFactory { get; }
    public BlazorIndexedDb Database { get; set; }
    public TaskCompletionSource TaskCompletionSource { get; set; } = new();
    private Stopwatch Stopwatch { get; set; } = new();
    public bool IsInitializing { get; set; }
    
    public IndexedDbService(IIndexedDbFactory indexedDbFactory)
    {
        IndexedDbFactory = indexedDbFactory;
    }

    public async Task InitializeDb()
    {
        Stopwatch.Restart();
        IsInitializing = true;
        Console.WriteLine("Database Initialize Start");
        try
        {
            Database = await IndexedDbFactory.Create<BlazorIndexedDb>();
            TaskCompletionSource.TrySetResult();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
        Stopwatch.Stop();
        Console.WriteLine($"Database Initialized! ({Stopwatch.Elapsed.TotalMilliseconds}ms)");
    }

    public async Task RemoveItem(string stateName)
    {
        var item = Database.StateCache.FirstOrDefault(i => i.Key == stateName);
        if (item is not null)
        {
            Database.StateCache.Remove(item);
            await Database.SaveChanges();
        }
    }
}