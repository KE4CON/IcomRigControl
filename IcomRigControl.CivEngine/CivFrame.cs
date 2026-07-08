namespace IcomRigControl.CivEngine;

/// <summary>
/// Immutable record representing a single CI-V frame.
/// Format: FE FE [To] [From] [Command] [SubCommand] [Data...] FD
/// </summary>
public record CivFrame(
    byte To,
    byte From,
    byte Command,
    byte? SubCommand,
    byte[] Data
)
{
    public bool IsPassResponse => Command == CivCommands.Pass;
    public bool IsFailResponse => Command == CivCommands.Fail;
}
