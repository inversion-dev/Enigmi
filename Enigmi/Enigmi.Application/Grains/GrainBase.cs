using Enigmi.Application.ExtensionMethods;
using Enigmi.Common;
using Enigmi.Common.Domain;
using Enigmi.Common.Messaging;
using Enigmi.Grains.Shared;
using Orleans.Runtime;
using Orleans.Streams;

namespace Enigmi.Application.Grains;

public abstract class GrainBase<T> : Grain<DomainGrainState<T>>, IAsyncObserver<DomainEvent>, IGrainBase
    where T : DomainEntity
{
    private readonly List<SubscriptionHandler> _subscriptionHandlers = new();

    protected GrainBase()
    {
        _currentTaskScheduler = TaskScheduler.Current;
        EnableProcessEventQueueTimer();
    }

    protected TaskScheduler _currentTaskScheduler;

    public abstract IEnumerable<string> ResolveSubscriptionNames(DomainEvent @event);

    private IDisposable? ProcessEventQueueTimer { get; set; }

    private async Task ProcessEventQueueHandler(object state)
    {
        await this.SelfInvokeAfter<IGrainBase>(o => o.ProcessEventQueue());
    }

    async Task IGrainBase.ProcessEventQueue()
    {
        if (State.DomainAggregate == null)
        {
            ProcessEventQueueTimer?.Dispose();
            ProcessEventQueueTimer = null;
            return;
        }

        var aggregate = State.DomainAggregate;
        if (!aggregate.DomainEvents.Any())
        {
            ProcessEventQueueTimer?.Dispose();
            ProcessEventQueueTimer = null;
            return;
        }

        try
        {
            foreach (var domainEvent in aggregate.DomainEvents.ToList())
            {
                var subscriptionNames = ResolveSubscriptionNames(domainEvent);
                foreach (var subscriptionName in subscriptionNames)
                {
                    if (!string.IsNullOrEmpty(subscriptionName))
                    {
                        var (_, stream) = GetStream(subscriptionName);
                        await stream.OnNextAsync(domainEvent);
                    }    
                }
                
                aggregate.MarkEventAsSent(domainEvent);
                await base.WriteStateAsync();
            }
        }
        finally
        {
            if (!aggregate.DomainEvents.Any())
            {
                ProcessEventQueueTimer?.Dispose();
                ProcessEventQueueTimer = null;
            }
        }
    }

    public Task<TaskScheduler> GetTaskScheduler()
    {
        return Task.FromResult(_currentTaskScheduler);
    }

    public async Task OnNextAsync(DomainEvent item, StreamSequenceToken? token = null)
    {
        var subscription = GetSubscriptionDetails(item);
        if (subscription == null || State.DomainAggregate == null)
        {
            return;
        }

        var processedEvent = State.DomainAggregate.ProcessedDomainEvents.FirstOrDefault(x => x.DomainEvent.Id == item.Id);
        if (processedEvent != null)
        {
            return;
        }

        var taskObject = subscription.Delegate.DynamicInvoke(item);
        await (Task)taskObject!;

        var subscriptionNames = ResolveSubscriptionNames(item);
        foreach (var subscriptionName in subscriptionNames)
        {
            if (!string.IsNullOrEmpty(subscriptionName))
            {
                var streamId = StreamId.Create(Constants.StreamNamespace, subscriptionName);
                State.SetStreamSequence(streamId, token!);
            }    
        }

        State.DomainAggregate.MarkEventAsProcessed(item);

        await WriteStateAsync();
    }

    private SubscriptionHandler? GetSubscriptionDetails(DomainEvent item)
    {
        return _subscriptionHandlers.FirstOrDefault(x => x.DomainType == item.GetType());
    }

    public Task OnCompletedAsync()
    {
        return Task.CompletedTask;
    }

    public Task OnErrorAsync(Exception ex)
    {
        return Task.CompletedTask;
    }

    public async Task Subscribe<TDomainEvent>(string subscriptionName, Func<TDomainEvent, Task> func) where TDomainEvent : DomainEvent
    {
        var (streamId, stream) = GetStream(subscriptionName);
        var param = func.Method.GetParameters()[0];

        _subscriptionHandlers.Add(new SubscriptionHandler
        {
            Delegate = func,
            DomainType = param.ParameterType,
            StreamId = streamId,
        });

        var subscriptionHandles = await stream.GetAllSubscriptionHandles();
        if (subscriptionHandles != null)
        {
            foreach (var subscription in subscriptionHandles)
            {
                await subscription.ResumeAsync(this, State.GetStreamSequenceToken(subscription.StreamId));
                return;
            }
        }

        await stream.SubscribeAsync(this, null);
        this.State.AddStreamSequence(streamId, null);
        await WriteStateAsync();
    }

    private (StreamId streamId, IAsyncStream<DomainEvent> stream) GetStream(string subscriptionName)
    {
        var streamProvider = this.GetStreamProvider(Constants.StreamProvider);
        var streamId = StreamId.Create(Constants.StreamNamespace, subscriptionName);
        var stream = streamProvider.GetStream<DomainEvent>(streamId);
        return (streamId, stream);
    }

    public async Task<ResultOrError<Constants.Unit>> UnsubscribeAll()
    {
        var streamProvider = this.GetStreamProvider(Constants.StreamProvider);
        foreach (var streamId in State.StreamIdStreamSequenceTokens)
        {
            var stream = streamProvider.GetStream<DomainEvent>(streamId.StreamId);
            var subscriptionHandles = await stream.GetAllSubscriptionHandles();
            if (subscriptionHandles != null)
            {
                foreach (var subscription in subscriptionHandles)
                {
                    await subscription.UnsubscribeAsync();
                }
            }
        }

        this.State.ClearStreamSequences();
        await WriteStateAsync();

        return new Constants.Unit().ToSuccessResponse();
    }
    
    public async Task<ResultOrError<Constants.Unit>> Unsubscribe(string subscriptionName)
    {
        var streamProvider = this.GetStreamProvider(Constants.StreamProvider);
        var streamId = StreamId.Create(Constants.StreamNamespace, subscriptionName);
        var stream = streamProvider.GetStream<DomainEvent>(streamId);
        var subscriptionHandles = await stream.GetAllSubscriptionHandles();
        if (subscriptionHandles != null)
        {
            foreach (var subscription in subscriptionHandles)
            {
                await subscription.UnsubscribeAsync();
            }
        }

        await WriteStateAsync();
        return new Constants.Unit().ToSuccessResponse();
    }

    protected override async Task WriteStateAsync()
    {
        await base.WriteStateAsync();

        if (State.DomainAggregate != null && (State.DomainAggregate.DomainEvents.Any()))
        {
            EnableProcessEventQueueTimer();
        }
    }

    private void EnableProcessEventQueueTimer()
    {
        ProcessEventQueueTimer ??= RegisterTimer(ProcessEventQueueHandler, this, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    private class SubscriptionHandler
    {
        public StreamId StreamId { get; set; }

        public Type DomainType { get; set; } = null!;

        public Delegate Delegate { get; set; } = null!;
    }
}