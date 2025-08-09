var builder = DistributedApplication.CreateBuilder(args);

var pg = builder.AddPostgres("postgres")
    .WithDataVolume(isReadOnly: false);

var calamusDb = pg.AddDatabase("calamus-db");

builder.AddProject<Projects.Calamus_Bot>("bot-service")
    .WithReference(calamusDb).WaitFor(calamusDb);

builder.Build().Run();