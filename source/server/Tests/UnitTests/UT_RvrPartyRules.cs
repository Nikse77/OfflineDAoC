using NUnit.Framework;

namespace DOL.GS.Tests;

[TestFixture]
public sealed class UT_RvrPartyRules
{
    // Owner 2026-11-02: "se il capo muore il nuovo capo deve essere nella zona dove il capo muore, non al keep ad
    // aspettare il recupero o da qualche altra parte".
    [Test]
    public void OnlyALivingComradeInTheDeathZoneMaySucceed()
    {
        const ushort emain = 200;
        Assert.That(AutonomousBotGroupCoordinator.MaySucceedInZone(emain, emain, true, false), Is.True);
        Assert.That(AutonomousBotGroupCoordinator.MaySucceedInZone(emain, emain, true, true), Is.False, "parked at the keep");
        Assert.That(AutonomousBotGroupCoordinator.MaySucceedInZone(emain, emain, false, false), Is.False, "dead");
        Assert.That(AutonomousBotGroupCoordinator.MaySucceedInZone(emain, 100, true, false), Is.False, "another frontier");
        Assert.That(AutonomousBotGroupCoordinator.MaySucceedInZone(0, 100, true, false), Is.True, "unknown death region");
    }

    // Owner 2026-11-02: "ok ma deve essere espulso dal gruppo".
    [Test]
    public void AMemberThatCannotReachTheRendezvousIsExpelledAfterTwoMinutes()
    {
        long grace = AutonomousBotGroupCoordinator.UnreachableRouteGraceMilliseconds;
        Assert.That(AutonomousBotGroupCoordinator.ExpelsUnreachableMember(eAutonomousObjectiveKind.RvR, false, grace - 1), Is.False);
        Assert.That(AutonomousBotGroupCoordinator.ExpelsUnreachableMember(eAutonomousObjectiveKind.RvR, false, grace), Is.True);
        Assert.That(AutonomousBotGroupCoordinator.ExpelsUnreachableMember(eAutonomousObjectiveKind.RvR, true, grace * 10), Is.False,
            "the leader is the anchor and is never expelled");
        Assert.That(AutonomousBotGroupCoordinator.ExpelsUnreachableMember(eAutonomousObjectiveKind.GroupPve, false, grace * 10), Is.False,
            "PvE keeps its own partial-roster path");
    }

    // Owner 2026-11-02, test aid: "solo la prima assegnazione di incarico dopo l'avvio del server manda tutte le
    // pattuglie ad emain".
    [Test]
    public void OnlyTheFirstPatrolAfterABootIsPinnedToEmain()
    {
        int party = 7_117_331;
        Assert.That(AutonomousRvrPatrolRegion.TakeFirstAssignmentAfterBoot(party, out ushort region), Is.True);
        Assert.That(region, Is.EqualTo(AutonomousRvrPatrolRegion.EmainRegion));
        Assert.That(AutonomousRvrPatrolRegion.TakeFirstAssignmentAfterBoot(party, out _), Is.False, "second assignment is free");
        Assert.That(AutonomousRvrPatrolRegion.TakeFirstAssignmentAfterBoot(party + 1, out region), Is.True, "another party");
        Assert.That(region, Is.EqualTo(200));
    }
}
