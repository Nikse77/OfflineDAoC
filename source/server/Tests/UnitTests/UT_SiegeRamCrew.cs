using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>
    /// Owner 2026-11-01: the people at a ram are capped by its size, and the player's companions count as crew.
    /// The counting itself needs a loaded world (it looks at the NPCs within 300 units), so only the cap is pinned here.
    /// </summary>
    [TestFixture]
    public class UT_SiegeRamCrew
    {
        [Test]
        public void TheRamsSizeSetsHowManyCanCrewIt()
        {
            Assert.That(GameSiegeRam.MaxPassengersFor(0), Is.EqualTo(2));   // mini
            Assert.That(GameSiegeRam.MaxPassengersFor(1), Is.EqualTo(6));   // light
            Assert.That(GameSiegeRam.MaxPassengersFor(2), Is.EqualTo(8));   // medium
            Assert.That(GameSiegeRam.MaxPassengersFor(3), Is.EqualTo(12));  // heavy
        }

        [Test]
        public void TheCrewCannotExceedTheRamsPlaces()
        {
            Assert.That(GameSiegeRam.CrewTotal(0, 5), Is.EqualTo(2), "a solo player with four companions fills a mini ram");
            Assert.That(GameSiegeRam.CrewTotal(1, 5), Is.EqualTo(5), "a light ram takes them all");
            Assert.That(GameSiegeRam.CrewTotal(3, 20), Is.EqualTo(12), "a heavy ram stops at twelve");
        }

        [Test]
        public void NoCrewMeansNoCrew()
        {
            Assert.That(GameSiegeRam.CrewTotal(2, 0), Is.EqualTo(0));
            Assert.That(GameSiegeRam.CrewTotal(2, -3), Is.EqualTo(0));
        }
    }
}
