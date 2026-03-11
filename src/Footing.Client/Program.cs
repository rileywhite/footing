using Blazored.LocalStorage;
using Footing.Client.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddBlazoredLocalStorage();
builder.Services.AddScoped<IFootingJSInterop, FootingJSInteropProvider>();

await builder.Build().RunAsync();
