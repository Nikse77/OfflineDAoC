using DOL.AI.Brain;
using DOL.GS;
using DOL.GS.PlayerClass;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>Owner 2026-10-31: spell casters do not cast at keep doors; they hit them with the staff.</summary>
    [TestFixture]
    public class UT_CasterKeepDoor
    {
        [TestCase(true, true, true)]     // a caster at a door: staff
        [TestCase(true, false, false)]   // a caster at anything else: spells as ever
        [TestCase(false, true, false)]   // a melee bot or archer at a door: unchanged
        [TestCase(false, false, false)]
        public void OnlyASpellCasterAtADoorUsesTheStaff(bool caster, bool door, bool expected) =>
            Assert.That(BotBrain.CasterHitsDoorWithStaff(caster, door), Is.EqualTo(expected));

        [Test]
        public void TheBrainCountsTheMagesAsSpellCastersAndNotTheFighters()
        {
            // PrefersSpellRange is what the brain asks of the class before it applies the rule.
            Assert.That(BotBrain.PrefersSpellRange(new ClassWizard()), Is.True);
            Assert.That(BotBrain.PrefersSpellRange(new ClassTheurgist()), Is.True);
            Assert.That(BotBrain.PrefersSpellRange(new ClassRunemaster()), Is.True);
            Assert.That(BotBrain.PrefersSpellRange(new ClassArmsman()), Is.False);
            Assert.That(BotBrain.PrefersSpellRange(new ClassHunter()), Is.False);
        }
    }
}
