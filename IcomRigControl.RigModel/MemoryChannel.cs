namespace IcomRigControl.RigModel;

/// <summary>
/// Represents one memory channel (1-99) as read from or written to the radio
/// via CI-V command 1Ah 00h.
/// </summary>
public record MemoryChannel(
    int ChannelNumber,
    long FrequencyHz,
    string Mode,
    string Name
)
{
    /// A blank/unprogrammed channel placeholder.
    public static MemoryChannel Empty(int channelNumber) =>
        new(channelNumber, 0, "USB", string.Empty);
}