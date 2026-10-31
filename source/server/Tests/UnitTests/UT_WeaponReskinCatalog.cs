using System.Linq;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>Owner 2026-10-31: the looks a weapon can take at the weapon reskin master.</summary>
    [TestFixture]
    public class UT_WeaponReskinCatalog
    {
        private static WeaponReskinCatalog.Candidate C(int model, string name, int level) => new(model, name, level);

        [Test]
        public void OneLookPerModelNamedAfterItsLowestLevelItem()
        {
            var looks = WeaponReskinCatalog.Looks(new[]
            {
                C(100, "Duskwood Bow", 51), C(100, "Rowan Bow", 5), C(100, "Dyrwood Bow", 50),
                C(200, "Heartwood Longbow", 50),
            }, currentModel: 0);

            Assert.That(looks.Select(look => (look.Model, look.Name)), Is.EqualTo(new[] { (100, "Dyrwood Bow"), (200, "Heartwood Longbow") }));
        }

        [Test]
        public void OnlyItemsOfLevelFiftyOrAboveGiveLooks()
        {
            var looks = WeaponReskinCatalog.Looks(new[]
            {
                C(1, "Rowan Bow", 5), C(2, "Elm Bow", 49), C(3, "Heartwood Bow", 50), C(4, "Dyrwood Bow", 51),
                C(5, "Mixed", 10), C(5, "Mixed Better", 50),
            }, currentModel: 0);

            Assert.That(looks.Select(look => look.Model), Is.EquivalentTo(new[] { 3, 4, 5 }),
                "a model also used by an item of level 50 stays, named after that item");
            Assert.That(looks.Single(look => look.Model == 5).Name, Is.EqualTo("Mixed Better"));
        }

        [Test]
        public void TheWeaponsOwnModelAndEmptyModelsAreLeftOut()
        {
            var looks = WeaponReskinCatalog.Looks(new[] { C(100, "A", 50), C(200, "B", 50), C(0, "No model", 50), C(300, "  ", 50) }, currentModel: 100);

            Assert.That(looks.Select(look => look.Model), Is.EqualTo(new[] { 200 }));
        }

        [Test]
        public void LooksAreOrderedByName()
        {
            var looks = WeaponReskinCatalog.Looks(new[] { C(3, "zeta", 50), C(1, "Alpha", 50), C(2, "beta", 50) }, 0);

            Assert.That(looks.Select(look => look.Name), Is.EqualTo(new[] { "Alpha", "beta", "zeta" }));
        }

        [TestCase(0, 1)]
        [TestCase(1, 1)]
        [TestCase(86, 1)]    // the longest list in the item table fits on one page
        [TestCase(200, 1)]
        [TestCase(201, 2)]
        [TestCase(400, 2)]
        [TestCase(401, 3)]
        public void PageCountRoundsUp(int looks, int expected) => Assert.That(WeaponReskinCatalog.PageCount(looks), Is.EqualTo(expected));

        [Test]
        public void PagesAreNumberedFromOneAndClamped()
        {
            int size = WeaponReskinCatalog.PageSize;
            var looks = Enumerable.Range(1, size + 3).Select(i => new WeaponReskinCatalog.Look(i, "Look " + i, 1)).ToList();

            var second = WeaponReskinCatalog.Page(looks, 1).ToList();
            Assert.That(second.Select(entry => entry.Number), Is.EqualTo(new[] { size + 1, size + 2, size + 3 }));
            Assert.That(WeaponReskinCatalog.Page(looks, 99).First().Number, Is.EqualTo(size + 1), "past the end shows the last page");
            Assert.That(WeaponReskinCatalog.Page(looks, -3).First().Number, Is.EqualTo(1), "before the start shows the first page");
            Assert.That(WeaponReskinCatalog.Page(new System.Collections.Generic.List<WeaponReskinCatalog.Look>(), 0), Is.Empty);
        }

        [Test]
        public void ShieldsAreKeptBySizeWhateverTheirHandField()
        {
            int shield = (int)eObjectType.Shield;
            Assert.That(WeaponReskinCatalog.KindOf(shield, 11, 0, 3), Is.EqualTo(WeaponReskinCatalog.KindOf(shield, 11, 2, 3)));
            Assert.That(WeaponReskinCatalog.KindOf(shield, 11, 0, 1), Is.Not.EqualTo(WeaponReskinCatalog.KindOf(shield, 11, 0, 3)));
        }

        [Test]
        public void WeaponsAreKeptByHandAndNotBySize()
        {
            int bow = (int)eObjectType.Longbow;
            Assert.That(WeaponReskinCatalog.KindOf(bow, 13, 1, 3), Is.EqualTo(WeaponReskinCatalog.KindOf(bow, 13, 1, 1)));
            Assert.That(WeaponReskinCatalog.KindOf(bow, 13, 1, 3), Is.Not.EqualTo(WeaponReskinCatalog.KindOf(bow, 13, 0, 3)));
        }

        [TestCase(eObjectType.Longbow, true)]
        [TestCase(eObjectType.Crossbow, true)]
        [TestCase(eObjectType.Staff, true)]
        [TestCase(eObjectType.Shield, true)]
        [TestCase(eObjectType.Cloth, false)]
        [TestCase(eObjectType.Plate, false)]
        public void OnlyWeaponsAndShields(eObjectType type, bool expected) =>
            Assert.That(WeaponReskinCatalog.IsReskinnable((int)type), Is.EqualTo(expected));
    }
}
