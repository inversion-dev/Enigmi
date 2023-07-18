using CardanoSharp.Blockfrost.Sdk.Common;
using Enigmi.Application;
using Enigmi.Application.Behaviors;
using Enigmi.Application.Services;
using Enigmi.Common;
using Enigmi.Common.Messaging;
using Enigmi.Infrastructure.Services;
using Enigmi.Infrastructure.Services.BlobStorage;
using Enigmi.Infrastructure.Services.BlockchainService;
using Enigmi.Infrastructure.Services.ImageProcessing;
using Enigmi.Infrastructure.Services.TimeProvider;
using Esendex.TokenBucket;
using FluentValidation;
using Foundatio.Caching;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Orleans.Serialization;
using System.Reflection;
using Enigmi.Infrastructure.Services.Authentication;
using Enigmi.Infrastructure.Services.SignalR;
using Orleans.Configuration;
using static System.FormattableString;

namespace Enigmi.HostSetup;

public static class ConfigurationExtensions
{
    public static IConfigurationBuilder AddJsonFiles(this IConfigurationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);
    }

    public static void ConfigureSerializer(this ISerializerBuilder serializerBuilder)
    {
        var jsonSerializerSettings = new JsonSerializerSettings();
        jsonSerializerSettings.TypeNameHandling = TypeNameHandling.All;
        jsonSerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;

        serializerBuilder.AddNewtonsoftJsonSerializer(isSupported: type => type!.Namespace!.StartsWith("Enigmi.Domain"), jsonSerializerSettings);
        serializerBuilder.AddNewtonsoftJsonSerializer(isSupported: type => type!.Namespace!.StartsWith("Domain.ValueObjects"), jsonSerializerSettings);
        serializerBuilder.AddNewtonsoftJsonSerializer(isSupported: type => type!.Namespace!.StartsWith("Enigmi.Grains"), jsonSerializerSettings);
        serializerBuilder.AddNewtonsoftJsonSerializer(isSupported: type => type!.Namespace!.StartsWith("Orleans.Streams"), jsonSerializerSettings);        
        serializerBuilder.AddNewtonsoftJsonSerializer(isSupported: type => type!.Namespace!.StartsWith("Enigmi.Common.Messaging"), jsonSerializerSettings);        
    }

    public static IConfiguration GetConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFiles()
            .Build();

        return configuration;
    }

    public static IServiceCollection ConfigureServices(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        AssemblyProvider assemblyProvider = new AssemblyProvider();
        var assembliesToScan = assemblyProvider.GetValidatorAssemblies().Concat(assemblyProvider.GetMessageAssemblies())
            .ToArray();

        Settings settings = services.ConfigureSettings(configuration);
        services.AddSingleton(settings.BlockfrostConfig);
        services.AddSingleton(settings.PolicyVaultConfig);
        services.AddSingleton(settings.CardanoBlockchainConfig);
        services.AddSingleton(settings.SendInBlueConfig);
        services.AddSingleton(settings.BlobstorageConfig);
        services.AddSingleton(assemblyProvider);
        services.AddSingleton<ITokenBucket, BlockfrostTokenBucket>();

        services.AddSingleton<ICacheClient, InMemoryCacheClient>();

        services.AddHttpClient<BlockfrostBlockchainService>();
        services.AddHttpClient<SendInBlueEmailSender>();

        services.AddScoped<ScopedInformation>();

        var authConfig =
            new AuthHeaderConfiguration(settings.BlockfrostConfig.ApiKey, settings.BlockfrostConfig.ApiUrl);
        services.AddBlockfrost(authConfig);

        services.AddTransient<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddTransient<IBlockchainService, BlockfrostBlockchainService>();
        services.AddTransient<IPolicyVaultService, PolicyVaultService>();
        services.AddTransient<IEmailSender, SendInBlueEmailSender>();
        services.AddTransient<IBlobStorageService, AzureBlobStorageService>();
        services.AddTransient<IImageProcessingService, ImageProcessingService>();
        services.AddTransient<IAuthenticationService, AuthenticationService>();

        services.AddLogging();
        services.ConfigureMediatr(assembliesToScan);
        services.AddValidatorsFromAssemblies(assembliesToScan, ServiceLifetime.Singleton);

        services.AddSingleton<SignalRService>();
        services.AddHostedService(sp => sp.GetService<SignalRService>().ThrowIfNull());
        services.AddSingleton<ISignalRHubContextStore>(sp => sp.GetService<SignalRService>().ThrowIfNull());
        
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(
                builder =>
                {
                    builder.WithOrigins(settings.JwtTokenConfiguration.Origins)
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                });
        });
        
        return services;
    }

    private static void ConfigureMediatr(this IServiceCollection services, params Assembly[] assembliesToScan)
    {
        services.AddMediatR(assembliesToScan);

        services.ConfigurePipeline(assembliesToScan);

        services.AddTransient<IMessageSender, MediatorMessageSender>();
    }

    private static void ConfigurePipeline(this IServiceCollection services, params Assembly[] assembliesToScan)
    {
        services.AddMediatRPipeline(typeof(LoggingBehavior<,>), assembliesToScan);
        services.AddMediatRPipeline(typeof(ApplyMessageAccessPolicyBehavior<,>), assembliesToScan);
        services.AddMediatRPipeline(typeof(ValidateBehavior<,>), assembliesToScan);
        services.AddMediatRPipeline(typeof(RequestCachingBehavior<,>), assembliesToScan);
    }

    private static void AddMediatRPipeline(this IServiceCollection services, Type behaviorType,
        params Assembly[] assembliesToScan)
    {
        var messageContstraintTypes =
            behaviorType
                .GetGenericArguments()
                .First()
                .GetGenericParameterConstraints()
                .Where(o => o.IsAssignableToExtended(typeof(IMessage<>)));

        var messageResponseCombinations =
            assembliesToScan
                .SelectMany(o => o.GetTypes())
                .Where(o => !o.IsInterface && !o.IsAbstract)
                .Where(o => messageContstraintTypes.All(i => o.IsAssignableToExtended(i)))
                .Select(o => new
                {
                    MessageType = o,
                    ResponseType = o.GetGenericInterfacesOf(typeof(IMessage<>))!.GetGenericArguments().Single()
                })
                .ToList();

        foreach (var combination in messageResponseCombinations)
        {
            Type mediatorEnvelopeType = typeof(MediatorMessageEnvelope<,>).MakeGenericType(combination.MessageType, combination.ResponseType);
            Type resultOrErrorType = typeof(ResultOrError<>).MakeGenericType(combination.ResponseType);
            Type behaviorInterfaceType = typeof(IPipelineBehavior<,>).MakeGenericType(mediatorEnvelopeType, resultOrErrorType);
            Type behaviorConcreteType = behaviorType.MakeGenericType(combination.MessageType, combination.ResponseType);
            services.AddTransient(behaviorInterfaceType, behaviorConcreteType);
        }
    }

    private static Settings ConfigureSettings(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = new Settings();
        configuration.GetSection("Settings").Bind(settings);
        services.AddTransient(o => settings);
        return settings;
    }

    public static void SetupSiloStorage(this ISiloBuilder siloBuilder)
    {
        var settings = GetConfiguration().GetSection("Settings").Get<Settings>();
        settings.ThrowIfNull();

        siloBuilder            
            .AddAzureBlobGrainStorage(Constants.GrainStorageProviderName, (AzureBlobStorageOptions options) =>
            {
                options.ConfigureBlobServiceClient(settings.BlobstorageConfig.ConnectionString);
                options.ContainerName =  Invariant($"{settings.EnvironmentPrefix}{Constants.GrainStorageProviderName}");
            })
            .AddAzureTableGrainStorage(
                name: "PubSubStore",
                configureOptions: options =>
                {
                    options.ConfigureTableServiceClient(settings.TableStorageConfig.ConnectionString);
                    options.TableName = Invariant($"{settings.EnvironmentPrefix}PubSubStore");
                })
            .AddEventHubStreams(
                Constants.StreamProvider,
                (ISiloEventHubStreamConfigurator configurator) =>
                {
                    configurator.ConfigureEventHub(optionsBuilder => optionsBuilder.Configure(options =>
                    {
                        options.ConfigureEventHubConnection(settings.EventHubConfig.ConnectionString,
                            settings.EventHubConfig.EventHubPath,
                            settings.EventHubConfig.EventHubConsumerGroup);
                    }));
                    configurator.UseAzureTableCheckpointer(
                        optionsBuilder => optionsBuilder.Configure(options =>
                        {
                            options.ConfigureTableServiceClient(settings.TableStorageConfig.ConnectionString);
                            options.PersistInterval = TimeSpan.FromSeconds(10);
                            options.TableName = Invariant($"{settings.EnvironmentPrefix}CheckPoint");
                        }));
                })
            .UseAzureTableReminderService(configure =>
            {
                configure.ConfigureTableServiceClient(settings.TableStorageConfig.ConnectionString);
                configure.TableName = Invariant($"{settings.EnvironmentPrefix}ReminderStore");
            })
            .AddReminders();
    }
}