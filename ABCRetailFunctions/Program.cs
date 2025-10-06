using ABCRetailFunctions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // Register storage services
        var storageConnectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage")
            ?? throw new InvalidOperationException("storageConnectionString not found");

        services.AddSingleton(sp => new TableStorageService(storageConnectionString, "tables"));
        services.AddSingleton(sp => new BlobStorageService(storageConnectionString, "blobs"));
        services.AddSingleton(sp => new QueueStorageService(storageConnectionString, "audit-queue"));
        services.AddSingleton(sp => new FileShareStorageService(storageConnectionString, "retail-fileshare"));
    })
    .Build();

host.Run();