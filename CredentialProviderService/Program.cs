using CredentialProviderAPP;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "CredentialProviderMFA";
});
builder.Services.AddHostedService<ServerWorker>();

var host = builder.Build();
host.Run();