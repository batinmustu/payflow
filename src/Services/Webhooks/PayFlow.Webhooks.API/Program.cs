using Microsoft.EntityFrameworkCore;
using PayFlow.EventBus.Kafka;
using PayFlow.Multitenancy;
using PayFlow.Observability;
using PayFlow.Webhooks.API.Endpoints;
using PayFlow.Webhooks.Application;
using PayFlow.Webhooks.Application.Consumers;
using PayFlow.Webhooks.Application.Deliveries;
using PayFlow.Webhooks.Infrastructure;
using PayFlow.Webhooks.Infrastructure.Persistence;

const string ServiceName = "payflow-webhooks";

var builder = WebApplication.CreateBuilder(args);

builder.AddPayFlowObservability(ServiceName);
builder.Services.AddPayFlowWebhooksApplication();
builder.Services.AddPayFlowWebhooksInfrastructure(builder.Configuration);
builder.Services.AddPayFlowJwtAuthentication();
builder.Services.AddPayFlowMultitenancy();

builder.Services.AddPayFlowKafkaConsumer<TransactionCapturedIntegrationEvent, TransactionCapturedConsumer>(
    "payflow.transaction.captured.v1");
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
    var db = scope.ServiceProvider.GetRequiredService<WebhooksDbContext>();
    await db.Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseAuthentication();
app.UsePayFlowMultitenancy();
app.UseAuthorization();
app.UsePayFlowLogEnrichment();

app.MapHealthChecks("/health");
app.MapSubscriptionEndpoints();
app.MapDeliveryEndpoints();

app.Run();
