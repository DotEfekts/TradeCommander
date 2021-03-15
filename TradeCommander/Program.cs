using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using TradeCommander.Providers;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using TradeCommander.CommandHandlers;

namespace TradeCommander
{
    public class Program
    {
        private const int REQUESTS_PER_SECOND = 2;

        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("#app");

            builder.Services.AddScoped(sp => new JsonSerializerOptions {
                AllowTrailingCommas = true,
                PropertyNameCaseInsensitive = true
            });

            builder.Services.AddScoped(sp => new HttpClient(new RateLimitedHandler(REQUESTS_PER_SECOND)) { 
                BaseAddress = new Uri(builder.Configuration["base_url"]) });

            builder.Services.AddBlazoredLocalStorage(options =>
            {
                options.JsonSerializerOptions.DictionaryKeyPolicy = null;
            });

            builder.Services.AddSentry(builder.Configuration["sentry_dsn"]);

            builder.Services.AddScoped<StateProvider>();
            builder.Services.AddScoped<SettingsProvider>();

            builder.Services.AddScoped<ConsoleOutput>();
            builder.Services.AddScoped<UserProvider>();
            builder.Services.AddScoped<CommandManager>();

            builder.Services.AddScoped<ShipsProvider>();
            builder.Services.AddScoped<MarketProvider>();
            builder.Services.AddScoped<AutoRouteProvider>();

            var host = builder.Build();
            host.Services.GetService<CommandManager>().RegisterCommands();

            await host.RunAsync();
        }
    }
}
