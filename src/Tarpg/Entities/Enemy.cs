using Tarpg.Core;
using Tarpg.Enemies;

namespace Tarpg.Entities;

public sealed class Enemy : Entity
{
    public required EnemyDefinition Definition { get; init; }

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
        };
        enemy.SetTile(spawnTile);
        return enemy;
    }
}
