using System.Text.Json.Serialization;
using Darts.Api.Endpoints;
using Darts.Api.Hubs;
using Darts.Application;
using Darts.Application.Common.Dispatch;
using Darts.Application.Common.Interfaces.Services;
using Darts.Infrastructure;
using Darts.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
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
    scope.ServiceProvider.GetRequiredService<DartsDbContext>().Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGameEndpoints();
app.MapMatchEndpoints();
app.MapDetectionEndpoints();
app.MapPlayerEndpoints();
app.MapHub<GameHub>("/hubs/game");

app.Run();

public partial class Program;
