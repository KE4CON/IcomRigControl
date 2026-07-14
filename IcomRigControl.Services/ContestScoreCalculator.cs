namespace IcomRigControl.Services;

/// <summary>
/// The result of scoring a contest log: total points, QSO count, and the
/// set of distinct sections/multipliers worked so far.
/// </summary>
public record ContestScoreResult(
    int TotalPoints,
    int QsoCount,
    HashSet<string> SectionsWorked
);

/// <summary>
/// Computes a running score for a contest log using a given ContestDefinition's
/// scoring rules. Currently models points-per-mode and section-based multiplier
/// tracking (Field Day style); more sophisticated multiplier rules can be added
/// per-contest as needed.
/// </summary>
public static class ContestScoreCalculator
{
    public static ContestScoreResult CalculateScore(ContestDefinition contest, IEnumerable<QsoRecord> qsos)
    {
        int totalPoints = 0;
        int qsoCount = 0;
        var sections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var qso in qsos)
        {
            totalPoints += contest.PointsForMode(qso.Mode);
            qsoCount++;

            string? section = ExtractSection(qso.ContestExchangeReceived);
            if (!string.IsNullOrWhiteSpace(section))
            {
                sections.Add(section);
            }
        }

        return new ContestScoreResult(totalPoints, qsoCount, sections);
    }

    /// Extracts the section from a Field-Day-style exchange ("3A GA" -> "GA").
    /// Field Day exchanges are "Class Section" — the section is the last token.
    /// Returns null if the exchange is empty or has no discernible section.
    private static string? ExtractSection(string? exchangeReceived)
    {
        if (string.IsNullOrWhiteSpace(exchangeReceived)) return null;

        var parts = exchangeReceived.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[^1] : null;
    }
}