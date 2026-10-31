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

        private static System.Collections.Generic.List<WeaponReskinCatalog.Look> Named(int count, string prefix = "Look ") =>
            Enumerable.Range(1, count).Select(i => new WeaponReskinCatalog.Look(i, prefix + i, 50)).ToList();

        [Test]
        public void NoLooksStillGiveOneEmptyPage()
        {
            var pages = WeaponReskinCatalog.Pages(Named(0));

            Assert.That(pages, Has.Count.EqualTo(1));
            Assert.That(pages[0], Is.Empty);
        }

        [Test]
        public void ThePagesKeepEveryLookInOrderWithItsOwnNumber()
        {
            var looks = Named(120, "A fairly long name of a look ");

            var pages = WeaponReskinCatalog.Pages(looks);

            Assert.That(pages.Count, Is.GreaterThan(1));
            Assert.That(pages.SelectMany(page => page).Select(entry => entry.Number), Is.EqualTo(Enumerable.Range(1, 120)));
            Assert.That(pages.SelectMany(page => page).Select(entry => entry.Look), Is.EqualTo(looks));
        }

        [Test]
        public void NoPageIsLongerThanItsCharacterBudget()
        {
            var pages = WeaponReskinCatalog.Pages(Named(200, "Duskwood Long Bow of the Chasm "));

            foreach (var page in pages)
                Assert.That(page.Sum(entry => WeaponReskinCatalog.LineOf(entry.Number, entry.Look).Length),
                    Is.LessThanOrEqualTo(WeaponReskinCatalog.PageCharacters));
        }

        [Test]
        public void AFullPageStaysUnderTheServersPacketLimitWithItsHeaderAndButtons()
        {
            // The server discards a packet over 2,048 bytes (the 3,058-byte list of 2026-10-31). A page also carries a header
            // with the item's name and the buttons.
            string header = "Looks for your Duskwood exceptional long bow of the something (page 10 of 10). Tell me [look N] to see one:\n\n";
            string footer = "\n[previous page]  [next page]  [cancel]";
            foreach (var page in WeaponReskinCatalog.Pages(Named(300, "A rather long look name here ")))
                Assert.That(header.Length + footer.Length + 2 + page.Sum(e => WeaponReskinCatalog.LineOf(e.Number, e.Look).Length),
                    Is.LessThan(2_048));
        }

        [Test]
        public void ALookLongerThanTheBudgetStillGetsAPageOfItsOwn()
        {
            var pages = WeaponReskinCatalog.Pages(new[] { new WeaponReskinCatalog.Look(1, new string('x', 2_000), 50) });

            Assert.That(pages, Has.Count.EqualTo(1));
            Assert.That(pages[0], Has.Count.EqualTo(1));
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
