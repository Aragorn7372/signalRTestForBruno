using signalRTestForBruno.Hub;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();

var app = builder.Build();

app.MapGet("/", () => "SignalR server OK");

app.MapHub<TestHub>("/hub");

await app.RunAsync();


