using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>Owner 2026-10-30: battleground bots see 4,000 units in a 120 degree arc ahead and 800 in the rest of the circle.</summary>
    [TestFixture]
    public class UT_BattlegroundHuntSight
    {
        [TestCase(3900f, 0f, true)]      // straight ahead, far
        [TestCase(3900f, 59f, true)]     // inside the arc on the clockwise side
        [TestCase(3900f, 301f, true)]    // inside the arc on the other side
        [TestCase(3900f, 61f, false)]    // just outside the arc
        [TestCase(3900f, 299f, false)]
        [TestCase(3900f, 180f, false)]   // straight behind, far
        [TestCase(4100f, 0f, false)]     // ahead but beyond 4,000
        [TestCase(790f, 180f, true)]     // behind but close
        [TestCase(790f, 90f, true)]      // beside but close
        [TestCase(810f, 90f, false)]     // beside, just beyond 800
        [TestCase(810f, 270f, false)]
        [TestCase(0f, 180f, true)]
        public void SeesFarAheadAndNearEverywhere(float distance, float angle, bool expected) =>
            Assert.That(AutonomousWorldBotController.BattlegroundSees(distance, angle), Is.EqualTo(expected));
    }
}
