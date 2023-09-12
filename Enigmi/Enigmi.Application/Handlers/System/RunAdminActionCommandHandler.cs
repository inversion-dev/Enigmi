using System.Globalization;
using Enigmi.Common;
using Enigmi.Common.Messaging;
using Enigmi.Grains.Shared.GrainSettings;
using Enigmi.Grains.Shared.PuzzleCollectionSniffer;
using Enigmi.Grains.Shared.PuzzleCollectionSniffer.Messages;
using Enigmi.Infrastructure.Services.BlockchainService;
using Enigmi.Messages.System;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace Enigmi.Application.Handlers.System;

public class RunAdminActionCommandHandler : Handler<RunAdminActionCommand, RunAdminActionResponse>
{
    private const string PopulateStakingAddressMessy = "POPSTAKEM355Y";
    private const string PopulateStakingAddress = "POPSTAKEADDR35535";
    private const string PopulateTransactionBlockDetails = "ICANHAZBLOCK5";

    public IClusterClient ClusterClient { get; }

    private IBlockchainService BlockchainService { get; }

    private ILogger<RunAdminActionCommandHandler> Logger { get; }

    public RunAdminActionCommandHandler(
        IBlockchainService blockchainService,
        ILogger<RunAdminActionCommandHandler> logger,
        IClusterClient clusterClient)
    {
        ClusterClient = clusterClient;
        BlockchainService = blockchainService.ThrowIfNull();
        Logger = logger.ThrowIfNull();
    }

    public override async Task<ResultOrError<RunAdminActionResponse>> Execute(RunAdminActionCommand command, CancellationToken cancellationToken)
    {
        string message = "";
        var action = "";
        try
        {
            switch (command.Action)
            {
                case "ProcessPuzzleCollection":
                    return await ProcessPuzzleCollection(command);

                case "UpdateUserWalletTimeOut":
                    return await UpdateGrainSettings(command, (settings, intValue) =>
                    {
                        settings.UserWalletOnlineIdleTimeout = TimeSpan.FromSeconds(intValue);
                    });
                
                case "UpdateUserWalletRoundTripPingInterval":
                    return await UpdateGrainSettings(command, (settings, intValue) =>
                    {
                        settings.UserWalletRoundTripPingInterval = TimeSpan.FromSeconds(intValue);
                    });
                
                case "UpdateOrderConfirmationThreshold":
                    return await UpdateGrainSettings(command, (settings, intValue) =>
                    {
                        settings.OrderBlockchainTransactionSettings.ConfirmationThreshold = intValue;
                    });

                default:
                    message = "Invalid Command!";
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, Invariant($"error during run admin action: {action}"));
            message = Invariant($"Error: {ex}");
        }

        return message.ToFailedResponse<RunAdminActionResponse>();
    }

    private async Task<ResultOrError<RunAdminActionResponse>> UpdateGrainSettings(RunAdminActionCommand command, 
        Action<Enigmi.Domain.Entities.GrainSettings.Settings,int> updateAction)
    {
        if (int.TryParse(command.AdditionalData, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue))
        {
            var grainSettingsGrain = ClusterClient.GetGrain<IGrainSettingsGrain>(Constants.SingletonGrain);
            var settings = await grainSettingsGrain.GetSettings();

            updateAction(settings, intValue);

            var updateSettingResponse = await grainSettingsGrain.UpdateSettings(settings);
            if (updateSettingResponse.HasErrors)
            {
                return updateSettingResponse.Errors.ToFailedResponse<RunAdminActionResponse>();
            }

            return new RunAdminActionResponse("Successfully updated setting").ToSuccessResponse();
        }
        
        return "Failed to update setting".ToFailedResponse<RunAdminActionResponse>();
    }

    private async Task<ResultOrError<RunAdminActionResponse>> ProcessPuzzleCollection(RunAdminActionCommand command)
    {
        if (string.IsNullOrEmpty(command.AdditionalData))
        {
            return "Additional data parameter is required".ToFailedResponse<RunAdminActionResponse>();
        }

        var createPuzzleCollectionCommand = new SeedPuzzleCollectionCommand(command.AdditionalData);
        var puzzleCollectionSnifferGrain = ClusterClient.GetGrain<IPuzzleCollectionSnifferGrain>(Constants.PuzzleCollectionSnifferGrainKey);
        var response = await puzzleCollectionSnifferGrain.SeedPuzzleCollection(createPuzzleCollectionCommand);
        return response.Transform(o => new RunAdminActionResponse("Successfully added puzzle collection"));
    }
}

public class RunAdminActionCommandValidator : AbstractValidator<RunAdminActionCommand>
{
    public RunAdminActionCommandValidator()
    {
        RuleFor(o => o.Action)
            .NotNull()
            .NotEmpty();
    }
}