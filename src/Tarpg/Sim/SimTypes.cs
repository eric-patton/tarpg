using Tarpg.Core;

namespace Tarpg.Sim;

public enum SimOutcome
{
    PlayerCleared,    // floor cleared (threshold reached or all enemies dead)
    PlayerDied,       // player HP hit zero
    Timeout,          // MaxTicks elapsed before either outcome
}

// Inputs to a single simulation run. One run = one floor of one zone with
// one class. Multi-floor tuning runs invoke TickRunner separately per floor.
public sealed record SimConfig
{
    public required string ZoneId { get; init; }
    public required string ClassId { get; init; }
    public required int Floor { get; init; }
    public required int Seed { get; init; }

    // 5 sim-minutes at 60 Hz default. Floors that take longer than this are
    // either too hard (player dies) or generated with broken connectivity
    // (kept as a safety belt against runaway loops).
    public int MaxTicks { get; init; } = 60 * 60 * 5;
    public float TickDeltaSec { get; init; } = 1f / 60f;

    // Map dimensions match GameScreen's WorldWidth / WorldHeight so spawn
    // density and pack expansion behave the same as live play.
    public int MapWidth { get; init; } = 160;
    public int MapHeight { get; init; } = 60;
}

// Aggregate metrics for a single simulation run.
public sealed record SimResult
{
    public required SimOutcome Outcome { get; init; }
    public required int TicksElapsed { get; init; }
    public required float SimSeconds { get; init; }

    public required int InitialEnemyCount { get; init; }
    public required int EnemiesKilled { get; init; }

    public required int PlayerDamageDealt { get; init; }
    public required int PlayerDamageTaken { get; init; }
    public required int PlayerHpAtEnd { get; init; }
    public required int PlayerHpMin { get; init; }

    public required int SkillUses { get; init; }
    public required IReadOnlyDictionary<string, int> KillsByEnemyId { get; init; }
}
