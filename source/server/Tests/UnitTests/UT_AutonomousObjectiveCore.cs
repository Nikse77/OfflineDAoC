using DOL.GS;
using NUnit.Framework;

namespace DOL.GS.Tests;

[TestFixture]
public sealed class UT_AutonomousObjectiveCore
{
    [TestCase(eCharacterClass.Armsman, true)]
    [TestCase(eCharacterClass.Warrior, true)]
    [TestCase(eCharacterClass.Hero, true)]
    [TestCase(eCharacterClass.Paladin, true)]
    [TestCase(eCharacterClass.Cleric, true)]
    [TestCase(eCharacterClass.Healer, true)]
    [TestCase(eCharacterClass.Druid, true)]
    [TestCase(eCharacterClass.Shaman, true)]
    [TestCase(eCharacterClass.Friar, true)]
    [TestCase(eCharacterClass.Warden, true)]
    [TestCase(eCharacterClass.Wizard, false)]
    [TestCase(eCharacterClass.Berserker, false)]
    [TestCase(eCharacterClass.Hunter, false)]
    [TestCase(eCharacterClass.Nightshade, false)]
    [TestCase(eCharacterClass.Minstrel, false)]
    public void ATankOrAHealerClassIsThePartysCore(eCharacterClass characterClass, bool core) =>
        Assert.That(AutonomousObjectiveAssignments.IsPartyCoreClass(characterClass), Is.EqualTo(core));

    [TestCase(0, 0)]
    [TestCase(5, 2)]
    [TestCase(10, 4)]
    [TestCase(35, 14)]
    [TestCase(40, 16)]
    public void TheGroupSlotsHoldAtMostTwoFifthsTanksAndHealers(int groupSlots, int limit) =>
        Assert.That(AutonomousObjectiveAssignments.CoreLimitInGroupSlots(groupSlots), Is.EqualTo(limit));

    [Test]
    public void ATankOrHealerWhoseQueueTimeRanOutStaysInTheQueueUnlessItHoldsTooManyOfThem()
    {
        // A queue of 10 holds at most 4 tanks and healers; the bot that timed out is one of them.
        Assert.That(AutonomousObjectiveAssignments.ShouldKeepCoreInQueue(1, 10), Is.True);
        Assert.That(AutonomousObjectiveAssignments.ShouldKeepCoreInQueue(4, 10), Is.True, "exactly at the cap");
        Assert.That(AutonomousObjectiveAssignments.ShouldKeepCoreInQueue(5, 10), Is.False, "one over: this one may go solo");
        Assert.That(AutonomousObjectiveAssignments.ShouldKeepCoreInQueue(3, 5), Is.False, "a queue of five holds two");
        Assert.That(AutonomousObjectiveAssignments.ShouldKeepCoreInQueue(2, 5), Is.True);
    }

    [Test]
    public void ACoreBotLeavesTheSoloSlotsWhileGroupSlotsAreLeftAndTheCapIsNotReached()
    {
        // 10 group slots, cap 4.
        Assert.That(AutonomousObjectiveAssignments.ShouldKeepCoreOutOfSolo(10, 0, 10), Is.True);
        Assert.That(AutonomousObjectiveAssignments.ShouldKeepCoreOutOfSolo(7, 3, 10), Is.True);
        Assert.That(AutonomousObjectiveAssignments.ShouldKeepCoreOutOfSolo(6, 4, 10), Is.False, "cap reached: the next ones may be solo");
        Assert.That(AutonomousObjectiveAssignments.ShouldKeepCoreOutOfSolo(0, 0, 10), Is.False, "no group slot left");
        Assert.That(AutonomousObjectiveAssignments.ShouldKeepCoreOutOfSolo(5, 0, 0), Is.False, "no group target in this bucket");
    }
}
