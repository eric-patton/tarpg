using Tarpg.Core;
using Tarpg.Entities;
using Tarpg.World;

namespace Tarpg.Sim;

// What the player "does" each tick. Pilots issue movement / combat / skill
// commands; the GameLoopController.Tick that follows resolves them. New
// pilots (random-action, optimal, replay-from-input) can land later
// without changing the runner.
public interface ISimPilot
{
    void Tick(SimContext ctx);
}

public sealed class SimContext
{
    public required GameLoopController Loop { get; init; }
    public required Position FloorThreshold { get; init; }
    public required Random Rng { get; init; }

    // Incremented by the pilot when it casts a skill — the runner uses
    // this for the SkillUses metric. Public so the pilot can bump it.
    public int SkillCastsThisRun { get; set; }
}
