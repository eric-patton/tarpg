using Tarpg.Sim;

namespace Tarpg.Tests.Sim;

// Smoke tests for the simulation harness itself. These are NOT real balance
// invariants — they're "the harness produces a sensible result" sanity
// checks. Real balance tuning happens via the tarpg-sim CLI on hundreds of
// seeds. Keep these fixed-seed and weak so they don't flake while we
// iterate on enemy / class numbers.
public class WolfwoodBalanceTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(13)]
    [InlineData(21)]
    [InlineData(99)]
    public void Reaver_ClearsF1_OnFixedSeeds(int seed)
    {
        var cfg = new SimConfig
        {
            ZoneId = "wolfwood",
            ClassId = "reaver",
            Floor = 1,
            Seed = seed,
        };

        var result = TickRunner.Run(cfg, new GreedySimPilot());

        Assert.NotEqual(SimOutcome.Timeout, result.Outcome);
        // Win-rate isn't a hard floor for any single seed — the test
        // passes if the sim terminates with a real outcome and at least
        // some kills happened. The CLI runner is what enforces aggregate
        // expectations across many seeds.
        Assert.True(result.EnemiesKilled > 0,
            $"Greedy pilot killed nothing on F1 seed {seed} ({result.Outcome}).");
    }

    [Fact]
    public void Reaver_F1_TakesSomeDamage()
    {
        var cfg = new SimConfig
        {
            ZoneId = "wolfwood",
            ClassId = "reaver",
            Floor = 1,
            Seed = 12345,
        };

        var result = TickRunner.Run(cfg, new GreedySimPilot());

        // Greedy pilot melees everything — should take damage on F1, even
        // if it survives. Catches a regression where enemies stop hitting
        // the player (broken AI / FOV plumbing in the loop).
        Assert.True(result.PlayerDamageTaken > 0,
            "Expected greedy melee pilot to take some damage on F1.");
    }

    [Fact]
    public void DeeperFloor_DoesNotCrashHarness()
    {
        var cfg = new SimConfig
        {
            ZoneId = "wolfwood",
            ClassId = "reaver",
            Floor = 12,
            Seed = 77,
        };

        // Floor 12 may kill the player — that's fine. Just want to confirm
        // the harness terminates without throwing on a deep, scaled floor.
        var result = TickRunner.Run(cfg, new GreedySimPilot());

        Assert.NotEqual(SimOutcome.Timeout, result.Outcome);
    }
}
