using System.Globalization;

namespace Tarpg.Sim;

// Pure-logic formatters for the tarpg-sim CLI progress output. Lifted out
// of Program.cs so the strings are unit-testable without spawning a process
// and capturing stdout. Each method returns a single line; the caller
// (Program.cs) decides where to emit it.
public static class SimProgress
{
    // One line per completed run. Padded so progress lines align in a
    // terminal and the eye can scan outcomes vertically.
    //
    //   [  1/250] F1 seed=1000 cleared in  4.2s (12 kills, hp=65/65)
    public static string FormatRunLine(
        int index, int total,
        int floor, int seed,
        SimResult result, int playerMaxHp)
    {
        var indexW = total.ToString(CultureInfo.InvariantCulture).Length;
        var indexStr = (index + 1).ToString(CultureInfo.InvariantCulture).PadLeft(indexW);
        var totalStr = total.ToString(CultureInfo.InvariantCulture);

        var outcome = result.Outcome switch
        {
            SimOutcome.PlayerCleared => "cleared",
            SimOutcome.PlayerDied    => "died   ",
            SimOutcome.Timeout       => "timeout",
            _                        => "?      ",
        };

        return string.Format(
            CultureInfo.InvariantCulture,
            "[{0}/{1}] F{2} seed={3} {4} in {5,5:F1}s ({6} kills, hp={7}/{8})",
            indexStr, totalStr,
            floor, seed,
            outcome,
            result.SimSeconds,
            result.EnemiesKilled,
            result.PlayerHpAtEnd, playerMaxHp);
    }

    // End-of-floor banner. Emitted after every run for that floor lands so
    // the user sees a rolling summary of how each depth shook out.
    //
    //   === F3 (25/25) cleared 92% died 8% timeout 0% kills_avg=12.4 wall=2m17s ===
    public static string FormatFloorSummary(
        int floor, int floorRunCount,
        int cleared, int died, int timeout,
        double killsAvg, double wallSeconds)
    {
        var n = floorRunCount;
        return string.Format(
            CultureInfo.InvariantCulture,
            "=== F{0} ({1}/{2}) cleared {3}% died {4}% timeout {5}% kills_avg={6:F1} wall={7} ===",
            floor, n, n,
            cleared * 100 / n,
            died * 100 / n,
            timeout * 100 / n,
            killsAvg,
            FormatWallTime(wallSeconds));
    }

    public static string FormatWallTime(double seconds)
    {
        if (seconds < 60.0)
            return string.Format(CultureInfo.InvariantCulture, "{0:F1}s", seconds);
        var minutes = (int)(seconds / 60.0);
        var remainder = (int)(seconds - minutes * 60.0);
        return string.Format(CultureInfo.InvariantCulture, "{0}m{1:D2}s", minutes, remainder);
    }
}
