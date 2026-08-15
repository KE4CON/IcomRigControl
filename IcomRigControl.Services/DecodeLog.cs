namespace IcomRigControl.Services;

/// <summary>
/// Saves decoded-text buffers (CW, RTTY) to timestamped .txt files under a
/// "Decoded" folder in the user's Documents, ready to open and print. Kept tiny and
/// shared so every decoder window saves the same way. See CLAUDE.md CW/RTTY decode.
/// </summary>
public static class DecodeLog
{
    /// Writes <paramref name="text"/> to Documents/IcomRigControl/Decoded/
    /// {mode}_{timestamp}.txt and returns the full path.
    public static string Save(string mode, string text)
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "IcomRigControl", "Decoded");
        Directory.CreateDirectory(dir);

        string file = Path.Combine(dir, $"{mode}_{DateTime.Now:yyyy-MM-dd_HHmmss}.txt");
        File.WriteAllText(file, text);
        return file;
    }
}
