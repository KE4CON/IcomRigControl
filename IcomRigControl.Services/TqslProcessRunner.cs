using System.Diagnostics;

namespace IcomRigControl.Services;

/// <summary>
/// Real ITqslProcessRunner implementation: launches ARRL's TQSL executable
/// as an external process to sign an ADIF file. Requires TQSL to already be
/// installed and the user's station location/certificate already configured
/// (one-time ARRL setup outside this app — see CLAUDE.md Phase 8d).
/// </summary>
public class TqslProcessRunner : ITqslProcessRunner
{
    private readonly string _tqslExecutablePath;

    public TqslProcessRunner(string tqslExecutablePath)
    {
        _tqslExecutablePath = tqslExecutablePath;
    }

    public async Task<TqslResult> SignAdifFileAsync(string adifFilePath, string outputPath)
    {
        try
        {
            if (!File.Exists(_tqslExecutablePath))
            {
                return new TqslResult(false, $"TQSL executable not found at: {_tqslExecutablePath}");
            }

            if (!File.Exists(adifFilePath))
            {
                return new TqslResult(false, $"ADIF file not found: {adifFilePath}");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = _tqslExecutablePath,
                Arguments = $"-a compendium -d -x -o \"{outputPath}\" \"{adifFilePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return new TqslResult(false, "Failed to start TQSL process.");
            }

            string stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                return new TqslResult(false, string.IsNullOrWhiteSpace(stderr)
                    ? $"TQSL exited with code {process.ExitCode}"
                    : stderr.Trim());
            }

            if (!File.Exists(outputPath))
            {
                return new TqslResult(false, "TQSL reported success but did not produce an output file.");
            }

            return new TqslResult(true, null);
        }
        catch (Exception ex)
        {
            return new TqslResult(false, ex.Message);
        }
    }
}