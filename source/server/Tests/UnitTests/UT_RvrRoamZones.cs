using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>Owner 2026-10-31: patrols and hunts are held in the zones of the portal keeps; without keep data nothing is restricted.</summary>
    [TestFixture]
    public class UT_RvrRoamZones
    {
        [TestCase((ushort)1, 600000, 290000)]
        [TestCase((ushort)100, 600000, 600000)]
        [TestCase((ushort)200, 475000, 340000)]
        [TestCase((ushort)163, 1000, 1000)]
        public void WithNoKeepDataAnyPointIsAllowed(ushort region, int x, int y) =>
            Assert.That(AutonomousRvrRoamZones.IsRoamPoint(region, x, y), Is.True);
    }
}
