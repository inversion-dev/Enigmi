using Enigmi.Common.Domain;
using Newtonsoft.Json;
using Orleans.Runtime;
using Orleans.Streams;

namespace Enigmi.Grains.Shared;

public class DomainGrainState<T> where T : DomainEntity
{
    public T? DomainAggregate { get; set; }
    
    private List<StreamIdStreamSequenceToken> _streamIdStreamSequenceToken = new();

    [JsonProperty]
    public IEnumerable<StreamIdStreamSequenceToken> StreamIdStreamSequenceTokens
    {
        get { return _streamIdStreamSequenceToken.AsReadOnly(); }
        private set { _streamIdStreamSequenceToken = value.ToList(); }
    }

    public void AddStreamSequence(StreamId streamId, StreamSequenceToken? streamSequenceToken)
    {
        var entry = GetStreamSequenceToken(streamId);
        if (entry != null)
        {
            return;
        }

        _streamIdStreamSequenceToken.Add(new StreamIdStreamSequenceToken{ StreamId = streamId, StreamSequenceToken = streamSequenceToken});
    }

    public StreamSequenceToken? GetStreamSequenceToken(StreamId streamId)
    {
        var entry = _streamIdStreamSequenceToken.FirstOrDefault(x => x.StreamId == streamId);
        return entry?.StreamSequenceToken;
    }

    public void SetStreamSequence(StreamId streamId, StreamSequenceToken? streamSequenceToken)
    {
        var entry = _streamIdStreamSequenceToken.FirstOrDefault(x => x.StreamId == streamId);
        if (entry == null)
        {
            AddStreamSequence(streamId, streamSequenceToken);
            return;
        }

        entry.StreamSequenceToken = streamSequenceToken;
    }

    public void ClearStreamSequences()
    {
        _streamIdStreamSequenceToken.Clear();
    }

    public class StreamIdStreamSequenceToken
    {
        public StreamId StreamId { get; set; }
        public StreamSequenceToken? StreamSequenceToken { get; set; }
    }
}