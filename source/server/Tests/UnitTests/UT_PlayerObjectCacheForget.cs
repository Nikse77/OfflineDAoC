using System.Runtime.CompilerServices;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>After a teleport only what was first sent since is sent again; a dead gamebot's body follows the same rule.</summary>
    [TestFixture]
    public class UT_PlayerObjectCacheForget
    {
        private static T Make<T>() where T : class => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));

        [Test]
        public void OnlyNpcsFirstSentSinceTheTeleportAreForgotten()
        {
            var cache = new PlayerObjectCache();
            GameNPC before = Make<GameNPC>(), after = Make<GameNPC>();
            cache.NpcUpdateCache[before] = new ClientService.CachedNpcValues(1_000, 100);
            cache.NpcUpdateCache[after] = new ClientService.CachedNpcValues(5_000, 100);
            cache.NpcInRangeCache.Add(before);
            cache.NpcInRangeCache.Add(after);

            cache.ForgetSentSince(2_000);

            Assert.That(cache.NpcUpdateCache.ContainsKey(before), Is.True, "already shown: never flickers");
            Assert.That(cache.NpcUpdateCache.ContainsKey(after), Is.False, "sent while loading: sent again");
            Assert.That(cache.NpcInRangeCache, Does.Contain(before).And.Not.Contain(after));
        }

        [Test]
        public void ANpcStaysForgottenOnlyByItsFirstSendNotItsLastUpdate()
        {
            var cache = new PlayerObjectCache();
            GameNPC npc = Make<GameNPC>();
            var values = new ClientService.CachedNpcValues(1_000, 100);
            values.LastUpdateTime = 9_000;
            cache.NpcUpdateCache[npc] = values;

            cache.ForgetSentSince(2_000);

            Assert.That(cache.NpcUpdateCache.ContainsKey(npc), Is.True);
        }

        [Test]
        public void ADeadBodyShownSinceTheTeleportIsShownAgain()
        {
            var cache = new PlayerObjectCache();
            GameNPC old = Make<GameNPC>(), recent = Make<GameNPC>();
            cache.DeadBodiesShown[old] = 1_000;
            cache.DeadBodiesShown[recent] = 5_000;

            cache.ForgetSentSince(2_000);

            Assert.That(cache.DeadBodiesShown.ContainsKey(old), Is.True, "the client kept its lying body");
            Assert.That(cache.DeadBodiesShown.ContainsKey(recent), Is.False, "it may have been dropped while loading");
        }

        [Test]
        public void ClearForgetsEveryDeadBody()
        {
            var cache = new PlayerObjectCache();
            cache.DeadBodiesShown[Make<GameNPC>()] = 1_000;
            cache.Clear();
            Assert.That(cache.DeadBodiesShown, Is.Empty);
        }
    }
}
