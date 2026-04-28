using System.Numerics;
using SadRogue.Primitives;
using Tarpg.Core;

namespace Tarpg.Entities;

// Anything that occupies a tile and can be drawn / interacted with.
// Players, enemies, NPCs, bound Echoes, dropped items all inherit from this.
//
// Position model: ContinuousPosition is the source of truth (tile-space floats,
// e.g. (12.5, 8.3) means the cell column 12, halfway across, 30% down). Position
// (the integer struct) is derived for tile-grid lookups (which cell am I in?).
// Movement systems write ContinuousPosition; game logic reads either.
public abstract class Entity
{
    public Vector2 ContinuousPosition { get; set; }

    public Position Position =>
        new((int)MathF.Floor(ContinuousPosition.X), (int)MathF.Floor(ContinuousPosition.Y));

    public required char Glyph { get; init; }
    public required Color Color { get; init; }
    public string? Name { get; init; }

    public int MaxHealth { get; set; } = 1;
    public int Health { get; set; } = 1;

    public bool IsDead => Health <= 0;

    // Render order: lower draws first (under). Items < creatures < player.
    public virtual int RenderLayer => 10;

    // Place the entity centered on a tile. Use for spawning / teleports.
    public void SetTile(Position p) =>
        ContinuousPosition = new Vector2(p.X + 0.5f, p.Y + 0.5f);

    // Subtracts damage from Health, clamped at zero. Combat / future skill
    // resolution funnels through here so we have one place to hook on-hit
    // effects, damage logging, and (later) the juice pass.
    public void TakeDamage(int amount)
    {
        if (amount <= 0) return;
        Health = Math.Max(0, Health - amount);
    }
}
