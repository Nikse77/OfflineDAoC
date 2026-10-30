using DOL.GS;
using DOL.GS.PlayerClass;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>Owner 2026-10-30: self dexterity buffs first; acuity buffs only for the casters who use acuity.</summary>
    [TestFixture]
    public class UT_BotBuffRules
    {
        private static Spell NewSpell(eSpellType type, eSpellTarget target) =>
            new(new DOL.Database.DbSpell { Type = type.ToString(), Target = target.ToString() }, 1);

        [TestCase(eSpellType.DexterityBuff, eSpellTarget.SELF, true)]
        [TestCase(eSpellType.DexterityQuicknessBuff, eSpellTarget.SELF, true)]
        [TestCase(eSpellType.DexterityBuff, eSpellTarget.GROUP, false)]
        [TestCase(eSpellType.DexterityBuff, eSpellTarget.REALM, false)]
        [TestCase(eSpellType.StrengthBuff, eSpellTarget.SELF, false)]
        public void OnlyTheSelfDexterityBuffsGoFirst(eSpellType type, eSpellTarget target, bool expected) =>
            Assert.That(BotBuffRules.IsSelfDexterityBuff(NewSpell(type, target)), Is.EqualTo(expected));

        [Test]
        public void NoSpellIsNotADexterityBuff() => Assert.That(BotBuffRules.IsSelfDexterityBuff(null), Is.False);

        [Test]
        public void AcuityBuffsAreWastedOnAnythingButAListCaster()
        {
            Assert.That(BotBuffRules.UsesAcuity((ICharacterClass)null), Is.False);
            Assert.That(BotBuffRules.UsesAcuity((GameLiving)null), Is.False);
            Assert.That(BotBuffRules.IsWastedOn(null, NewSpell(eSpellType.AcuityBuff, eSpellTarget.GROUP)), Is.True);
            // any other buff is never "wasted" by this rule
            Assert.That(BotBuffRules.IsWastedOn(null, NewSpell(eSpellType.StrengthBuff, eSpellTarget.GROUP)), Is.False);
        }
    }
}
