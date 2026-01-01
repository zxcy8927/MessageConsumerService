using MessageConsumerService;
using MessageConsumerService.Repository;
using MessageConsumerService.Services;

var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureServices((context, services) =>
{
    services.AddHostedService<Worker>();

    services.AddSingleton<MessageQueueRepository>();
    services.AddSingleton<MessageHandler>();
});

await builder.Build().RunAsync();

