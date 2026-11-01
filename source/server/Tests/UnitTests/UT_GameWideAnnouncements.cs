using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    [TestFixture, NonParallelizable]
    public class UT_GameWideAnnouncements
    {
        [TearDown]
        public void After()
        {
            GameWideAnnouncements.RvrBattleground = true;
            GameWideAnnouncements.PveRealmEvents = true;
        }

        [Test]
        public void EachCategoryHasItsOwnSwitch()
        {
            Assert.That(GameWideAnnouncements.Enabled(AnnouncementKind.RvrBattleground), Is.True);
            Assert.That(GameWideAnnouncements.Enabled(AnnouncementKind.PveRealmEvent), Is.True);

            GameWideAnnouncements.RvrBattleground = false;
            Assert.That(GameWideAnnouncements.Enabled(AnnouncementKind.RvrBattleground), Is.False);
            Assert.That(GameWideAnnouncements.Enabled(AnnouncementKind.PveRealmEvent), Is.True);

            GameWideAnnouncements.RvrBattleground = true;
            GameWideAnnouncements.PveRealmEvents = false;
            Assert.That(GameWideAnnouncements.Enabled(AnnouncementKind.RvrBattleground), Is.True);
            Assert.That(GameWideAnnouncements.Enabled(AnnouncementKind.PveRealmEvent), Is.False);
        }

        [Test]
        public void BattlegroundSiegeAnnouncesOncePerQuietSpell()
        {
            long quiet = AutonomousRvrEventLayer.BattlegroundSiegeQuietMilliseconds;
            Assert.That(AutonomousRvrEventLayer.BattlegroundSiegeIsNew(0, 10_000), Is.True);
            Assert.That(AutonomousRvrEventLayer.BattlegroundSiegeIsNew(10_000, 10_000 + quiet - 1), Is.False);
            Assert.That(AutonomousRvrEventLayer.BattlegroundSiegeIsNew(10_000, 10_000 + quiet), Is.True);
        }

        // The line this used to pin ("Albion's forces have laid siege to ... in ...") is gone: owner 2026-11-01 dropped the
        // announcement a battleground siege starting used to send to every realm. The quiet-spell pacing above still runs
        // for the BG_SIEGE_STARTED log entry.
    }
}
