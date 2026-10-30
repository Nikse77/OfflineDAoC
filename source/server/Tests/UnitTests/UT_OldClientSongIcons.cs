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
        public void SongsGetTheIconsRowThatDrawsThem(int icon, string type, int expected) =>
            Assert.That(OldClientSongIcons.For((ushort)icon, type), Is.EqualTo((ushort)expected));

        [TestCase(3608, "DirectDamage")]
        [TestCase(1111, "SpeedEnhancement")]
        [TestCase(1131, "DirectDamage")]
        [TestCase(2000, "HealthRegenBuff")]
        public void OtherSpellsKeepTheirIcon(int icon, string type) =>
            Assert.That(OldClientSongIcons.For((ushort)icon, type), Is.EqualTo((ushort)icon));
    }
}
