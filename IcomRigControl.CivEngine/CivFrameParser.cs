namespace IcomRigControl.CivEngine;

/// <summary>
/// Stateful parser that accumulates incoming CI-V bytes and emits complete CivFrame
/// objects as they are found. Handles frames split across multiple reads and multiple
/// frames arriving in a single buffer.
/// </summary>
public class CivFrameParser
{
    private readonly List<byte> _buffer = new();

    /// <summary>
    /// Feed newly received bytes into the parser. Returns any complete frames found.
    /// Incomplete trailing data is retained internally for the next call.
    /// </summary>
    public List<CivFrame> Feed(ReadOnlySpan<byte> incoming)
    {
        _buffer.AddRange(incoming.ToArray());

        var frames = new List<CivFrame>();

        while (true)
        {
            // Find start of a frame: two consecutive FE bytes
            int start = FindPreambleStart();
            if (start < 0)
            {
                // No preamble found at all — discard everything (pure noise)
                _buffer.Clear();
                break;
            }

            // Discard any noise before the preamble
            if (start > 0)
                _buffer.RemoveRange(0, start);

            // Find end of frame (FD) after the preamble
            int end = _buffer.IndexOf(CivCommands.EndOfMessage, 2);
            if (end < 0)
            {
                // Frame not complete yet — wait for more data
                break;
            }

            // Extract the frame bytes: [FE FE To From Cmd [SubCmd] [Data...] FD]
            var frameBytes = _buffer.GetRange(0, end + 1);
            _buffer.RemoveRange(0, end + 1);

            var parsed = ParseFrame(frameBytes);
            if (parsed != null)
                frames.Add(parsed);
        }

        return frames;
    }

    private int FindPreambleStart()
    {
        for (int i = 0; i < _buffer.Count - 1; i++)
        {
            if (_buffer[i] == CivCommands.Preamble && _buffer[i + 1] == CivCommands.Preamble)
                return i;
        }
        return -1;
    }

    private static CivFrame? ParseFrame(List<byte> frameBytes)
    {
        // Minimum valid frame: FE FE To From Cmd FD = 6 bytes
        if (frameBytes.Count < 6) return null;
        if (frameBytes[0] != CivCommands.Preamble || frameBytes[1] != CivCommands.Preamble) return null;
        if (frameBytes[^1] != CivCommands.EndOfMessage) return null;

        byte to = frameBytes[2];
        byte from = frameBytes[3];
        byte command = frameBytes[4];

        // Everything between command and the trailing FD is subcommand + data.
        // Pass (FBh) and Fail (FAh) responses have no subcommand or data.
        var remainder = frameBytes.GetRange(5, frameBytes.Count - 6);

        if (command == CivCommands.Pass || command == CivCommands.Fail)
        {
            return new CivFrame(to, from, command, null, Array.Empty<byte>());
        }

        // Heuristic: commands that carry a subcommand byte have one as the first
        // remainder byte for the multi-function command groups (14h,15h,16h,1Ah,1Bh,1Ch,21h,27h).
        // Simple commands (03h,04h,05h,06h,07h,08h,09h,0Ah,0Bh,etc.) do not.
        bool hasSubCommand = command is 0x07 or 0x08 or 0x0E or 0x14 or 0x15 or 0x16
                                       or 0x1A or 0x1B or 0x1C or 0x1E or 0x21 or 0x27;

        if (hasSubCommand && remainder.Count > 0)
        {
            byte subCommand = remainder[0];
            var data = remainder.GetRange(1, remainder.Count - 1).ToArray();
            return new CivFrame(to, from, command, subCommand, data);
        }

        return new CivFrame(to, from, command, null, remainder.ToArray());
    }
}
