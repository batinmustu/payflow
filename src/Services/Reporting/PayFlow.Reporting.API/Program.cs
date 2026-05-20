using Microsoft.EntityFrameworkCore;
using PayFlow.Multitenancy;
using PayFlow.Observability;
using PayFlow.Reporting.API.Auth;
using PayFlow.Reporting.Application;
using PayFlow.Reporting.Infrastructure;
using PayFlow.Reporting.Infrastructure.Persistence;

const string ServiceName = "payflow-reporting";

var builder = WebApplication.CreateBuilder(args);

builder.AddPayFlowObservability(ServiceName);
builder.Services.AddPayFlowReportingApplication();
builder.Services.AddPayFlowReportingInfrastructure(builder.Configuration);
builder.Services.AddPayFlowJwtAuthentication();
builder.Services.AddPayFlowMultitenancy();

builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();
    await db.Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseAuthentication();
app.UsePayFlowMultitenancy();
app.UseAuthorization();

app.MapHealthChecks("/health");

app.Run();
