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
                C(100, "Heartwood Bow", 25), C(100, "Rowan Bow", 5), C(100, "Elm Bow", 10),
                C(200, "Runewood Longbow", 30),
            }, currentModel: 0);

            Assert.That(looks.Select(look => (look.Model, look.Name)), Is.EqualTo(new[] { (100, "Rowan Bow"), (200, "Runewood Longbow") }));
        }

        [Test]
        public void TheWeaponsOwnModelAndEmptyModelsAreLeftOut()
        {
            var looks = WeaponReskinCatalog.Looks(new[] { C(100, "A", 1), C(200, "B", 1), C(0, "No model", 1), C(300, "  ", 1) }, currentModel: 100);

            Assert.That(looks.Select(look => look.Model), Is.EqualTo(new[] { 200 }));
        }

        [Test]
        public void LooksAreOrderedByName()
        {
            var looks = WeaponReskinCatalog.Looks(new[] { C(3, "zeta", 1), C(1, "Alpha", 1), C(2, "beta", 1) }, 0);

            Assert.That(looks.Select(look => look.Name), Is.EqualTo(new[] { "Alpha", "beta", "zeta" }));
        }

        [TestCase(0, 1)]
        [TestCase(1, 1)]
        [TestCase(8, 1)]
        [TestCase(9, 2)]
        [TestCase(24, 3)]
        [TestCase(25, 4)]
        public void PageCountRoundsUp(int looks, int expected) => Assert.That(WeaponReskinCatalog.PageCount(looks), Is.EqualTo(expected));

        [Test]
        public void PagesAreNumberedFromOneAndClamped()
        {
            var looks = Enumerable.Range(1, 11).Select(i => new WeaponReskinCatalog.Look(i, "Look " + i, 1)).ToList();

            var second = WeaponReskinCatalog.Page(looks, 1).ToList();
            Assert.That(second.Select(entry => entry.Number), Is.EqualTo(new[] { 9, 10, 11 }));
            Assert.That(WeaponReskinCatalog.Page(looks, 99).First().Number, Is.EqualTo(9), "past the end shows the last page");
            Assert.That(WeaponReskinCatalog.Page(looks, -3).First().Number, Is.EqualTo(1), "before the start shows the first page");
            Assert.That(WeaponReskinCatalog.Page(new System.Collections.Generic.List<WeaponReskinCatalog.Look>(), 0), Is.Empty);
        }

        [TestCase(eObjectType.Longbow, true)]
        [TestCase(eObjectType.Crossbow, true)]
        [TestCase(eObjectType.Staff, true)]
        [TestCase(eObjectType.Shield, false)]
        [TestCase(eObjectType.Cloth, false)]
        [TestCase(eObjectType.Plate, false)]
        public void OnlyWeaponsButShields(eObjectType type, bool expected) =>
            Assert.That(WeaponReskinCatalog.IsReskinnable((int)type), Is.EqualTo(expected));
    }
}
