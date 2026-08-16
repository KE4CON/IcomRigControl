namespace IcomRigControl.Services;

/// <summary>
/// Something that can save a recording of a just-completed contact's audio — the
/// Band DVR implements this from its rolling buffer, and the QsoLogger calls it when
/// a QSO is logged so each contact can carry its own audio. See CLAUDE.md per-QSO audio.
/// </summary>
public interface IQsoAudioSource
{
    /// Saves the recent receive audio for a contact with <paramref name="callsign"/>,
    /// returning the file path, or null if no audio is available (not monitoring).
    string? SaveQsoAudio(string callsign);
}
