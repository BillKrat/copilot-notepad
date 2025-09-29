var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.CopilotNotepad_ApiService>("apiservice");

// Add the Angular app as a static web app
var angularApp = builder.AddNpmApp("webapp", "../CopilotNotepad.Web")
    .WithReference(apiService)
    .WithExternalHttpEndpoints();

builder.Build().Run();
