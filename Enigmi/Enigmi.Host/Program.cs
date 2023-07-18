using Enigmi.HostSetup;
using Orleans.Configuration;
using Enigmi.Common;
using Orleans.Serialization;
using ConfigurationExtensions = Enigmi.HostSetup.ConfigurationExtensions;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddConfiguration(ConfigurationExtensions.GetConfiguration());
var settings = builder.Configuration.GetSection("Settings").Get<Settings>();
settings.ThrowIfNull();

builder.Host.UseOrleans(siloBuilder =>
{
    siloBuilder.Services.AddSerializer(serializerBuilder =>
    {
        serializerBuilder.ConfigureSerializer();
    });

    siloBuilder
        .UseLocalhostClustering()
        .Configure<ClusterOptions>(options =>
        {
            options.ClusterId = settings.ClusterConfiguration.ClusterId;
            options.ServiceId = settings.ClusterConfiguration.ServiceId;
        })
        .SetupSiloStorage();
});

builder.Services.AddControllers().AddNewtonsoftJson();
builder.Services.ConfigureServices(builder.Configuration);


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
   app.UseWebAssemblyDebugging();
   app.UseCors();
}
else
{
    builder.Services.AddApplicationInsightsTelemetry();
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.UseRouting();

app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();