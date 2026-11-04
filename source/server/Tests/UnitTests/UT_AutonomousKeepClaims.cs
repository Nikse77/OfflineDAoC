using System;
using System.Collections.Generic;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    [TestFixture]
    public class UT_AutonomousKeepClaims
    {
        [Test]
        public void AKeepIsNotClaimedBeforeThirtySecondsAfterTheLordFell()
        {
            Assert.That(AutonomousKeepClaims.IsDue(now: 29_999, takenAt: 0, nextTry: 0), Is.False);
            Assert.That(AutonomousKeepClaims.IsDue(now: 30_000, takenAt: 0, nextTry: 0), Is.True);
            Assert.That(AutonomousKeepClaims.IsDue(now: 130_000, takenAt: 100_000, nextTry: 0), Is.True);
        }

        [Test]
        public void ARetryWaitsForItsOwnClock()
        {
            Assert.That(AutonomousKeepClaims.IsDue(now: 40_000, takenAt: 0, nextTry: 45_000), Is.False);
            Assert.That(AutonomousKeepClaims.IsDue(now: 45_000, takenAt: 0, nextTry: 45_000), Is.True);
        }

        [Test]
        public void ANoOneClaimIsGivenUpAfterTwentyMinutes()
        {
            Assert.That(AutonomousKeepClaims.Expired(now: 1_200_000, takenAt: 0), Is.False);
            Assert.That(AutonomousKeepClaims.Expired(now: 1_200_001, takenAt: 0), Is.True);
        }

        [Test]
        public void ABotIsPresentWhenAliveInTheRegionWithinFourThousandFiveHundredUnits()
        {
            Assert.That(AutonomousKeepClaims.IsPresent(alive: true, sameRegion: true, distance: 4_500), Is.True);
            Assert.That(AutonomousKeepClaims.IsPresent(alive: true, sameRegion: true, distance: 4_501), Is.False);
            Assert.That(AutonomousKeepClaims.IsPresent(alive: false, sameRegion: true, distance: 100), Is.False, "a corpse is not present");
            Assert.That(AutonomousKeepClaims.IsPresent(alive: true, sameRegion: false, distance: 100), Is.False);
        }

        [Test]
        public void OneCandidateIsPickedAtRandomOrNoneWhenThereIsNone()
        {
            Assert.That(AutonomousKeepClaims.PickOne<string>([]), Is.Null);
            Assert.That(AutonomousKeepClaims.PickOne<string>(null), Is.Null);
            Assert.That(AutonomousKeepClaims.PickOne(["only"]), Is.EqualTo("only"));
            var seen = new HashSet<string>();
            var random = new Random(1);
            for (int i = 0; i < 200; i++)
                seen.Add(AutonomousKeepClaims.PickOne(["a", "b", "c"], random));
            Assert.That(seen, Is.EquivalentTo(new[] { "a", "b", "c" }), "every candidate can be picked");
        }
    }
}
