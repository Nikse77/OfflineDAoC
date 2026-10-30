using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>The disabled class and race lists hold ranges ("58-62", "16-21") that character creation never expanded.</summary>
    [TestFixture]
    public class UT_DisabledClassRaceRanges
    {
        [Test]
        public void DisabledClassAndRaceRangesAreExpanded() => Assert.Multiple(() =>
        {
            var classes = Util.SplitCSV("33;34;39;58-62", true);
            Assert.That(classes, Does.Contain("60").And.Contain("58").And.Contain("62").And.Contain("33"));
            Assert.That(classes, Does.Not.Contain("20"), "Necromancer (Disciple) stays creatable");
            Assert.That(Util.SplitCSV("16-21", true), Is.EquivalentTo(new[] { "16", "17", "18", "19", "20", "21" }));
        });
    }
}
