using SadRogue.Primitives;
using Tarpg.Classes;
using Tarpg.Combat;
using Tarpg.Core;
using Tarpg.Enemies;
using Tarpg.Entities;

namespace Tarpg.Tests.Combat;

public class CombatControllerTests
{
    private static Player MakePlayer(Position at) => Player.Create(Reaver.Definition, at);
    private static Enemy MakeWolf(Position at) => Enemy.Create(Wolf.Definition, at);

    [Fact]
    public void TryAttack_TargetInRange_DealsBaseDamageAndSetsCooldown()
    {
        var attacker = MakePlayer(new Position(5, 5));
        var target = MakeWolf(new Position(6, 5)); // 1 tile away ≤ MeleeRange 1.4
        var combat = new CombatController();
        combat.SetTarget(target);

        var initialHp = target.Health;
        var hit = combat.TryAttack(attacker, deltaSec: 0.05f);

        Assert.True(hit);
        Assert.Equal(initialHp - CombatController.BaseDamage, target.Health);
        Assert.True(combat.CooldownRemaining > 0f);
    }

    [Fact]
    public void TryAttack_TargetOutOfRange_DoesNothing()
    {
        var attacker = MakePlayer(new Position(5, 5));
        var target = MakeWolf(new Position(10, 5)); // 5 tiles away
        var combat = new CombatController();
        combat.SetTarget(target);

        var initialHp = target.Health;
        var hit = combat.TryAttack(attacker, 0.05f);

        Assert.False(hit);
        Assert.Equal(initialHp, target.Health);
    }

    [Fact]
    public void TryAttack_WhileCoolingDown_DoesNotFireAgain()
    {
        var attacker = MakePlayer(new Position(5, 5));
        var target = MakeWolf(new Position(6, 5));
        var combat = new CombatController();
        combat.SetTarget(target);

        combat.TryAttack(attacker, 0.05f); // first swing lands
        var hpAfterFirst = target.Health;

        // Second call within the cooldown window — no second hit.
        var hit = combat.TryAttack(attacker, 0.05f);

        Assert.False(hit);
        Assert.Equal(hpAfterFirst, target.Health);
    }

    [Fact]
    public void TryAttack_AfterCooldownElapses_FiresAgain()
    {
        var attacker = MakePlayer(new Position(5, 5));
        var target = MakeWolf(new Position(6, 5));
        target.MaxHealth = 1000;
        target.Health = 1000;
        var combat = new CombatController();
        combat.SetTarget(target);

        combat.TryAttack(attacker, 0.05f); // first swing — sets cooldown
        // Burn off the cooldown a tick at a time so each call exercises
        // the same TryAttack path. Total elapsed > AutoAttackCooldownSec.
        for (var i = 0; i < 20; i++)
            combat.TryAttack(attacker, 0.05f);

        // Second swing fires now.
        var hpBefore = target.Health;
        var hit = combat.TryAttack(attacker, 0.05f);

        Assert.True(hit);
        Assert.Equal(hpBefore - CombatController.BaseDamage, target.Health);
    }

    [Fact]
    public void Clear_DropsTargetAndCooldown()
    {
        var attacker = MakePlayer(new Position(5, 5));
        var target = MakeWolf(new Position(6, 5));
        var combat = new CombatController();
        combat.SetTarget(target);
        combat.TryAttack(attacker, 0.05f);

        combat.Clear();

        Assert.Null(combat.Target);
        Assert.Equal(0f, combat.CooldownRemaining);
    }
}
