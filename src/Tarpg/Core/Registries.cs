using Tarpg.Bosses;
using Tarpg.Classes;
using Tarpg.Enemies;
using Tarpg.Items;
using Tarpg.Modifiers;
using Tarpg.Skills;
using Tarpg.World;

namespace Tarpg.Core;

// Central typed registries. Lookup by string id; iteration via .All.
// Adding a new content category = add a registry here AND a route in
// ContentInitializer.RegistryFor.
public static class Registries
{
    public static readonly Registry<TileTypeDefinition>    TileTypes = new();
    public static readonly Registry<WalkerClassDefinition> Classes   = new();
    public static readonly Registry<ItemDefinition>        Items     = new();
    public static readonly Registry<SkillDefinition>       Skills    = new();
    public static readonly Registry<EnemyDefinition>       Enemies   = new();
    public static readonly Registry<ModifierDefinition>    Modifiers = new();
    public static readonly Registry<BossDefinition>        Bosses    = new();
}
