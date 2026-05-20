using Microsoft.EntityFrameworkCore;
using PayFlow.EventBus.Kafka;
using PayFlow.Multitenancy;
using PayFlow.Observability;
using PayFlow.Reconciliation.Application;
using PayFlow.Reconciliation.Application.RefundSagas;
using PayFlow.Reconciliation.Infrastructure;
using PayFlow.Reconciliation.Infrastructure.Persistence;

const string ServiceName = "payflow-reconciliation";

var builder = WebApplication.CreateBuilder(args);

builder.AddPayFlowObservability(ServiceName);
builder.Services.AddPayFlowReconciliationApplication();
builder.Services.AddPayFlowReconciliationInfrastructure(builder.Configuration);
builder.Services.AddPayFlowJwtAuthentication();
builder.Services.AddPayFlowMultitenancy();

// Saga starts here — bind the producer-side topic name to the consumer
// that walks the choreography. Add more AddPayFlowKafkaConsumer<,>() calls
// as the service grows.
builder.Services.AddPayFlowKafkaConsumer<RefundRequestedIntegrationEvent, RefundRequestedConsumer>(
    "payflow.refund.requested.v1");

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
app.UsePayFlowLogEnrichment();

app.MapHealthChecks("/health");

app.Run();
