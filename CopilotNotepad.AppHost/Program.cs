var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.CopilotNotepad_ApiService>("apiservice");

builder.Build().Run();
