using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Shared.Hosting;

public static class GracefulShutdownExtensions
{
    public const int ShutdownTimeoutSeconds = 15;

    public static TBuilder AddGracefulShutdown<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.Configure<HostOptions>(o =>
            o.ShutdownTimeout = TimeSpan.FromSeconds(ShutdownTimeoutSeconds));
        return builder;
    }
}