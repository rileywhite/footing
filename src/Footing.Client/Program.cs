using Blazored.LocalStorage;
using Footing.Client;
using Footing.Client.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<Routes>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddBlazoredLocalStorage();
builder.Services.AddScoped<IFootingJSInterop, FootingJSInteropProvider>();

await builder.Build().RunAsync();
