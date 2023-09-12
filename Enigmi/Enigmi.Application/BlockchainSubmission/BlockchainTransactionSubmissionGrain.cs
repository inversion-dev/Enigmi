using Enigmi.Application.Grains;
using Enigmi.Common;
using Enigmi.Common.Domain;
using Enigmi.Common.Extensions;
using Enigmi.Common.Messaging;
using Enigmi.Domain.Entities.BlockchainTransactionSubmissionAggregate;
using Enigmi.Domain.Entities.BlockchainTransactionSubmissionAggregate.Events;
using Enigmi.Grains.Shared.GrainSettings;
using Enigmi.Infrastructure.Services.BlockchainService;
using Microsoft.Extensions.Logging;
using Orleans.Providers;
using Orleans.Runtime;
using System.Net;
using Enigmi.Grains.Shared.BlockchainTransactionSubmission;

namespace Enigmi.Application.BlockchainSubmission;

[StorageProvider(ProviderName = Constants.GrainStorageProviderName)]
public class BlockchainTransactionSubmissionGrain : GrainBase<BlockchainTransactionSubmission>, IRemindable, IBlockchainTransactionSubmissionGrain
{
    private ILogger<BlockchainTransactionSubmissionGrain> Logger { get; }

    private IBlockchainService BlockchainService { get; }

    private const string SyncBlockchainStatusReminder = "SyncBlockchainStatus";

    private IDisposable? SyncTransactionTimer { get; set; }

    public BlockchainTransactionSubmissionGrain(ILogger<BlockchainTransactionSubmissionGrain> logger, IBlockchainService blockchainService)
    {
        Logger = logger.ThrowIfNull();
        BlockchainService = blockchainService.ThrowIfNull();
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (State.DomainAggregate == null)
        {
            State.DomainAggregate = new BlockchainTransactionSubmission();
            await WriteStateAsync();
        }
        else
        {
            ScheduleSyncTransactionTimerIfRequired();
        }

        await base.OnActivateAsync(cancellationToken);
    }

    private async Task SyncConfirmationsFromBlockchain(Domain.Entities.GrainSettings.Settings settings)
    {
        State.DomainAggregate.ThrowIfNull();

        var transaction = await BlockchainService.GetTransactionAsync(State.DomainAggregate.TransactionId!);
        uint? confirmationCount = null;
        if (transaction.BlockHeight != null)
        {
            confirmationCount = await BlockchainService.GetConfirmationsAsync(transaction.BlockHeight!.Value);
        }

        if (confirmationCount == null && DateTime.UtcNow >
            State.DomainAggregate.TtlUtcTimestamp!.Value.AddMinutes(settings.OrderBlockchainTransactionSettings
                .TtlBufferInMinutes))
        {
            State.DomainAggregate.MarkAsNotIncluded();
            await CancelSyncBlockchainReminderAndSyncTimer();
            return;
        }

        if (confirmationCount == null)
        {
            return;
        }

        if (confirmationCount >= settings.OrderBlockchainTransactionSettings.ConfirmationThreshold)
        {
            State.DomainAggregate.MarkAsOnChainConfirmed(transaction.TxId, transaction.BlockHeight!.Value,
                transaction.BlockHash!, transaction.BlockUtcTimestamp!.Value);
            await CancelSyncBlockchainReminderAndSyncTimer();
        }
        else
        {
            State.DomainAggregate.MarkAsOnChain(confirmationCount.Value);
        }
    }

    private async Task CancelSyncBlockchainReminderAndSyncTimer()
    {
        var reminderReference = await this.GetReminder(SyncBlockchainStatusReminder);
        await this.UnregisterReminder(reminderReference);
        SyncTransactionTimer?.Dispose();

        Logger.LogInformation(Invariant($"BlockchainTransactionSubmissionGrain: Id - {this.GetGrainId().GetGuidKey()} CancelSyncBlockchainReminderAndSyncTimer"));
    }

