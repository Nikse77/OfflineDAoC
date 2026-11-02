using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>
    /// Owner 2026-11-02: a ram always fights with a full crew. The virtual count - riders plus friendly bots within 300
    /// units - never filled (a night of sieges ran at 0-3 of 12 places), so the count was removed. These are the numbers
    /// a deployed ram now produces.
    /// </summary>
    [TestFixture]
    public class UT_SiegeRamFullCrew
    {
        [Test]
        public void AHeavyRamAtFullCrewHitsForItsIntendedDamage()
        {
            Assert.That(GameSiegeRam.DamageFor(3, GameSiegeRam.MaxPassengersFor(3)), Is.EqualTo(1400));
            Assert.That(GameSiegeRam.ReloadDelay(3, GameSiegeRam.MaxPassengersFor(3)), Is.EqualTo(8000));
        }

        [Test]
        public void AMediumRamAtFullCrewHitsForItsIntendedDamage()
        {
            Assert.That(GameSiegeRam.DamageFor(2, 8), Is.EqualTo(750));
            Assert.That(GameSiegeRam.ReloadDelay(2, 8), Is.EqualTo(6000));
        }

        [Test]
        public void TheSoloCrewNumbersStayAsTheyWere()
        {
            Assert.That(GameSiegeRam.DamageFor(2, 1), Is.EqualTo(225));
            Assert.That(GameSiegeRam.ReloadDelay(2, 1), Is.EqualTo(14750));
        }
    }
}
