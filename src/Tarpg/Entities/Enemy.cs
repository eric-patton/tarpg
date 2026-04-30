using Tarpg.Core;
using Tarpg.Enemies;
using Tarpg.Enemies.Ai;

namespace Tarpg.Entities;

public sealed class Enemy : Entity
{
    public required EnemyDefinition Definition { get; init; }
    public required IEnemyAi Ai { get; init; }

    // Per-instance damage. Initialized from Definition.BaseDamage by Create;
    // GameScreen.SpawnEnemy may scale it at higher floor depths. AIs read
    // this field (not Definition.BaseDamage) so per-floor scaling propagates
    // to every attack without mutating the shared Definition singleton.
    public int Damage { get; set; }

    public override int RenderLayer => 50;

    public static Enemy Create(EnemyDefinition def, Position spawnTile)
    {
        var enemy = new Enemy
        {
            Glyph = def.Glyph,
            Color = def.Color,
            Name = def.Name,
            Definition = def,
            MaxHealth = def.BaseHealth,
            Health = def.BaseHealth,
            Damage = def.BaseDamage,
            Ai = ResolveAi(def),
        };
        enemy.SetTile(spawnTile);
        return enemy;
    }

    // Maps EnemyDefinition.AiTag to a fresh IEnemyAi. Switch-on-string for
    // v0; if the tag set ever exceeds ~5 entries this becomes a registry-
    // style table lookup. Definition is passed so the AI can read its own
    // MoveSpeed / AttackCooldown from the registry data.
    private static IEnemyAi ResolveAi(EnemyDefinition def) => def.AiTag switch
    {
        "melee_charger" => new MeleeChargerAi(def),
        "melee_skirmisher" => new SkirmisherAi(def),
        "ranged_kiter" => new RangedKiterAi(def),
        _ => throw new InvalidOperationException(
            $"No IEnemyAi registered for AiTag '{def.AiTag}'."),
    };
}
