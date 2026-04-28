using Tarpg.Core;
using Tarpg.Enemies;
using Tarpg.Enemies.Ai;

namespace Tarpg.Entities;

public sealed class Enemy : Entity
{
    public required EnemyDefinition Definition { get; init; }
    public required IEnemyAi Ai { get; init; }

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
            Ai = ResolveAi(def.AiTag),
        };
        enemy.SetTile(spawnTile);
        return enemy;
    }

    // Maps EnemyDefinition.AiTag to a fresh IEnemyAi. Switch-on-string for
    // v0; if the tag set ever exceeds ~5 entries this becomes a registry-
    // style table lookup.
    private static IEnemyAi ResolveAi(string tag) => tag switch
    {
        "melee_charger" => new MeleeChargerAi(),
        _ => throw new InvalidOperationException(
            $"No IEnemyAi registered for AiTag '{tag}'."),
    };
}
