using Tarpg.Core;
using Tarpg.Entities;
using Tarpg.World;

namespace Tarpg.Skills;

// Everything a skill behavior needs to resolve its effect.
// Grows as combat systems land — kept minimal until then.
public sealed class SkillContext
{
    public required Entity Caster { get; init; }
    public required Position Target { get; init; }
    public required Map Map { get; init; }

    // Live enemies the skill can target. AOE / cleave behaviors filter this
    // by distance / direction; single-target behaviors look up the entity
    // at Target. GameScreen passes its current enemy list when activating.
    public required IReadOnlyList<Entity> Hostiles { get; init; }

    // Optional visual-effect hooks. Null in headless / test contexts;
    // GameScreen plugs in the concrete UI-side renderer when activating
    // skills in the running game. Skills should null-check before calling.
    public ISkillVfx? Vfx { get; init; }
}
