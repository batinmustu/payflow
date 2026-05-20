using Microsoft.EntityFrameworkCore;
using PayFlow.Multitenancy;
using PayFlow.Observability;
using PayFlow.Reconciliation.API.Auth;
using PayFlow.Reconciliation.Application;
using PayFlow.Reconciliation.Infrastructure;
using PayFlow.Reconciliation.Infrastructure.Persistence;

const string ServiceName = "payflow-reconciliation";

var builder = WebApplication.CreateBuilder(args);

builder.AddPayFlowObservability(ServiceName);
builder.Services.AddPayFlowReconciliationApplication();
builder.Services.AddPayFlowReconciliationInfrastructure(builder.Configuration);
builder.Services.AddPayFlowJwtAuthentication();
builder.Services.AddPayFlowMultitenancy();

builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ReconciliationDbContext>();
    await db.Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseAuthentication();
app.UsePayFlowMultitenancy();
app.UseAuthorization();

app.MapHealthChecks("/health");

app.Run();
