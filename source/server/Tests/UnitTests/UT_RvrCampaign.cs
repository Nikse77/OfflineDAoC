using System;
using System.Linq;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>Owner 2026-10-31, step 4 of the RvR plan: retake first, then conquest of the nearest keep of one of the other two frontiers.</summary>
    [TestFixture]
    public class UT_RvrCampaign
    {
        private const long Minute = 60_000;

        private static AutonomousRvrEventLayer.LiveObjective Keep(string id, ushort region, int x, int y, eRealm owner = eRealm.Midgard) =>
            new(id, id, AutonomousRvrEventLayer.Intent.AssaultKeep, owner, region, x, y, 0, false, 0, 0, 0, 0);

        [TestCase(null, 0L, true)]
        [TestCase(0L, 39 * Minute, false)]
        [TestCase(0L, 40 * Minute - 1, false)]
        [TestCase(0L, 40 * Minute, true)]
        [TestCase(1_000L, 41 * Minute, true)]
        public void TheConquestGapIsFortyMinutesFromTheEndOfTheAttempt(long? last, long now, bool expected) =>
            Assert.That(AutonomousRvrCampaign.ConquestGapElapsed(last, now), Is.EqualTo(expected));

        [Test]
        public void TheHomeFrontiersAreAlbion1Midgard100Hibernia200()
        {
            Assert.That(AutonomousRvrCampaign.HomeRegion(eRealm.Albion), Is.EqualTo((ushort)1));
            Assert.That(AutonomousRvrCampaign.HomeRegion(eRealm.Midgard), Is.EqualTo((ushort)100));
            Assert.That(AutonomousRvrCampaign.HomeRegion(eRealm.Hibernia), Is.EqualTo((ushort)200));
        }

        [Test]
        public void AKeepOfTheOwnFrontierHeldByAnotherRealmIsRetakenFirst()
        {
            var assaultable = new[] { Keep("home-keep", 1, 10, 10, eRealm.Hibernia), Keep("far-keep", 100, 10, 10) };

            var chosen = AutonomousRvrCampaign.SelectAssault(eRealm.Albion, assaultable, homeLost: true, null, 0, _ => null, new Random(1), out bool conquest);

            Assert.That(chosen.Id, Is.EqualTo("home-keep"));
            Assert.That(conquest, Is.False);
        }

        [Test]
        public void WhileAKeepOfTheOwnFrontierIsLostThereIsNoConquest()
        {
            // the lost keep is already under siege or on cooldown, so it is not assaultable now; the other frontiers must wait
            var assaultable = new[] { Keep("far-keep", 100, 10, 10) };

            var chosen = AutonomousRvrCampaign.SelectAssault(eRealm.Albion, assaultable, homeLost: true, null, 0, _ => null, new Random(1), out bool conquest);

            Assert.That(chosen, Is.Null);
            Assert.That(conquest, Is.False);
        }

        [Test]
        public void TheConquestWaitsForTheGapAndThenTakesTheKeepNearestToTheEntryKeep()
        {
            var assaultable = new[] { Keep("far", 100, 900, 900), Keep("near", 100, 110, 100), Keep("middle", 100, 400, 400) };
            Func<ushort, (int X, int Y)?> entry = region => region == 100 ? (100, 100) : null;

            var tooSoon = AutonomousRvrCampaign.SelectAssault(eRealm.Albion, assaultable, false, 0, 39 * Minute, entry, new Random(1), out bool early);
            var later = AutonomousRvrCampaign.SelectAssault(eRealm.Albion, assaultable, false, 0, 40 * Minute, entry, new Random(1), out bool conquest);

            Assert.That(tooSoon, Is.Null);
            Assert.That(early, Is.False);
            Assert.That(later.Id, Is.EqualTo("near"));
            Assert.That(conquest, Is.True);
        }

        [Test]
        public void TheConquestPicksOneOfTheOtherTwoFrontiersAtRandomAndNeverTheOwn()
        {
            var assaultable = new[] { Keep("midgard-keep", 100, 10, 10), Keep("hibernia-keep", 200, 10, 10) };
            var regions = Enumerable.Range(0, 60).Select(seed =>
                AutonomousRvrCampaign.SelectAssault(eRealm.Albion, assaultable, false, null, 0, _ => null, new Random(seed), out _).RegionId).ToArray();

            Assert.That(regions.Distinct(), Is.EquivalentTo(new ushort[] { 100, 200 }), "both frontiers are chosen over many tries");
            Assert.That(regions, Has.None.EqualTo((ushort)1));
        }

        [Test]
        public void NothingToAssaultGivesNothing()
        {
            Assert.That(AutonomousRvrCampaign.SelectAssault(eRealm.Albion, Array.Empty<AutonomousRvrEventLayer.LiveObjective>(), false, null, 0,
                _ => null, new Random(1), out bool conquest), Is.Null);
            Assert.That(conquest, Is.False);
        }

        [Test]
        public void TheRealmRemembersItsLastConquestAttempt()
        {
            Assert.That(AutonomousRvrCampaign.LastConquestTick(eRealm.Midgard), Is.Null.Or.TypeOf<long>());
            AutonomousRvrCampaign.RecordConquest(eRealm.Midgard, 12_345);
            Assert.That(AutonomousRvrCampaign.LastConquestTick(eRealm.Midgard), Is.EqualTo(12_345L));
        }
    }
}
