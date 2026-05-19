using PayFlow.Observability;

const string ServiceName = "payflow-gateway";

var builder = WebApplication.CreateBuilder(args);

builder.AddPayFlowObservability(ServiceName);
builder.Services.AddHealthChecks();

// YARP routes are bound from the "ReverseProxy" section of configuration.
// In production the section would carry every downstream service; for now it
// carries Identity only — more lands as services come online in later
// milestones.
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

// Local healthcheck — does not get forwarded. Stays a gateway-only liveness
// signal so a downstream outage doesn't make the gateway itself look down.
app.MapHealthChecks("/health");

// All other routes (currently /api/*) are forwarded to the matching
// downstream service per the YARP config.
app.MapReverseProxy();

app.Run();
