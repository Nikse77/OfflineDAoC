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

    // Owner 2026-11-02: "La scelta la fa il capo. Solo lui calcola la destinazione. e i componenti del gruppo la
    // ereditano." Nobody receives a destination before the leader's chain (candidates, event plan, choice, frontier)
    // is finished.
    [Test]
    public void OnlyTheLivingLeaderComputesThePartyDestination()
    {
        Assert.That(AutonomousWorldBotController.MayComputeRvrDestination(false, false), Is.True, "a bot on its own");
        Assert.That(AutonomousWorldBotController.MayComputeRvrDestination(true, true), Is.True, "the living leader");
        Assert.That(AutonomousWorldBotController.MayComputeRvrDestination(true, false), Is.False, "a member inherits");
    }
}
