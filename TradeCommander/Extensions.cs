using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sentry.Extensions.Logging;
using Sentry.Extensions.Logging.Extensions.DependencyInjection;

namespace TradeCommander
{
    public static class Extensions
    {
        public static IServiceCollection AddSentry(this IServiceCollection services, string dsn)
        {
            services
                .AddSingleton<IConfigureOptions<SentryLoggingOptions>>(provider => new ConfigureOptions<SentryLoggingOptions>(options => options.Dsn = dsn))
                .AddSingleton<ILoggerProvider, SentryLoggerProvider>()
                .AddSentry<SentryLoggingOptions>();

            return services;
        }
    }
}
