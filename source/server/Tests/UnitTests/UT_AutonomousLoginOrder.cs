using System.Collections.Generic;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>Owner 2026-10-31: at server start the bots enter the world from the highest level down, the level-50 ones first.</summary>
    [TestFixture]
    public class UT_AutonomousLoginOrder
    {
        private static long Next(List<AutonomousRealmLoginBalancer.Candidate> candidates, Dictionary<eRealm, int> load = null) =>
            AutonomousRealmLoginBalancer.SelectNext(candidates, load ?? new Dictionary<eRealm, int>()).Value;

        [Test]
        public void TheHighestLevelEntersBeforeAnyLower()
        {
            var candidates = new List<AutonomousRealmLoginBalancer.Candidate>
            {
                new(1, eRealm.Albion, 12), new(2, eRealm.Albion, 50), new(3, eRealm.Midgard, 30), new(4, eRealm.Hibernia, 50),
            };

            long first = Next(candidates);
            Assert.That(first, Is.AnyOf(2L, 4L));
        }

        [Test]
        public void EveryLevelFiftyEntersBeforeTheFirstLowerEvenOfAnotherRealm()
        {
            var candidates = new List<AutonomousRealmLoginBalancer.Candidate>
            {
                new(1, eRealm.Albion, 50), new(2, eRealm.Albion, 50), new(3, eRealm.Albion, 50),
                new(4, eRealm.Midgard, 49), new(5, eRealm.Hibernia, 49),
            };
            var load = new Dictionary<eRealm, int>();
            var order = new List<long>();
            while (candidates.Count > 0)
            {
                long id = Next(candidates, load);
                order.Add(id);
                var chosen = candidates.Find(candidate => candidate.BotId == id);
                candidates.Remove(chosen);
                load[chosen.Realm] = load.GetValueOrDefault(chosen.Realm) + 1;
            }

            Assert.That(order.GetRange(0, 3), Is.EquivalentTo(new long[] { 1, 2, 3 }), "the three level-50 bots of Albion come first");
            Assert.That(order.GetRange(3, 2), Is.EquivalentTo(new long[] { 4, 5 }));
        }

        [Test]
        public void WithinALevelTheRealmsStillAlternate()
        {
            var candidates = new List<AutonomousRealmLoginBalancer.Candidate>
            {
                new(1, eRealm.Albion, 50), new(2, eRealm.Albion, 50), new(3, eRealm.Midgard, 50), new(4, eRealm.Midgard, 50),
                new(5, eRealm.Hibernia, 50), new(6, eRealm.Hibernia, 50),
            };
            var load = new Dictionary<eRealm, int>();
            var realms = new List<eRealm>();
            for (int i = 0; i < 6; i++)
            {
                long id = Next(candidates, load);
                var chosen = candidates.Find(candidate => candidate.BotId == id);
                candidates.Remove(chosen);
                realms.Add(chosen.Realm);
                load[chosen.Realm] = load.GetValueOrDefault(chosen.Realm) + 1;
            }

            Assert.That(realms.GetRange(0, 3), Is.EquivalentTo(new[] { eRealm.Albion, eRealm.Midgard, eRealm.Hibernia }));
            Assert.That(realms.GetRange(3, 3), Is.EquivalentTo(new[] { eRealm.Albion, eRealm.Midgard, eRealm.Hibernia }));
        }

        [Test]
        public void TheLevelDefaultsToZeroSoOlderCallersKeepTheirOrder()
        {
            var candidates = new List<AutonomousRealmLoginBalancer.Candidate> { new(7, eRealm.Albion), new(3, eRealm.Albion) };

            Assert.That(Next(candidates), Is.EqualTo(3L), "by database id, as before");
        }
    }
}
