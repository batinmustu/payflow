using Microsoft.EntityFrameworkCore;
using PayFlow.Multitenancy;
using PayFlow.Observability;
using PayFlow.Transaction.API.Auth;
using PayFlow.Transaction.API.Endpoints;
using PayFlow.Transaction.API.Idempotency;
using PayFlow.Transaction.Application;
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
