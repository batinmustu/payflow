using Microsoft.EntityFrameworkCore;
using PayFlow.EventBus.Kafka;
using PayFlow.Multitenancy;
using PayFlow.Notification.API.Endpoints;
using PayFlow.Notification.Application;
using PayFlow.Notification.Application.Notifications;
using PayFlow.Notification.Application.Notifications.Consumers;
using PayFlow.Notification.Infrastructure;
using PayFlow.Notification.Infrastructure.Persistence;
using PayFlow.Observability;

const string ServiceName = "payflow-notification";

var builder = WebApplication.CreateBuilder(args);

builder.AddPayFlowObservability(ServiceName);
builder.Services.AddPayFlowNotificationApplication();
builder.Services.AddPayFlowNotificationInfrastructure(builder.Configuration);
builder.Services.AddPayFlowJwtAuthentication();
builder.Services.AddPayFlowMultitenancy();

// Bind producer-side topics → consumers. Each consumer goes through the
// shared NotificationDispatcher which dedupes on the source message id
// and persists the audit row before calling the channel adapter.
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
    var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
    await db.Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseAuthentication();
app.UsePayFlowMultitenancy();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapNotificationEndpoints();

app.Run();
