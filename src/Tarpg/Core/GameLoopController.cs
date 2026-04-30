using System.Numerics;
using Tarpg.Combat;
using Tarpg.Entities;
using Tarpg.Movement;
using Tarpg.Skills;
using Tarpg.World;

namespace Tarpg.Core;

// The headless per-frame logic loop. Owns movement / combat / AI / regen /
// skill-cooldown advancement; emits flags for the outer orchestrator
// (GameScreen for live play, TickRunner for simulation) to react to —
// floor descent, player death, etc.
//
// What the controller does NOT own:
//   - rendering, camera, hit feedback, click indicator, skill VFX
//   - dash visual lerping (caller decides whether the player position
//     snap from a teleport should animate or not)
//   - reaping dead enemies from the live list (caller mutates _enemies
//     to keep its own visual / metric state in sync)
//
// The dash interaction is mediated by CastResult: a teleport-style skill
// (Charge today) returns Teleported=true with PreCastPosition / PostCastPosition.
// The caller can choose to roll the player back to PreCast and animate, or
// leave them at PostCast for an instant snap.
public sealed class GameLoopController
{
    public const float OutOfCombatRegenDelaySec = 3.0f;
    public const float RegenPerSec = 5.0f;
    public const int ResourceGainPerAutoAttackHit = 5;

    public const int SlotCount = 5;
    public const int SlotIndexM2 = 0;
    public const int SlotIndexQ = 1;
    public const int SlotIndexW = 2;
    public const int SlotIndexE = 3;
    public const int SlotIndexR = 4;

    public const int FovRadius = 10;

    public Player Player => _player;
    public List<Enemy> Enemies => _enemies;
    public Map Map { get => _map; set => _map = value; }
    public MovementController Movement => _movement;
    public CombatController Combat => _combat;
    public IReadOnlyList<SkillDefinition?> Slots => _slotSkills;
    public IReadOnlyList<float> Cooldowns => _slotCooldowns;

    // Set by Tick. Caller checks and reacts (descent / regen).
    public bool SteppedOnThreshold { get; private set; }
    public bool PlayerDied { get; private set; }

    private readonly Player _player;
    private readonly List<Enemy> _enemies;
    private readonly MovementController _movement;
    private readonly CombatController _combat;
    private Map _map;

    private float _timeSinceLastDamage = OutOfCombatRegenDelaySec;
    private float _regenAccumulator;

    private readonly SkillDefinition?[] _slotSkills = new SkillDefinition?[SlotCount];
    private readonly float[] _slotCooldowns = new float[SlotCount];

    public GameLoopController(
        Player player,
        List<Enemy> enemies,
        Map map,
        MovementController movement,
        CombatController combat)
    {
        _player = player;
        _enemies = enemies;
        _movement = movement;
        _combat = combat;
        _map = map;
        _player.Damaged += OnPlayerDamaged;
    }

    public void SetSlotSkill(int index, SkillDefinition? skill) => _slotSkills[index] = skill;

    // Reset transient state on floor reload (descent or death). Caller has
    // already swapped Map and respawned enemies; we just clear the loop's
    // own timers / cooldowns / combat / movement.
    public void OnFloorLoaded(bool resetResource)
    {
        Array.Clear(_slotCooldowns, 0, _slotCooldowns.Length);
        _regenAccumulator = 0f;
        _timeSinceLastDamage = OutOfCombatRegenDelaySec;
        _combat.Clear();
        _movement.Stop();
        if (resetResource) _player.Resource = 0;
    }

    public void GrantResourceOnHit()
    {
        _player.Resource = Math.Min(
            _player.MaxResource,
            _player.Resource + ResourceGainPerAutoAttackHit);
    }

    // Try to cast the skill in the given slot at the given target cell.
    // Gates: slot populated, cooldown clear, resource available.
    // Returns: success flag plus pre/post-cast position so the caller can
    // animate teleports (Charge) or leave the snap instantaneous (sim).
    public CastResult TryCastSkill(int slotIndex, Position target, ISkillVfx? vfx)
    {
        var def = _slotSkills[slotIndex];
        if (def is null) return CastResult.Fail;
        if (_slotCooldowns[slotIndex] > 0f) return CastResult.Fail;
        if (_player.Resource < def.Cost) return CastResult.Fail;

        var prePos = _player.ContinuousPosition;
        var ctx = new SkillContext
        {
            Caster = _player,
            Target = target,
            Map = _map,
            Hostiles = _enemies.Cast<Entity>().ToList(),
            Vfx = vfx,
        };
        def.Behavior.Execute(ctx);
        _player.Resource -= def.Cost;
        _slotCooldowns[slotIndex] = def.CooldownSec;

        var postPos = _player.ContinuousPosition;
        return new CastResult
        {
            Success = true,
            Teleported = postPos != prePos,
            PreCastPosition = prePos,
            PostCastPosition = postPos,
        };
    }

