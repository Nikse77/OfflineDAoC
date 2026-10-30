using System.Linq;
using DOL.GS;
using DOL.GS.Commands;
using DOL.GS.ServerProperties;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>The /spawn class menu never offers the Sluaghbinder, which is discontinued.</summary>
    [TestFixture]
    public class UT_SpawnMenuSluaghbinder
    {
        private bool _saved;

        [SetUp]
        public void Save() => _saved = Properties.ENABLE_SLUAGHBINDER;

        [TearDown]
        public void Restore() => Properties.ENABLE_SLUAGHBINDER = _saved;

        [TestCase(true)]
        [TestCase(false)]
        public void TheMenuNeverOffersIt(bool setting)
        {
            Properties.ENABLE_SLUAGHBINDER = setting;

            foreach (eRealm realm in new[] { eRealm.Albion, eRealm.Midgard, eRealm.Hibernia })
                Assert.That(TemporaryGroupClassCatalog.ForRealm(realm).Select(entry => entry.CharacterClass), Does.Not.Contain(eCharacterClass.Sluaghbinder));
            Assert.That(TemporaryGroupClassCatalog.TryResolve(eRealm.Hibernia, "Sluaghbinder", out _), Is.False);
        }
    }
}
