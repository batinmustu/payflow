using Microsoft.EntityFrameworkCore;
using PayFlow.Identity.API.Endpoints;
using PayFlow.Identity.Application;
using PayFlow.Identity.Infrastructure;
using PayFlow.Identity.Infrastructure.Persistence;
using PayFlow.Observability;

const string ServiceName = "payflow-identity";

var builder = WebApplication.CreateBuilder(args);

builder.AddPayFlowObservability(ServiceName);
builder.Services.AddPayFlowIdentityApplication();
builder.Services.AddPayFlowIdentityInfrastructure(builder.Configuration);

builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();

var app = builder.Build();

// Dev convenience — apply migrations on startup so a clean clone needs no
// out-of-band `dotnet ef database update`. Production deployments would
// run migrations through the release pipeline instead.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    await db.Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapHealthChecks("/health");
app.MapTenantEndpoints();
app.MapAuthEndpoints();

app.Run();
