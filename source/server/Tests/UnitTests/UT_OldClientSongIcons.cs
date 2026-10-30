using DOL.Database;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    [TestFixture]
    public sealed class UT_OldClientSongIcons
    {
        [TestCase(3608, "SpeedEnhancement", 320)]
        [TestCase(3611, "SpeedEnhancement", 323)]
        [TestCase(1111, "HealthRegenBuff", 1370)]
        [TestCase(1116, "HealthRegenBuff", 1372)]
        [TestCase(3616, "HealthRegenBuff", 1372)]
        [TestCase(5173, "PowerRegenBuff", 1462)]
        [TestCase(5151, "SpeedEnhancement", 320)]  // Bard: Clear Path
        [TestCase(5155, "SpeedEnhancement", 324)]  // Bard: Clear Horizon
        [TestCase(3221, "SpeedEnhancement", 320)]
        [TestCase(931, "SpeedEnhancement", 320)]
        [TestCase(5161, "EnduranceRegenBuff", 330)]  // Bard: Rhythm of Hibernia
        [TestCase(5165, "EnduranceRegenBuff", 334)]  // Bard: Rhythm of the Cosmos
        [TestCase(1081, "EnduranceRegenBuff", 330)]
        [TestCase(4971, "HealthRegenBuff", 1370)]  // Regrowth Bard: Chant of Healing
        [TestCase(4975, "HealthRegenBuff", 1374)]
        public void SongsGetTheIconsRowThatDrawsThem(int icon, string type, int expected) =>
            Assert.That(OldClientSongIcons.For((ushort)icon, type), Is.EqualTo((ushort)expected));

        [TestCase(3608, "DirectDamage")]
        [TestCase(1111, "SpeedEnhancement")]
        [TestCase(1131, "DirectDamage")]
        [TestCase(2000, "HealthRegenBuff")]
        [TestCase(5161, "DirectDamage")]
        [TestCase(5151, "EnduranceRegenBuff")]
        [TestCase(5161, "SpeedEnhancement")]
        public void OtherSpellsKeepTheirIcon(int icon, string type) =>
            Assert.That(OldClientSongIcons.For((ushort)icon, type), Is.EqualTo((ushort)icon));

        [Test]
        public void TheEffectsBarKeepsTheDatabaseIconWhileTheSongBarGetsTheIconsRow()
        {
            var spell = new Spell(new DbSpell { Type = "SpeedEnhancement", Target = "Self", Icon = 3608 }, 20);
            Assert.That(spell.Icon, Is.EqualTo((ushort)320));
            Assert.That(spell.EffectIcon, Is.EqualTo((ushort)3608));
        }

        [Test]
        public void ACopyOfASpellKeepsItsEffectIcon()
        {
            var spell = new Spell(new DbSpell { Type = "SpeedEnhancement", Target = "Self", Icon = 3608 }, 20);
            var copy = new Spell(spell, eSpellType.SpeedEnhancement);
            Assert.That(copy.EffectIcon, Is.EqualTo((ushort)3608));
        }
    }
}
