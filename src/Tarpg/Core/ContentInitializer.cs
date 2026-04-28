using System.Reflection;
using Tarpg.Bosses;
using Tarpg.Classes;
using Tarpg.Enemies;
using Tarpg.Items;
using Tarpg.Modifiers;
using Tarpg.Skills;
using Tarpg.World;

namespace Tarpg.Core;

// Walks the assembly at startup, finds every public static readonly field
// whose type is one of the known IRegistryEntry subtypes, and registers it
// with the appropriate registry.
//
// Adding a new piece of content = drop a new file with:
//   public static readonly XDefinition Foo = new() { Id = "foo", ... };
// No edits to a central list required.
//
// Adding a new content CATEGORY = add a registry to Registries.cs and a
// route to RegistryFor() below.
public static class ContentInitializer
{
    private static bool _initialized;
    private static readonly object _lock = new();

    public static void Initialize()
    {
        lock (_lock)
        {
            if (_initialized) return;
            _initialized = true;

            var assembly = typeof(ContentInitializer).Assembly;
            var routes = BuildRouteMap();

            // Touch a static field on each definition's containing namespace
            // anchor so types referenced only by reflection still get loaded.
            // Iterating GetTypes() handles that for us in practice.
            var entryTypes = routes.Keys;

            foreach (var type in assembly.GetTypes())
            {
                // Skip the registry-entry type definitions themselves; we want
                // their consumers (the static "definition holder" classes).
                if (entryTypes.Contains(type)) continue;

                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
                {
                    if (!field.IsInitOnly) continue;
                    if (!routes.TryGetValue(field.FieldType, out var register)) continue;

                    var entry = (IRegistryEntry?)field.GetValue(null);
                    if (entry is null) continue;

                    register(entry);
                }
            }
        }
    }

    // Wiring of definition type -> registry. New categories add a line here.
    private static Dictionary<Type, Action<IRegistryEntry>> BuildRouteMap() => new()
    {
        [typeof(TileTypeDefinition)]    = e => Registries.TileTypes.Register((TileTypeDefinition)e),
        [typeof(WalkerClassDefinition)] = e => Registries.Classes.Register((WalkerClassDefinition)e),
        [typeof(ItemDefinition)]        = e => Registries.Items.Register((ItemDefinition)e),
        [typeof(SkillDefinition)]       = e => Registries.Skills.Register((SkillDefinition)e),
        [typeof(EnemyDefinition)]       = e => Registries.Enemies.Register((EnemyDefinition)e),
        [typeof(ModifierDefinition)]    = e => Registries.Modifiers.Register((ModifierDefinition)e),
        [typeof(BossDefinition)]        = e => Registries.Bosses.Register((BossDefinition)e),
    };
}
