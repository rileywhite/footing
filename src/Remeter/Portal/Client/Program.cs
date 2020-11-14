using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Remeter.Portal.JSInterop;
using Remeter.Portal.Shared;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Remeter.Portal.Client
{
    public class Program
    {
        private class IsPrerenderDetection : IBlazorPrerenderDetector
        {
            public bool IsPrerendering => false;
        }

        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
            Console.WriteLine("RootComponents End");

            var wasPrerendered = builder.RootComponents.Any();

            if (!wasPrerendered)
            {
                builder.RootComponents.Add<App>("app");
            }

            builder.Services
                .AddTransient(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) })
                .AddBlazoredLocalStorage()
                .AddSingleton<IBlazorPrerenderDetector>(new IsPrerenderDetection())
                .AddSingleton<IJSInterop>(services => new JSInteropProvider(services.GetService<IJSRuntime>()!));

            // builder.Services.AddOidcAuthentication(options =>
            // {
            //     // Configure your authentication provider options here.
            //     // For more information, see https://aka.ms/blazor-standalone-auth
            //     builder.Configuration.Bind("Local", options.ProviderOptions);
            // });

            await builder.Build().RunAsync();
        }
    }
}