    public async Task<ResultOrError<Constants.Unit>> Submit(
        Guid orderId,
        string signedTransactionCborHex,
        DateTime ttlUtcTimestamp)
    {
        State.DomainAggregate.ThrowIfNull();
        State.DomainAggregate.SetOrderId(orderId.ThrowIfEmpty());
        State.DomainAggregate.SetTransactionDetails(signedTransactionCborHex.ThrowIfNullOrWhitespace(), ttlUtcTimestamp.ThrowIfNull());
        await WriteStateAsync();

        await this.RegisterOrUpdateReminder(SyncBlockchainStatusReminder, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
        ScheduleSyncTransactionTimerIfRequired();

        return new Constants.Unit().ToSuccessResponse();
    }

    public ValueTask<BlockchainTransactionSubmission> GetBlockchainTransactionSubmissionDetail()
    {
        State.DomainAggregate.ThrowIfNull();
        return ValueTask.FromResult(State.DomainAggregate);
    }

    private bool IsErrorUtxoDoubleSpend(HttpRequestException ex)
    {
        return ex.Message.Contains("ValueNotConservedUTxO");
    }

    public Task ReceiveReminder(string reminderName, TickStatus status)
    {
        return Task.CompletedTask;
    }

    private async Task SyncTransactionHandler(object state)
    {
        if (State.DomainAggregate == null)
        {
            return;
        }

        if (State.DomainAggregate.State is BlockchainTransactionProcessState.OnChainConfirmed
            or BlockchainTransactionProcessState.Rejected)
        {
            await CancelSyncBlockchainReminderAndSyncTimer();
            return;
        }

        State.DomainAggregate.ThrowIfNull();

        var settingsGrain = GrainFactory.GetGrain<IGrainSettingsGrain>(Constants.SingletonGrain);
        var settings = await settingsGrain.GetSettings();

        if (State.DomainAggregate.ShouldResubmitTransaction)
        {
            await SubmitTransactionToBlockchain(settings);
            await WriteStateAsync();
            return;
        }

        await SyncConfirmationsFromBlockchain(settings);
        await WriteStateAsync();
    }

    private async Task SubmitTransactionToBlockchain(Domain.Entities.GrainSettings.Settings settings)
    {
        State.DomainAggregate.ThrowIfNull();

        try
        {
            var transactionId =
                await BlockchainService.SubmitTransactionAsync(State.DomainAggregate.SignedTransactionCborHex!);
            if (!string.IsNullOrEmpty(transactionId))
            {
                transactionId = transactionId.TrimStart('"').TrimEnd('"');
                State.DomainAggregate.SetTransactionId(transactionId);
                State.DomainAggregate.MarkAsSubmitted();
            }
            else
            {
                State.DomainAggregate.MarkAsRejected(settings.OrderBlockchainTransactionSettings.MaxTransientRejectedCount, false);
            }
        }
        catch (HttpRequestException ex) when (ex.StatusCode.IsIn(
                                                  HttpStatusCode.BadRequest, HttpStatusCode.Forbidden,
                                                  HttpStatusCode.NotFound,
                                                  HttpStatusCode.UnavailableForLegalReasons,
                                                  HttpStatusCode.TooManyRequests))
        {
            var isDoubleSpent = IsErrorUtxoDoubleSpend(ex);
            Logger.LogError(ex, "Tx Submission Rejected");
            State.DomainAggregate.MarkAsRejected(settings.OrderBlockchainTransactionSettings.MaxTransientRejectedCount, isDoubleSpent);
        }
        catch (HttpRequestException ex) when (ex.StatusCode.IsIn(HttpStatusCode.InternalServerError))
        {
            Logger.LogError(ex, "Tx Submission Failure");
            State.DomainAggregate.MarkAsSubmissionTransientFailure();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Tx Submission Exception");
            State.DomainAggregate.MarkAsSubmissionTransientFailure();
        }
    }

    public override IEnumerable<string> ResolveSubscriptionNames(DomainEvent @event)
    {
        if (State.DomainAggregate == null)
        {
            return string.Empty.ToSingletonList();
        }

        var subscriptionName = @event switch
        {
            BlockchainTransactionFailed => State.DomainAggregate.OrderId.ToString().ToSingletonList(),
            BlockchainTransactionSucceeded => State.DomainAggregate.OrderId.ToString().ToSingletonList(),
            BlockchainTransactionStateUpdated => State.DomainAggregate.OrderId.ToString().ToSingletonList(),
            BlockchainTransactionSubmitted => State.DomainAggregate.OrderId.ToString().ToSingletonList(),
            _ => string.Empty.ToSingletonList()
        };

        return subscriptionName;
    }

    private void ScheduleSyncTransactionTimerIfRequired()
    {
        State.DomainAggregate.ThrowIfNull();
        if (State.DomainAggregate.State is BlockchainTransactionProcessState.UnSubmitted
            or BlockchainTransactionProcessState.Submitted
            or BlockchainTransactionProcessState.SubmissionTransientFailure
            or BlockchainTransactionProcessState.OnChain
            or BlockchainTransactionProcessState.TransientRejected && SyncTransactionTimer == null)
        {
            SyncTransactionTimer = RegisterTimer(SyncTransactionHandler, this, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10));
        }
    }
}