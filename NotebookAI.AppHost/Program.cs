var builder = DistributedApplication.CreateBuilder(args);

// Add the server project
var server = builder.AddProject<Projects.NotebookAI_Server>("notebookai-server");

// Add the Angular client as an npm app
builder.AddNpmApp("notebookai-client", "../notebookai.client")
    .WithReference(server)
    .WithEnvironment("BROWSER", "none") // Prevents auto-opening browser
    // Pin a stable dev port so Auth0 Allowed Callback URLs can be configured once
    .WithHttpEndpoint(env: "PORT", port: 50012);

builder.Build().Run();
