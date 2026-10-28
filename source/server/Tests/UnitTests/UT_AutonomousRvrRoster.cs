using System.Linq;
using NUnit.Framework;
using static DOL.GS.AutonomousRvrRoster;

namespace DOL.GS.Tests;

[TestFixture]
public sealed class UT_AutonomousRvrRoster
{
    private const Role A = Role.Attacker, S = Role.Speed, H = Role.Healer, C = Role.Control, T = Role.Tank;

    [Test]
    public void AnEightAsksForTwoHealersOneSpeedOneControlAndOneTank()
    {
        var needs = Requirements(8).ToDictionary(need => need.Role, need => need.Count);
        Assert.That(needs, Is.EqualTo(new System.Collections.Generic.Dictionary<Role, int>
            { [Role.Healer] = 2, [Role.Speed] = 1, [Role.Control] = 1, [Role.Tank] = 1 }));
    }

    [Test]
    public void ASmallerPartyDropsTankThenControlThenSpeedThenHealers()
    {
        Assert.That(Requirements(4).Select(need => need.Role), Is.EquivalentTo(new[] { Role.Healer, Role.Speed, Role.Control }));
        Assert.That(Requirements(3).Select(need => need.Role), Is.EquivalentTo(new[] { Role.Healer, Role.Speed }));
        Assert.That(Requirements(2).Select(need => need.Role), Is.EquivalentTo(new[] { Role.Healer }));
        Assert.That(Requirements(2).Single().Count, Is.EqualTo(2));
        Assert.That(Requirements(1).Single().Count, Is.EqualTo(1));
    }

    [Test]
    public void TheRolesAreFoundAmongManyAttackers()
    {
        // Leader is an attacker; the pool is eight attackers, then a speed, two healers, a control and a tank.
        Role[] pool = [A, A, A, A, A, A, A, A, S, H, H, C, T];
        int[] picks = Select(A, pool, 7);
        Assert.That(picks, Has.Length.EqualTo(7));
        Assert.That(picks.Select(index => pool[index]), Is.SupersetOf(new[] { S, H, H, C, T }));
    }

    [Test]
    public void OneBotCoversTwoPlaces()
    {
        // A Bard is speed and control, a Shaman heals and roots: the party needs fewer specialists.
        Role[] pool = [A, A, A, S | C, H | C, H, T, A];
        int[] picks = Select(A, pool, 7);
        Assert.That(picks.Select(index => pool[index]), Is.SupersetOf(new[] { S | C, H | C, H, T }));
        Assert.That(picks, Has.Length.EqualTo(7));
    }

    [Test]
    public void APaladinIsATankNotAHealer()
    {
        Role paladin = RolesOf(eCharacterClass.Paladin);
        Assert.That(paladin.HasFlag(Role.Tank), Is.True);
        Assert.That(paladin.HasFlag(Role.Healer), Is.False);
        Assert.That(RolesOf(eCharacterClass.Cleric).HasFlag(Role.Healer), Is.True);
        Assert.That(RolesOf(eCharacterClass.Shaman).HasFlag(Role.Healer | Role.Control), Is.True);
        Assert.That(RolesOf(eCharacterClass.Bard).HasFlag(Role.Speed | Role.Control), Is.True);
    }

    [Test]
    public void AFriarIsAnAttackerAndAlbionControlIsTheSorcererAndTheTheurgist()
    {
        Assert.That(RolesOf(eCharacterClass.Friar), Is.EqualTo(Role.Attacker));
        Assert.That(RolesOf(eCharacterClass.Bonedancer), Is.EqualTo(Role.Attacker));
        Assert.That(RolesOf(eCharacterClass.Sorcerer).HasFlag(Role.Control), Is.True);
        Assert.That(RolesOf(eCharacterClass.Theurgist).HasFlag(Role.Control), Is.True);
        Assert.That(RolesOf(eCharacterClass.Wizard).HasFlag(Role.Control), Is.False);
        Assert.That(RolesOf(eCharacterClass.Cabalist).HasFlag(Role.Control), Is.False);
        Assert.That(RolesOf(eCharacterClass.Minstrel), Is.EqualTo(Role.Speed));
    }

    [Test]
    public void AlbionTanksWithThePaladinAlone()
    {
        Assert.That(RolesOf(eCharacterClass.Paladin), Is.EqualTo(Role.Tank));
        Assert.That(RolesOf(eCharacterClass.Reaver), Is.EqualTo(Role.Attacker));
        Assert.That(RolesOf(eCharacterClass.Armsman), Is.EqualTo(Role.Attacker));
        Assert.That(RolesOf(eCharacterClass.Warrior), Is.EqualTo(Role.Tank));
        Assert.That(RolesOf(eCharacterClass.Hero), Is.EqualTo(Role.Tank));
    }

    [Test]
    public void AMissingRoleStillFormsTheParty()
    {
        Role[] pool = [A, A, A, A, A, A, T];
        int[] picks = Select(A, pool, 7);
        Assert.That(picks, Has.Length.EqualTo(6).Or.Length.EqualTo(7));
        Assert.That(picks, Is.Unique);
        Assert.That(picks.Select(index => pool[index]), Does.Contain(T));
    }

    [Test]
    public void AThreeTakesTwoHealersBeforeTheSpeed()
    {
        Role[] pool = [A, H, H, S];
        Assert.That(Select(A, pool, 2), Is.EqualTo(new[] { 1, 2 }));
    }

    [Test]
    public void TheLeaderCountsTowardsThePlaces()
    {
        // The leader is the healer: only one more healer is asked for.
        Role[] pool = [A, A, H, S, C, T, A, A];
        int[] picks = Select(H, pool, 7);
        Assert.That(picks.Select(index => pool[index]).Count(role => role == H), Is.EqualTo(1));
        Assert.That(picks.Select(index => pool[index]), Is.SupersetOf(new[] { H, S, C, T }));
    }

    [Test]
    public void AnEmptyPoolOrZeroCountPicksNobody()
    {
        Assert.That(Select(A, [], 7), Is.Empty);
        Assert.That(Select(A, [A, H], 0), Is.Empty);
    }
}
