using Microsoft.EntityFrameworkCore;
using PayFlow.EventBus.Kafka;
using PayFlow.Multitenancy;
using PayFlow.Observability;
using PayFlow.Transaction.API.Endpoints;
using PayFlow.Transaction.API.Idempotency;
using PayFlow.Transaction.Application;
using PayFlow.Transaction.Application.Refunds.SagaConsumers;
using PayFlow.Transaction.Infrastructure;
using PayFlow.Transaction.Infrastructure.Persistence;

const string ServiceName = "payflow-transaction";

var builder = WebApplication.CreateBuilder(args);

builder.AddPayFlowObservability(ServiceName);
builder.Services.AddPayFlowTransactionApplication();
builder.Services.AddPayFlowTransactionInfrastructure(builder.Configuration);
builder.Services.AddPayFlowJwtAuthentication();
builder.Services.AddPayFlowMultitenancy();
builder.Services.AddPayFlowIdempotency(builder.Configuration);

// Saga events flow back into TX as plain Kafka consumers. The Infrastructure
// layer already wired AddPayFlowKafkaConsuming — here we just bind the three
// topics Reconciliation publishes to their respective consumers.
builder.Services.AddPayFlowKafkaConsumer<RefundProcessingIntegrationEvent, RefundProcessingConsumer>(
    "payflow.refund.processing.v1");
builder.Services.AddPayFlowKafkaConsumer<RefundCompletedIntegrationEvent, RefundCompletedConsumer>(
    "payflow.refund.completed.v1");
builder.Services.AddPayFlowKafkaConsumer<RefundFailedIntegrationEvent, RefundFailedConsumer>(
    "payflow.refund.failed.v1");

builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<TransactionDbContext>();
    await db.Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseAuthentication();
app.UsePayFlowMultitenancy();
// Idempotency must see the principal (tenant id comes from JWT) but should
// run before Authorization so it can short-circuit replays without touching
// the endpoint pipeline.
app.UsePayFlowIdempotency();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapTransactionEndpoints();

app.Run();
