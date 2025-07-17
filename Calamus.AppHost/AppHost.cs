var builder = DistributedApplication.CreateBuilder(args);

var pg = builder.AddPostgres("postgres")
    .WithDataVolume(isReadOnly: false);

var calamusDb = pg.AddDatabase("calamus-db");

builder.AddProject<Projects.Calamus_Bot>("bot-service")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT",
        Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development")
    .WithEnvironment("DOTNET_ENVIRONMENT", Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Development")
    .WithReference(calamusDb).WaitFor(calamusDb);

builder.Build().Run();