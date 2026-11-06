using System.Text.Json.Serialization;

namespace ModernTinyWall.TinyWall;

public sealed record TwMessageDataUsage : TwMessage
{
    public DataUsagePeriod Period { get; }
    public DataUsageSnapshot? Snapshot { get; }

    [JsonConstructor]
    public TwMessageDataUsage(DataUsagePeriod period, DataUsageSnapshot? snapshot)
        : base(MessageType.READ_DATA_USAGE)
    {
        Period = period;
        Snapshot = snapshot;
    }
}
