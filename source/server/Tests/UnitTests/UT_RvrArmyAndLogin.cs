using DOL.GS;
using NUnit.Framework;
using System.Linq;

namespace DOL.UnitTests
{
    /// <summary>Owner 2026-11-01: no army under two groups, and the level-50 RvR bots log in at their frontier keep.</summary>
    [TestFixture]
    public class UT_RvrArmyAndLogin
    {
        [TestCase(8, false, 16)]
        [TestCase(16, false, 16)]
        [TestCase(20, false, 17)]
        [TestCase(200, false, 108)]
        [TestCase(300, true, 170)]
        public void AnArmyNeverMarchesUnderSixteen(int assigned, bool relic, int expected) =>
            Assert.That(AutonomousRvrEventLayer.MarchThreshold(relic, assigned), Is.EqualTo(expected));

        [TestCase(50, true)]
        [TestCase(49, false)]
        [TestCase(1, false)]
        public void EveryLevelFiftyBotLogsInAtTheKeep(int level, bool expected) =>
            Assert.That(AutonomousRvrLogin.Applies(level), Is.EqualTo(expected));

        [TestCase(eRealm.Albion, (ushort)1)]
        [TestCase(eRealm.Midgard, (ushort)100)]
        [TestCase(eRealm.Hibernia, (ushort)200)]
        public void TheLoginPointIsTheRealmsHomeFrontierKeep(eRealm realm, ushort region)
        {
            Assert.That(AutonomousRvrLogin.TryGetPoint(realm, 12345, out ushort found, out System.Numerics.Vector3 point), Is.True);
            Assert.That(found, Is.EqualTo(region));
            Assert.That(point.X, Is.GreaterThan(0));
        }

        [Test]
        public void APartyWaitsOnThePassagePadOfItsHomeKeep()
        {
            // Master Visur's pad at Castle Sauvage: 940 units from the keep centre the staging rings are drawn around, so
            // the pad is the first anchor tried there (owner 2026-11-01: the level-50 bots wait where the player stands).
            var castleSauvage = new AutonomousRvrStaging.BorderKeep(1,
                new System.Numerics.Vector3(585085, 477504, 2600), "Castle Sauvage");
            var pad = new System.Numerics.Vector3(585995, 477187, 2623);
            Assert.That(AutonomousRvrStaging.IsOnKeepPad(castleSauvage, pad), Is.True);
            Assert.That(AutonomousRvrStaging.CandidateAnchors(castleSauvage, 12345, pad).First(), Is.EqualTo(pad));

            // The realm's other border keep is 60,000 units away: this pad is not its own and does not become its anchor.
            var snowdonia = new AutonomousRvrStaging.BorderKeep(1,
                new System.Numerics.Vector3(528451, 358290, 8320), "Snowdonia Fortress");
            Assert.That(AutonomousRvrStaging.IsOnKeepPad(snowdonia, pad), Is.False);
            Assert.That(AutonomousRvrStaging.CandidateAnchors(snowdonia, 12345, pad).First(), Is.Not.EqualTo(pad));
        }
    }
}
