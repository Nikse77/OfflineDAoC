using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    [TestFixture]
    public class UT_AutonomousLoginRange
    {
        [TestCase(1, 1, 0, true)]
        [TestCase(50, 1, 0, true)]
        [TestCase(49, 50, 0, false)]
        [TestCase(50, 50, 0, true)]
        [TestCase(49, 0, 0, true)]
        [TestCase(30, 20, 40, true)]
        [TestCase(41, 20, 40, false)]
        [TestCase(19, 20, 40, false)]
        [TestCase(1, -5, 0, true)]
        public void OnlyTheConfiguredLevelRangeMayLogIn(int level, int minimum, int maximum, bool expected) =>
            Assert.That(AutonomousPopulationController.MayLogin(level, minimum, maximum), Is.EqualTo(expected));
    }
}
