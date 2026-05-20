using System.Text.Json.Serialization;
using PayFlow.Multitenancy;
using PayFlow.Observability;
using PayFlow.Payment.API.Endpoints;
using PayFlow.Payment.Application;
using PayFlow.Payment.Infrastructure;

const string ServiceName = "payflow-payment";

var builder = WebApplication.CreateBuilder(args);

builder.AddPayFlowObservability(ServiceName);
builder.Services.AddPayFlowPaymentApplication();
builder.Services.AddPayFlowPaymentInfrastructure(builder.Environment);
builder.Services.AddPayFlowJwtAuthentication();
builder.Services.AddPayFlowMultitenancy();

// PaymentStatus etc. surface as readable strings ("Captured"), not enum
// ordinals ("1"). Clients should branch on the name, not the number.
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseAuthentication();
app.UsePayFlowMultitenancy();
app.UseAuthorization();
app.UsePayFlowLogEnrichment();

app.MapHealthChecks("/health");
app.MapPaymentEndpoints();

app.Run();
