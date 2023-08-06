using System.Diagnostics;
using HealthEssentials.Core.DataAccess.Commands.Entity.Medicine;
using HealthEssentials.Domain.Contracts.Responses;
using Microsoft.Extensions.Hosting;
using RestSharp;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Medicine;

public class InitializeMedicineListHandler : CommandBaseHandler, IRequestHandler<InitializeMedicineListCmd, CmdResponse>
{
    private readonly IHostApplicationLifetime _hostApplicationLifetime;

    public InitializeMedicineListHandler(IDataLayer dataLayer, IHostApplicationLifetime hostApplicationLifetime)
    {
        _hostApplicationLifetime = hostApplicationLifetime;
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse> Handle(InitializeMedicineListCmd request, CancellationToken cancellationToken)
    {
        Console.WriteLine("Checking if medicine list is initialized");
        var anyMedicines = _dataLayer.HealthEssentialsContext.Medicines.Any();
        if (anyMedicines) return new();

        var tasks = new List<Task>();

        Console.WriteLine("Initializing medicine list");
        
        const string characterSet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        foreach (var t in characterSet)
        {
            Console.WriteLine($"Initializing medicine list for {t}");
            var sw = new Stopwatch();
            sw.Start();
            var retryCounter = 0;
            
            var client = new RestClient();
            var restRequest = new RestRequest("https://dailymed.nlm.nih.gov/dailymed/autocomplete.cfm", Method.Get)
                .AddParameter("key", "search")
                .AddParameter("returntype", "json")
                .AddParameter("term", $"{t}");
        
            retry:
            var response = await client.ExecuteAsync<List<DailyMedAutoCompleteResponse>>(restRequest, CancellationToken.None);
            if (!response.IsSuccessStatusCode)
            {
                if (retryCounter >= 3) throw new Exception("Failed to initialize medicine list");
                retryCounter++;
                goto retry;
            }
            
            var list = response.Data.Select(o => new Domain.Generics.Contracts.Medicine
            {
                EntityId = 1,
                Name = o.Value,
                Description = o.Value,
            }).ToList();

            await _dataLayer.HealthEssentialsContext.Medicines.AddRangeAsync(list, CancellationToken.None);
            sw.Stop();
            
            Console.WriteLine($"Initialized medicine list for {t} in {sw.ElapsedMilliseconds:n0}ms");
        }

        Console.WriteLine("Saving medicine list");
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        Console.WriteLine("Medicine list initialized");
        
        _hostApplicationLifetime.StopApplication();

        Console.WriteLine("Please restart the application");
        return new();
    }
}