    public Enemy? FindLiveEnemyAt(Position cell, float radius)
    {
        var clickPos = new Vector2(cell.X + 0.5f, cell.Y + 0.5f);
        Enemy? best = null;
        var bestDistSq = radius * radius;
        foreach (var enemy in _enemies)
        {
            if (enemy.IsDead) continue;
            var d = Vector2.DistanceSquared(enemy.ContinuousPosition, clickPos);
            if (d <= bestDistSq) { best = enemy; bestDistSq = d; }
        }
        return best;
    }

    // Drive one frame of game logic. Caller passes deltaSec, the cell aspect
    // ratio (for direction-uniform movement speed), the frozen flag (true to
    // pause movement / combat / AI for hit-stop or dash-visual windows), and
    // the player's tile from the previous tick (for threshold detection +
    // FOV recompute on movement). Caller checks SteppedOnThreshold and
    // PlayerDied after Tick returns.
    public void Tick(float deltaSec, float cellAspect, bool frozen, Position lastPlayerTile)
    {
        SteppedOnThreshold = false;
        PlayerDied = false;

        if (!frozen)
        {
            if (_combat.IsTargetAlive)
            {
                var target = _combat.Target!;
                var distance = Vector2.Distance(_player.ContinuousPosition, target.ContinuousPosition);

                if (!_combat.ForceStand && distance > CombatController.MeleeRange)
                {
                    _movement.RetargetTo(target.ContinuousPosition, _player.ContinuousPosition, _map);
                    _movement.Tick(_player, _map, deltaSec, cellAspect);
                }
                else
                {
                    _movement.Stop();
                    if (_combat.TryAttack(_player, deltaSec))
                        GrantResourceOnHit();
                }
            }
            else
            {
                _movement.Tick(_player, _map, deltaSec, cellAspect);
            }
        }

        // Tile transition: detect threshold (signal to caller) or refresh FOV
        // at the new tile. Caller is responsible for the actual descent so it
        // can sync its visual / spawn state when the floor swaps.
        if (_player.Position != lastPlayerTile)
        {
            if (_map[_player.Position].Type == TileTypes.Threshold)
            {
                SteppedOnThreshold = true;
            }
            else
            {
                _map.ComputeFovFor(_player.Position, FovRadius);
            }
        }

        if (!frozen)
        {
            foreach (var enemy in _enemies)
            {
                if (enemy.IsDead) continue;
                enemy.Ai.Tick(enemy, _player, _map, deltaSec, cellAspect);
            }
        }

        if (_player.IsDead)
            PlayerDied = true;

        // Cooldowns decay through the frozen window (matches Diablo-style
        // "your CDs keep ticking even during impact freeze").
        for (var i = 0; i < SlotCount; i++)
        {
            if (_slotCooldowns[i] > 0f)
                _slotCooldowns[i] = MathF.Max(0f, _slotCooldowns[i] - deltaSec);
        }

        TickHpRegen(deltaSec);
    }

    private void TickHpRegen(float deltaSec)
    {
        _timeSinceLastDamage += deltaSec;
        if (_timeSinceLastDamage < OutOfCombatRegenDelaySec) return;
        if (_player.Health >= _player.MaxHealth) { _regenAccumulator = 0f; return; }

        _regenAccumulator += deltaSec * RegenPerSec;
        var heal = (int)_regenAccumulator;
        if (heal <= 0) return;
        _regenAccumulator -= heal;
        _player.Health = Math.Min(_player.MaxHealth, _player.Health + heal);
    }

    private void OnPlayerDamaged(Entity entity, int amount)
    {
        _timeSinceLastDamage = 0f;
    }
}

public readonly struct CastResult
{
    public bool Success { get; init; }
    public bool Teleported { get; init; }
    public Vector2 PreCastPosition { get; init; }
    public Vector2 PostCastPosition { get; init; }

    public static readonly CastResult Fail = default;
}
