using Tarpg.Sim;

namespace Tarpg.Tests.Sim;

public class SimProgressTests
{
    private static SimResult MakeResult(SimOutcome outcome, int kills = 12, int hp = 65, float secs = 4.2f) =>
        new()
        {
            Outcome = outcome,
            TicksElapsed = (int)(secs * 60),
            SimSeconds = secs,
            InitialEnemyCount = 12,
            EnemiesKilled = kills,
            PlayerDamageDealt = kills * 10,
            PlayerDamageTaken = 8,
            PlayerHpAtEnd = hp,
            PlayerHpMin = hp - 5,
            SkillUses = 3,
            KillsByEnemyId = new Dictionary<string, int> { ["wolf"] = kills },
        };

    [Fact]
    public void FormatRunLine_ClearedRun_RendersExpected()
    {
        var line = SimProgress.FormatRunLine(
            index: 0, total: 250,
            floor: 1, seed: 1000,
            result: MakeResult(SimOutcome.PlayerCleared, kills: 12, hp: 65, secs: 4.2f),
            playerMaxHp: 65);

        Assert.Equal("[  1/250] F1 seed=1000 cleared in   4.2s (12 kills, hp=65/65)", line);
    }

    [Fact]
    public void FormatRunLine_DeadRun_RendersDiedTag()
    {
        var line = SimProgress.FormatRunLine(
            index: 99, total: 250,
            floor: 12, seed: 9999,
            result: MakeResult(SimOutcome.PlayerDied, kills: 4, hp: 0, secs: 18.7f),
            playerMaxHp: 65);

        Assert.Contains("died", line);
        Assert.Contains("F12", line);
        Assert.Contains("hp=0/65", line);
    }

    [Fact]
    public void FormatRunLine_IndexPaddingMatchesTotalWidth()
    {
        // Total = 9999 → 4-character index field. Index 5 → "   6" with leading spaces.
        var line = SimProgress.FormatRunLine(
            index: 5, total: 9999,
            floor: 1, seed: 1000,
            result: MakeResult(SimOutcome.PlayerCleared),
            playerMaxHp: 65);

        Assert.StartsWith("[   6/9999]", line);
    }

    [Fact]
    public void FormatFloorSummary_ComputesPercentages()
    {
        var line = SimProgress.FormatFloorSummary(
            floor: 5, floorRunCount: 25,
            cleared: 23, died: 2, timeout: 0,
            killsAvg: 12.4, wallSeconds: 137.0);

        Assert.Contains("F5", line);
        Assert.Contains("cleared 92%", line);
        Assert.Contains("died 8%", line);
        Assert.Contains("timeout 0%", line);
        Assert.Contains("kills_avg=12.4", line);
        Assert.Contains("wall=2m17s", line);
    }

    [Theory]
    [InlineData(5.0, "5.0s")]
    [InlineData(45.7, "45.7s")]
    [InlineData(60.0, "1m00s")]
    [InlineData(125.5, "2m05s")]
    [InlineData(3600.0, "60m00s")]
    public void FormatWallTime_RendersExpected(double seconds, string expected)
    {
        Assert.Equal(expected, SimProgress.FormatWallTime(seconds));
    }
}
