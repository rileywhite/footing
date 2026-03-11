var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.Footing>("footing");

builder.Build().Run();
