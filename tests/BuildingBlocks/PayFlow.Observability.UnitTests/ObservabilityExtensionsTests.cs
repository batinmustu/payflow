using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PayFlow.Observability.UnitTests;

public class ObservabilityExtensionsTests
{
    [Fact]
    public void Registers_a_logger_factory_that_resolves_from_the_host()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddPayFlowObservability("test-service");
        using var host = builder.Build();

        var factory = host.Services.GetRequiredService<ILoggerFactory>();
        factory.Should().NotBeNull();
        factory.CreateLogger("any").Should().NotBeNull();
    }

    [Fact]
    public void Returns_the_same_builder_for_chaining()
    {
        var builder = Host.CreateApplicationBuilder();

        var returned = builder.AddPayFlowObservability("test-service");

        returned.Should().BeSameAs(builder);
    }

    [Fact]
    public void Throws_when_service_name_is_blank()
    {
        var builder = Host.CreateApplicationBuilder();

        var act = () => builder.AddPayFlowObservability("   ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Throws_when_builder_is_null()
    {
        IHostApplicationBuilder builder = null!;

        var act = () => builder.AddPayFlowObservability("test-service");

        act.Should().Throw<ArgumentNullException>();
    }
}
