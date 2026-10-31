using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>Owner 2026-10-31, step 2 of the RvR plan: groups of eight, six or seven after five minutes, stealth classes alone.</summary>
    [TestFixture]
    public class UT_RvrFormation
    {
        private const long Minute = 60_000;

        [TestCase(8, 0L, 8)]
        [TestCase(12, 0L, 8)]
        [TestCase(7, 4 * Minute, 0)]            // seven, not yet five minutes: keep waiting
        [TestCase(7, 5 * Minute, 7)]
        [TestCase(6, 5 * Minute, 6)]
        [TestCase(6, 4 * Minute + 59_999, 0)]
        [TestCase(5, 60 * Minute, 0)]            // fewer than six never start, however long they wait
        [TestCase(1, 60 * Minute, 0)]
        [TestCase(0, 60 * Minute, 0)]
        public void TheGroupStartsWithEightOrSixToSevenAfterTheWait(int candidates, long waited, int expected) =>
            Assert.That(AutonomousRvrFormation.TargetSize(candidates, waited), Is.EqualTo(expected));

        [TestCase(eCharacterClass.Infiltrator, true)]
        [TestCase(eCharacterClass.Nightshade, true)]
        [TestCase(eCharacterClass.Shadowblade, true)]
        [TestCase(eCharacterClass.Scout, true)]
        [TestCase(eCharacterClass.Hunter, true)]
        [TestCase(eCharacterClass.Ranger, true)]
        [TestCase(eCharacterClass.Minstrel, false)]   // has the group speed song: the group comes first
        [TestCase(eCharacterClass.Cleric, false)]
        [TestCase(eCharacterClass.Armsman, false)]
        [TestCase(eCharacterClass.Wizard, false)]
        [TestCase(eCharacterClass.Bard, false)]
        public void OnlyTheStealthClassesWithoutTheGroupSpeedSongGoAlone(eCharacterClass characterClass, bool expected) =>
            Assert.That(AutonomousRvrFormation.IsSoloStealth(characterClass), Is.EqualTo(expected));
    }
}
