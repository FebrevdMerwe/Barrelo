using System.Text.Json.Serialization;
using Barrelo.Api.Endpoints;
using Barrelo.Api.Hubs;
using Barrelo.Application;
using Barrelo.Application.Common.Dispatch;
using Barrelo.Application.Common.Interfaces.Services;
using Barrelo.Infrastructure;
using Barrelo.Infrastructure.External.GamePlugins;
using Barrelo.Infrastructure.Persistence;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddDartsDispatcher();
builder.Services.AddOpenApi();
builder.Services.AddSignalR().AddJsonProtocol(options =>
    options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddScoped<IGameNotifier, GameHubNotifier>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<BarreloDbContext>().Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();

var pluginsDirectory = PluginsDirectoryResolver.Resolve(builder.Configuration);
if (Directory.Exists(pluginsDirectory))
{
    var pluginContentTypes = new FileExtensionContentTypeProvider();
    pluginContentTypes.Mappings.Clear();
    pluginContentTypes.Mappings[".js"] = "text/javascript";
    pluginContentTypes.Mappings[".css"] = "text/css";
    // .html + these four cover the out-of-process game UI convention (an iframe'd ui/index.html) — the
    // last three specifically so a Unity WebGL build's output (.wasm/.data/.framework.js/.loader.js) serves.
    pluginContentTypes.Mappings[".html"] = "text/html";
    pluginContentTypes.Mappings[".wasm"] = "application/wasm";
    pluginContentTypes.Mappings[".data"] = "application/octet-stream";
    pluginContentTypes.Mappings[".json"] = "application/json";

    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(pluginsDirectory),
        RequestPath = "/plugins",
        ContentTypeProvider = pluginContentTypes,
        ServeUnknownFileTypes = false,
    });
}

app.MapGameEndpoints();
app.MapMatchEndpoints();
app.MapSessionEndpoints();
app.MapDetectionEndpoints();
app.MapPlayerEndpoints();
app.MapLeaderboardEndpoints();
app.MapHub<GameHub>("/hubs/game");

app.Run();

public partial class Program;
