using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>Bots spread over a zone's camps instead of piling into one (AutonomousCampCompetition).</summary>
    [TestFixture]
    public class UT_CampCompetition
    {
        [Test]
        public void ACampIsCrowdedWhenItsBotsOutnumberHalfItsMonsters() => Assert.Multiple(() =>
        {
            Assert.That(AutonomousCampCompetition.Crowded(2, 1), Is.False, "two bots always fit");
            Assert.That(AutonomousCampCompetition.Crowded(5, 10), Is.False);
            Assert.That(AutonomousCampCompetition.Crowded(6, 10), Is.True);
            Assert.That(AutonomousCampCompetition.Pressure(6, 10), Is.LessThan(AutonomousCampCompetition.Pressure(6, 4)));
        });

        [Test]
        public void ASingleCampIsKeptWhateverItsCrowd() =>
            Assert.That(AutonomousCampCompetition.PreferQuiet(new[] { "only" }, id => id, _ => 1), Is.EqualTo(new[] { "only" }));
    }
}
