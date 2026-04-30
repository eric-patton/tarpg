using Tarpg.Sim;

namespace Tarpg.Tests.Sim;

// Load-bearing claim that makes parallel sweeps safe: TickRunner.Run with
// the same config and seed must produce a bit-identical SimResult whether
// invoked serially or concurrently from many threads. Any shared mutable
// state inside the run path (a static cache, singleton RNG, etc.) would
// surface here as a result divergence.
//
// If this test goes flaky, do NOT mark it [Flaky] — investigate. The
// CLI's parallel mode and any future cluster-style runner depend on this
// guarantee directly.
public class TickRunnerParallelismTests
{
    // MaxTicks cap on every Run call: the property under test is "concurrent
    // invocation produces the same SimResult as serial," not "the floor
    // clears." Capping the run keeps test wall-time bounded regardless of
    // whether the chosen seed naturally clears or runs to MaxTicks.
    private const int TestMaxTicks = 3500;

    [Theory]
    [InlineData(1, 1000)]
    [InlineData(1, 1001)]
    public void Run_SameConfig_IsIdenticalUnderConcurrentInvocation(int floor, int seed)
    {
        var cfg = new SimConfig
        {
            ZoneId = "wolfwood",
            ClassId = "reaver",
            Floor = floor,
            Seed = seed,
            MaxTicks = TestMaxTicks,
        };

        var serial = TickRunner.Run(cfg, new GreedySimPilot());

        const int Workers = 4;
        var concurrent = new SimResult[Workers];
        Parallel.For(0, Workers, i =>
        {
            concurrent[i] = TickRunner.Run(cfg, new GreedySimPilot());
        });

        foreach (var r in concurrent)
        {
            Assert.Equal(serial.Outcome, r.Outcome);
            Assert.Equal(serial.TicksElapsed, r.TicksElapsed);
            Assert.Equal(serial.PlayerHpAtEnd, r.PlayerHpAtEnd);
            Assert.Equal(serial.PlayerHpMin, r.PlayerHpMin);
            Assert.Equal(serial.EnemiesKilled, r.EnemiesKilled);
            Assert.Equal(serial.PlayerDamageDealt, r.PlayerDamageDealt);
            Assert.Equal(serial.PlayerDamageTaken, r.PlayerDamageTaken);
            Assert.Equal(serial.SkillUses, r.SkillUses);
        }
    }

    [Fact]
    public void Run_DistinctConfigs_DoNotInterfereUnderConcurrency()
    {
        // Each (floor, seed) pair has its own ground-truth result. Running
        // a mix of pairs concurrently must produce the same result for each
        // pair as running it alone — i.e. no cross-thread state bleed.
        var pairs = new (int Floor, int Seed)[]
        {
            (1, 1001), (1, 1002), (2, 1003), (2, 1004),
        };

        var expected = pairs.ToDictionary(
            p => p,
            p => TickRunner.Run(new SimConfig
            {
                ZoneId = "wolfwood", ClassId = "reaver",
                Floor = p.Floor, Seed = p.Seed,
                MaxTicks = TestMaxTicks,
            }, new GreedySimPilot()));

        var observed = new System.Collections.Concurrent.ConcurrentDictionary<(int, int), SimResult>();
        Parallel.ForEach(pairs, p =>
        {
            var r = TickRunner.Run(new SimConfig
            {
                ZoneId = "wolfwood", ClassId = "reaver",
                Floor = p.Floor, Seed = p.Seed,
                MaxTicks = TestMaxTicks,
            }, new GreedySimPilot());
            observed[p] = r;
        });

        foreach (var p in pairs)
        {
            var e = expected[p];
            var o = observed[p];
            Assert.Equal(e.Outcome, o.Outcome);
            Assert.Equal(e.TicksElapsed, o.TicksElapsed);
            Assert.Equal(e.PlayerHpAtEnd, o.PlayerHpAtEnd);
            Assert.Equal(e.EnemiesKilled, o.EnemiesKilled);
        }
    }
}
