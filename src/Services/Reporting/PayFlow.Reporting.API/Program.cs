using Microsoft.EntityFrameworkCore;
using PayFlow.EventBus.Kafka;
using PayFlow.Multitenancy;
using PayFlow.Observability;
using PayFlow.Reporting.API.Endpoints;
using PayFlow.Reporting.Application;
using PayFlow.Reporting.Application.Projections;
using PayFlow.Reporting.Application.Projections.Consumers;
using PayFlow.Reporting.Infrastructure;
using PayFlow.Reporting.Infrastructure.Persistence;

const string ServiceName = "payflow-reporting";

var builder = WebApplication.CreateBuilder(args);

builder.AddPayFlowObservability(ServiceName);
builder.Services.AddPayFlowReportingApplication();
builder.Services.AddPayFlowReportingInfrastructure(builder.Configuration);
builder.Services.AddPayFlowJwtAuthentication();
builder.Services.AddPayFlowMultitenancy();

// Bind topics → consumers. Infrastructure layer already called
// AddPayFlowKafkaConsuming; here we just declare what to project.
builder.Services.AddPayFlowKafkaConsumer<TransactionInitiatedIntegrationEvent, TransactionInitiatedConsumer>(
    "payflow.transaction.initiated.v1");
builder.Services.AddPayFlowKafkaConsumer<TransactionCapturedIntegrationEvent, TransactionCapturedConsumer>(
    "payflow.transaction.captured.v1");
builder.Services.AddPayFlowKafkaConsumer<TransactionFailedIntegrationEvent, TransactionFailedConsumer>(
    "payflow.transaction.failed.v1");
builder.Services.AddPayFlowKafkaConsumer<RefundCompletedIntegrationEvent, RefundCompletedConsumer>(
    "payflow.refund.completed.v1");

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
app.MapReportingEndpoints();

app.Run();
