namespace IcomRigControl.Services;

/// <summary>
/// Result of a TQSL signing operation.
/// </summary>
public record TqslResult(bool Success, string? Message);

/// <summary>
/// Abstraction over launching ARRL's TQSL tool as an external process to
/// sign an ADIF file for LoTW upload. Isolated behind this interface so
/// LotwBridge is testable without TQSL actually installed. See CLAUDE.md
/// Phase 8d — TQSL is the correct, ARRL-sanctioned signing tool; this
/// project must never reimplement ARRL's certificate/signing logic.
/// </summary>
public interface ITqslProcessRunner
{
    /// Signs the ADIF file at adifFilePath, writing the signed .tq8 output
    /// to outputPath. Returns success/failure — never throws.
    Task<TqslResult> SignAdifFileAsync(string adifFilePath, string outputPath);
}