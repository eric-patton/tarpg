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
}
