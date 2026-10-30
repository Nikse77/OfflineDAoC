using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>The expansion block: disabled_expansions is in client numbers, the Regions table counts from 0.</summary>
    [TestFixture]
    public class UT_ExpansionBlock
    {
        [Test]
        public void TheExpansionBlockUsesClientNumbers() => Assert.Multiple(() =>
        {
            const string blocked = "3;4;5;6";
            Assert.That(Region.IsExpansionDisabled(0, blocked), Is.False, "Classic");
            Assert.That(Region.IsExpansionDisabled(1, blocked), Is.False, "Shrouded Isles");
            Assert.That(Region.IsExpansionDisabled(2, blocked), Is.True, "Trials of Atlantis");
            Assert.That(Region.IsExpansionDisabled(3, blocked), Is.True, "Catacombs");
            Assert.That(Region.IsExpansionDisabled(4, blocked), Is.True, "Darkness Rising");
            Assert.That(Region.IsExpansionDisabled(2, "3-6"), Is.True, "a range");
            Assert.That(Region.IsExpansionDisabled(2, ""), Is.False);
            Assert.That(Region.IsExpansionDisabled(2, null), Is.False);
        });
    }
}
