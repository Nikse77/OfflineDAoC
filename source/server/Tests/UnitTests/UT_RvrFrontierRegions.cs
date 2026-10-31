using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>Owner 2026-10-31: RvR bots look for enemies in the frontier maps, not in the old frontier dungeons.</summary>
    [TestFixture]
    public class UT_RvrFrontierRegions
    {
        [TestCase((ushort)246, false)]   // Dodens Gruva
        [TestCase((ushort)248, false)]   // Summoner's Hall
        [TestCase((ushort)276, false)]   // Marfach Caverns
        [TestCase((ushort)277, false)]   // Hall of the Corrupt
        [TestCase((ushort)1, true)]      // Albion (old frontier zones)
        [TestCase((ushort)100, true)]    // Midgard
        [TestCase((ushort)200, true)]    // Hibernia
        [TestCase((ushort)163, true)]    // New Frontiers
        [TestCase((ushort)249, true)]    // Darkness Falls was never a frontier dungeon: unchanged here
        public void TheFourFrontierDungeonsAreNotRvrFrontier(ushort region, bool expected) =>
            Assert.That(AutonomousDungeonPolicy.IsRvrFrontierRegion(region), Is.EqualTo(expected));
    }
}
