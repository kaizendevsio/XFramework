using System;
using System.Net.Http;
using System.Net.Http.Headers;
using NBomber.Contracts;
using NBomber.CSharp;
using NBomber.Plugins.Http.CSharp;
using NBomber.Plugins.Network.Ping;

namespace XFramework.Tests;

public class StreamFlowTest
{
    public static void Run()
    {
        var httpFactory = HttpClientFactory.Create();

        var step = Step.Create("fetch_html_page",
            clientFactory: httpFactory,
            execute: async context =>
            {
                var content = @"{
  ""requestServer"": {
    ""applicationId"": ""3fa85f64-5717-4562-b3fc-2c963f66afa6"",
    ""name"": ""string"",
    ""deviceAgent"": ""string"",
    ""ipAddress"": ""string"",
    ""requestId"": ""3fa85f64-5717-4562-b3fc-2c963f66afa6""
  },
  ""guid"": ""3fa85f64-5717-4562-b3fc-2c963f66afa6"",
  ""generateToken"": true,
  ""username"": ""string"",
  ""password"": ""string"",
  ""remember"": true,
  ""authorizeBy"": 0
}";
                
                var finalContent = new StringContent(content);
                finalContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                var response = await context.Client.PostAsync("https://localhost:65007/Api/v2/Identity/Authenticate", finalContent, context.CancellationToken);

                return response.IsSuccessStatusCode
                    ? Response.Ok(statusCode: (int) response.StatusCode)
                    : Response.Fail(statusCode: (int) response.StatusCode);
            },
            timeout: TimeSpan.FromSeconds(5));

        var scenario = ScenarioBuilder
            .CreateScenario("simple_http", step)
            .WithWarmUpDuration(TimeSpan.FromSeconds(5))
            .WithLoadSimulations(new[]
            {
                Simulation.InjectPerSec(rate: 50, during: TimeSpan.FromSeconds(30))
                //Simulation.KeepConstant(copies: 10, during: TimeSpan.FromSeconds(30))
            });

        var pingPluginConfig = PingPluginConfig.CreateDefault(new[] {"nbomber.com"});
        var pingPlugin = new PingPlugin(pingPluginConfig);

        NBomberRunner
            .RegisterScenarios(scenario)
            .WithWorkerPlugins(pingPlugin)
            .Run();
    }
}