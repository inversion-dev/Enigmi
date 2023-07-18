using Blazored.LocalStorage;
using Blazored.Modal;
using Blazored.Toast;
using Enigmi.Blazor;
using Enigmi.Blazor.Events;
using Enigmi.Blazor.Utils;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddScoped<ApiClient>();
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddScoped<WalletConnection>();
builder.Services.AddScoped<AuthenticationClient>();
builder.Services.AddScoped<ClientAppSettings>();
builder.Services.AddScoped<SignalRClient>();
builder.Services.AddScoped<TabVisibilityHandler>();
builder.Services.AddScoped<PuzzleSelectionManager>();

builder.Services.AddScoped<OnShowBuyPuzzlePieceSectionRequestedEvent>();
builder.Services.AddScoped<OnHideBuyPuzzlePieceSectionRequestedEvent>();

builder.Services.AddScoped<OnUserWalletStateRefreshedEvent>();
builder.Services.AddScoped<OnUserWalletStateReceivedEvent>();
builder.Services.AddScoped<ActivePuzzlePieceUpdatedEvent>();

builder.Services.AddScoped<OnShowScreenBlockerEvent>();
builder.Services.AddScoped<OnUnblockScreenRequestedEvent>();

builder.Services.AddBlazoredModal();
builder.Services.AddBlazoredToast();

await builder.Build().RunAsync();