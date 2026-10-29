using DOL.GS.Keeps;
using NUnit.Framework;

namespace DOL.GS.Tests;

[TestFixture]
public sealed class UT_BattlegroundKeepLevels
{
    // Brackets: Abermenai 15-19, Thidranki 20-24, Murdaigean 25-29, Caledonia 30-35.
    [TestCase(19, false, 21)]
    [TestCase(19, true, 24)]
    [TestCase(24, false, 26)]
    [TestCase(24, true, 29)]
    [TestCase(29, false, 31)]
    [TestCase(29, true, 34)]
    [TestCase(35, false, 37)]
    [TestCase(35, true, 40)]
    public void GuardsAndLordsFollowTheBracket(int bracketMax, bool lord, int expected) =>
        Assert.That(BattlegroundKeepLevels.Level(bracketMax, lord, 2, 5), Is.EqualTo(expected));

    [Test]
    public void TheLevelStaysInRange()
    {
        Assert.That(BattlegroundKeepLevels.Level(3, false, -10, -10), Is.EqualTo(1));
        Assert.That(BattlegroundKeepLevels.Level(245, true, 20, 20), Is.EqualTo(250));
    }

    [TestCase(19, 33)]   // the old Abermenai lord, for bots of 15-19
    [TestCase(35, 57)]   // the old Caer Caledon lord, for bots of 30-35
    public void TheNewLordIsWellBelowTheOldOne(int bracketMax, int oldLord) =>
        Assert.That(BattlegroundKeepLevels.Level(bracketMax, true, 2, 5), Is.LessThan(oldLord));
}
