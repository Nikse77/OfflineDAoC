using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>
    /// Owner 2026-11-02: "un membro che fa release aspetta dove sta che il leader sia a tiro (2000 unità?) e lo
    /// raggiunge per rientrare in formazione", and the leader "lo va a prendere" until then.
    /// </summary>
    [TestFixture]
    public class UT_RvrPickup
    {
        [Test]
        public void ThePickupRadiusIsTheOwnersTwoThousand()
        {
            Assert.That(AutonomousRvrPickup.PickupRadius, Is.EqualTo(2_000));
        }

        [Test]
        public void AReleasedMemberJoinsOnlyInsideTheRadius()
        {
            Assert.That(AutonomousRvrPickup.MemberJoinsLeader(1_999), Is.True);
            Assert.That(AutonomousRvrPickup.MemberJoinsLeader(2_000), Is.True);
            Assert.That(AutonomousRvrPickup.MemberJoinsLeader(2_001), Is.False);
        }

        [Test]
        public void TheLeaderKeepsMarchingToTheKeepUntilItIsInsideTheRadius()
        {
            Assert.That(AutonomousRvrPickup.LeaderKeepsFetching(40_000), Is.True);
            Assert.That(AutonomousRvrPickup.LeaderKeepsFetching(2_001), Is.True);
            Assert.That(AutonomousRvrPickup.LeaderKeepsFetching(2_000), Is.False);
            Assert.That(AutonomousRvrPickup.LeaderKeepsFetching(0), Is.False);
        }

        [Test]
        public void AFighterBeyondTheRadiusDropsTheChase()
        {
            Assert.That(AutonomousRvrPickup.MemberReturnsToFormation(2_000), Is.False);
            Assert.That(AutonomousRvrPickup.MemberReturnsToFormation(2_001), Is.True);
            Assert.That(AutonomousRvrPickup.MemberReturnsToFormation(9_000), Is.True);
        }

        [Test]
        public void TheLeaderIsLeashedTheSameWayByItsNearestMember()
        {
            Assert.That(AutonomousRvrPickup.LeaderMustReturn(2_000), Is.False);
            Assert.That(AutonomousRvrPickup.LeaderMustReturn(2_001), Is.True);
            Assert.That(AutonomousRvrPickup.LeaderMustReturn(30_000), Is.True);
        }

        [Test]
        public void ALeaderThatReleasedAtTheKeepWaitsForTheWholeParty()
        {
            // "sono rellati al keep ma non hanno rigruppato, sono partiti uno alla volta".
            Assert.That(AutonomousRvrPickup.PartyMayLeaveKeep(2_000, 0), Is.True);
            Assert.That(AutonomousRvrPickup.PartyMayLeaveKeep(2_001, 0), Is.False);
            Assert.That(AutonomousRvrPickup.PartyMayLeaveKeep(20_000, 60_000), Is.False);
            Assert.That(AutonomousRvrPickup.PartyMayLeaveKeep(20_000, AutonomousRvrPickup.KeepGatherMilliseconds), Is.True);
        }
    }
}
