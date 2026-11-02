using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>
    /// Owner 2026-11-02: in a frontier the release is always the realm's border keep with the portal - its own
    /// frontier's keep, or its portal keep in the enemy frontier it came in through. Each value below is the landing pad
    /// of that realm's passage in that frontier, i.e. the keep whose porter carries its medallions.
    /// </summary>
    [TestFixture]
    public class UT_FrontierReleasePoints
    {
        private static void AssertPoint(eRealm realm, ushort region, int x, int y)
        {
            Assert.That(AutonomousFrontierRelease.TryPoint(realm, region, out ushort released, out Point3D point), Is.True,
                $"{realm} has no release point in {region}");
            Assert.That(released, Is.EqualTo(region));
            Assert.That(point.X, Is.EqualTo(x));
            Assert.That(point.Y, Is.EqualTo(y));
        }

        [Test]
        public void AlbionReleasesAtItsOwnKeepAtHomeAndAtItsPortalKeepInTheEnemyFrontiers()
        {
            AssertPoint(eRealm.Albion, 1, 584_285, 477_200);    // Castle Sauvage
            AssertPoint(eRealm.Albion, 100, 596_364, 631_509);  // Albion portal keep in Odin's Gate
            AssertPoint(eRealm.Albion, 200, 475_835, 343_661);  // Albion portal keep in Emain
        }

        [Test]
        public void MidgardReleasesAtItsOwnKeepAtHomeAndAtItsPortalKeepInTheEnemyFrontiers()
        {
            AssertPoint(eRealm.Midgard, 100, 766_811, 669_605); // Svasud Faste
            AssertPoint(eRealm.Midgard, 1, 655_200, 293_217);   // Midgard portal keep in Hadrian's Wall
            AssertPoint(eRealm.Midgard, 200, 474_107, 295_199); // Midgard portal keep in Emain
        }

        [Test]
        public void HiberniaReleasesAtItsOwnKeepAtHomeAndAtItsPortalKeepInTheEnemyFrontiers()
        {
            AssertPoint(eRealm.Hibernia, 200, 334_386, 420_071); // Druim Ligen
            AssertPoint(eRealm.Hibernia, 1, 605_743, 293_676);   // Hibernia portal keep in Hadrian's Wall
            AssertPoint(eRealm.Hibernia, 100, 596_055, 581_400); // Hibernia portal keep in Odin's Gate
        }

        [Test]
        public void OnlyTheThreeFrontiersAndTheThreeRealmsAreCovered()
        {
            Assert.That(AutonomousFrontierRelease.Applies(1), Is.True);
            Assert.That(AutonomousFrontierRelease.Applies(100), Is.True);
            Assert.That(AutonomousFrontierRelease.Applies(200), Is.True);
            Assert.That(AutonomousFrontierRelease.Applies(10), Is.False);
            Assert.That(AutonomousFrontierRelease.TryPoint(eRealm.Albion, 10, out _, out _), Is.False);
            Assert.That(AutonomousFrontierRelease.TryPoint(eRealm.None, 200, out _, out _), Is.False);
        }
    }
}
