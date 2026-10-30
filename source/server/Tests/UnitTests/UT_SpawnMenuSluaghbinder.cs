using System.Linq;
using DOL.GS;
using DOL.GS.Commands;
using DOL.GS.ServerProperties;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>The /spawn class menu offers the Sluaghbinder only while the edition has the class on.</summary>
    [TestFixture]
    public class UT_SpawnMenuSluaghbinder
    {
        private bool _saved;

        [SetUp]
        public void Save() => _saved = Properties.ENABLE_SLUAGHBINDER;

        [TearDown]
        public void Restore() => Properties.ENABLE_SLUAGHBINDER = _saved;

        [Test]
        public void TheMenuDoesNotOfferItWhenTheClassIsOff()
        {
            Properties.ENABLE_SLUAGHBINDER = false;

            var classes = TemporaryGroupClassCatalog.ForRealm(eRealm.Hibernia).Select(entry => entry.CharacterClass);

            Assert.That(classes, Does.Not.Contain(eCharacterClass.Sluaghbinder));
            Assert.That(TemporaryGroupClassCatalog.TryResolve(eRealm.Hibernia, "Sluaghbinder", out _), Is.False);
        }

        [Test]
        public void TheMenuOffersItWhenTheClassIsOn()
        {
            Properties.ENABLE_SLUAGHBINDER = true;

            var classes = TemporaryGroupClassCatalog.ForRealm(eRealm.Hibernia).Select(entry => entry.CharacterClass);

            Assert.That(classes, Does.Contain(eCharacterClass.Sluaghbinder));
        }

        [Test]
        public void OtherRealmsNeverGetIt()
        {
            Properties.ENABLE_SLUAGHBINDER = true;

            Assert.That(TemporaryGroupClassCatalog.ForRealm(eRealm.Albion).Select(entry => entry.CharacterClass), Does.Not.Contain(eCharacterClass.Sluaghbinder));
            Assert.That(TemporaryGroupClassCatalog.ForRealm(eRealm.Midgard).Select(entry => entry.CharacterClass), Does.Not.Contain(eCharacterClass.Sluaghbinder));
        }
    }
}
