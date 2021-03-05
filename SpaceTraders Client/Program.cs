using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using SpaceTraders_Client.Providers;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace SpaceTraders_Client
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("#app");

            builder.Services.AddScoped<StateEvents>();
            builder.Services.AddScoped<ConsoleOutput>();
            builder.Services.AddScoped<CommandHandler>();

            builder.Services.AddScoped(sp => new JsonSerializerOptions {
                AllowTrailingCommas = true,
                PropertyNameCaseInsensitive = true
            });

            builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.Configuration["base_url"]) });
            builder.Services.AddBlazoredLocalStorage(options =>
            {
                options.JsonSerializerOptions.DictionaryKeyPolicy = null;
            });
            builder.Services.AddScoped<SpaceTradersUserInfo>();

            builder.Services.AddScoped<ShipsProvider>();
            builder.Services.AddScoped<MarketProvider>();
            builder.Services.AddScoped<LocationProvider>();
            builder.Services.AddScoped<ShipyardProvider>();
            builder.Services.AddScoped<LoanProvider>();
            builder.Services.AddScoped<UtilityProvider>();

            var host = builder.Build();

            WarmServices(host.Services);

            await host.RunAsync();
        }

        private static void WarmServices(IServiceProvider serviceProvider)
        {
            serviceProvider.GetRequiredService<ShipsProvider>();
            serviceProvider.GetRequiredService<MarketProvider>();
            serviceProvider.GetRequiredService<LocationProvider>();
            serviceProvider.GetRequiredService<ShipyardProvider>();
            serviceProvider.GetRequiredService<LoanProvider>();
            serviceProvider.GetRequiredService<UtilityProvider>();
        }
    }
}
