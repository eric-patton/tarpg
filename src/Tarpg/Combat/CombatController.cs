using System.Numerics;
using Tarpg.Entities;

namespace Tarpg.Combat;

// v0 melee combat: a single attack target, a fixed range, a fixed cooldown,
// fixed damage. The system grows with skills and item modifiers later.
//
// Driving model: GameScreen owns the combat + movement controllers. Each
// frame, if there's a live target, GameScreen tells movement to approach
// the target's position; once in range, GameScreen calls TryAttack and
// pauses movement. Combat does NOT mutate movement directly — keeps the
// two systems independent.
public sealed class CombatController
{
    public const float MeleeRange = 1.4f;             // tiles (1 + a touch for diagonals)
    public const float AutoAttackCooldownSec = 0.8f;
    // Unarmed baseline. Players read this through Player.WeaponDamage
    // (which adds any equipped weapon's WeaponDamageBonus on top), so
    // the const stays the source-of-truth for "what does a fist do."
    public const int BaseDamage = 10;

    public Entity? Target { get; private set; }
    public float CooldownRemaining { get; private set; }

    // When true, the player should not approach the target (Shift+click semantics).
    // The player will only attack if the target is already in range.
    public bool ForceStand { get; private set; }

    public bool IsTargetAlive => Target is { IsDead: false };

    public void SetTarget(Entity target, bool forceStand = false)
    {
        Target = target;
        ForceStand = forceStand;
        CooldownRemaining = 0f; // first swing fires the moment we're in range
    }

    public void Clear()
    {
        Target = null;
        ForceStand = false;
        CooldownRemaining = 0f;
    }

    // Tick the cooldown. Returns true if an attack was applied this tick.
    public bool TryAttack(Entity attacker, float deltaSec)
    {
        if (!IsTargetAlive) { Target = null; return false; }

        if (CooldownRemaining > 0f)
        {
            CooldownRemaining -= deltaSec;
            return false;
        }

        var distance = Vector2.Distance(attacker.ContinuousPosition, Target!.ContinuousPosition);
        if (distance > MeleeRange) return false;

        // Players add equipped-weapon bonus on top of the BaseDamage
        // baseline; non-player attackers (none today, but defensive)
        // fall back to BaseDamage so the system stays generic.
        var dmg = attacker is Player p ? p.WeaponDamage : BaseDamage;
        Target.TakeDamage(dmg);
        CooldownRemaining = AutoAttackCooldownSec;

        if (Target.IsDead) Target = null;
        return true;
    }
}
