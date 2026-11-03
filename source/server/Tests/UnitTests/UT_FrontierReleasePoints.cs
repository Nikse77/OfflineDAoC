using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>
    /// Owner 2026-11-02: in a frontier the release is always the realm's border keep with the portal - its own
    /// frontier's keep, or its portal keep in the enemy frontier it came in through. Each value below is the spot in front
    /// of that keep's gate: the frontier hastener's position plus five hundred units along the direction he faces (owner
    /// 2026-11-02, home keeps included; the fixture used the landing pads until the 2026-11-03 update).
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
        public void AlbionReleasesAtItsOwnKeepAtHomeAndInFrontOfTheGateInTheEnemyFrontiers()
        {
            AssertPoint(eRealm.Albion, 1, 586_460, 476_519);    // Castle Sauvage, home keep: in front of the gate
            AssertPoint(eRealm.Albion, 100, 597_274, 629_875);  // Albion portal keep in Odin's Gate: gate
            AssertPoint(eRealm.Albion, 200, 474_679, 342_496);  // Albion portal keep in Emain: gate
        }

        [Test]
        public void MidgardReleasesAtItsOwnKeepAtHomeAndInFrontOfTheGateInTheEnemyFrontiers()
        {
            AssertPoint(eRealm.Midgard, 100, 766_219, 667_345); // Svasud Faste, home keep: in front of the gate
            AssertPoint(eRealm.Midgard, 1, 654_013, 294_113);   // Midgard portal keep in Hadrian's Wall: gate
            AssertPoint(eRealm.Midgard, 200, 473_000, 296_290); // Midgard portal keep in Emain: gate
        }

        [Test]
        public void HiberniaReleasesAtItsOwnKeepAtHomeAndInFrontOfTheGateInTheEnemyFrontiers()
        {
            AssertPoint(eRealm.Hibernia, 200, 332_862, 418_660); // Druim Ligen, home keep: in front of the gate
            AssertPoint(eRealm.Hibernia, 1, 606_664, 295_125);   // Hibernia portal keep in Hadrian's Wall: gate
            AssertPoint(eRealm.Hibernia, 100, 596_891, 582_904); // Hibernia portal keep in Odin's Gate: gate
        }

        [Test]
        public void AllNineBorderKeepsKnowAGatePoint()
        {
            // Owner 2026-11-02: the home keeps got a gate front too, outside their walls (the six entry keeps had one already).
            Assert.That(AutonomousFrontierRelease.TryGateFront(eRealm.Albion, 1, out _), Is.True);
            Assert.That(AutonomousFrontierRelease.TryGateFront(eRealm.Midgard, 100, out _), Is.True);
            Assert.That(AutonomousFrontierRelease.TryGateFront(eRealm.Hibernia, 200, out _), Is.True);
            Assert.That(AutonomousFrontierRelease.TryGateFront(eRealm.Albion, 100, out _), Is.True);
            Assert.That(AutonomousFrontierRelease.TryGateFront(eRealm.Midgard, 200, out _), Is.True);
            Assert.That(AutonomousFrontierRelease.TryGateFront(eRealm.Hibernia, 1, out _), Is.True);
            Assert.That(AutonomousFrontierRelease.TryGateFront(eRealm.None, 1, out _), Is.False);
            Assert.That(AutonomousFrontierRelease.TryGateFront(eRealm.Albion, 10, out _), Is.False);
            Assert.That(AutonomousFrontierRelease.TryGateFront(eRealm.Albion, 200, out _), Is.True);
            Assert.That(AutonomousFrontierRelease.TryGateFront(eRealm.Midgard, 1, out _), Is.True);
            Assert.That(AutonomousFrontierRelease.TryGateFront(eRealm.Hibernia, 100, out _), Is.True);
        }

        [Test]
        public void ThePartySpreadsOutInFrontOfTheGateWithoutPilingUp()
        {
            // Owner 2026-11-02: "non si ammassino tutti nello stesso punto ... ma siano un po' sparpagliati lì fuori".
            var seen = new System.Collections.Generic.HashSet<(int, int)>();
            double farthest = 0;
            for (long botId = 1; botId <= 60; botId++)
            {
                System.Numerics.Vector3 offset = AutonomousFrontierRelease.GateOffset(eRealm.Albion, 200, botId);
                seen.Add(((int)offset.X, (int)offset.Y));
                farthest = System.Math.Max(farthest, offset.Length());
                // the gate of the Albion keep in Emain faces (-0.7045, -0.7097): nobody stands behind the walls
                double forward = offset.X * -0.7045 + offset.Y * -0.7097;
                Assert.That(forward, Is.GreaterThanOrEqualTo(-1f), $"bot {botId} would stand behind the gate");
            }
            Assert.That(seen.Count, Is.GreaterThan(50), "the offsets must not collapse on one point");
            Assert.That(farthest, Is.GreaterThan(400), "the stand has to reach out into the open");
            Assert.That(farthest, Is.LessThan(800), "and never wander past eight hundred units from the gate");
        }

        [Test]
        public void OnlyTheFrontierMapsCountAsAFrontierForARelease()
        {
            // The regions also hold the homelands: Cotswold is in region 1 and Mularn in 100.
            Assert.That(AutonomousFrontierRelease.IsFrontierZone(1, 15), Is.True);   // Hadrian's Wall
            Assert.That(AutonomousFrontierRelease.IsFrontierZone(1, 11), Is.True);   // Forest Sauvage
            Assert.That(AutonomousFrontierRelease.IsFrontierZone(1, 1), Is.False);   // Salisbury Plains
            Assert.That(AutonomousFrontierRelease.IsFrontierZone(1, 0), Is.False);   // Camelot Hills
            Assert.That(AutonomousFrontierRelease.IsFrontierZone(100, 115), Is.True); // Odin's Gate
            Assert.That(AutonomousFrontierRelease.IsFrontierZone(100, 100), Is.False); // Vale of Mularn
            Assert.That(AutonomousFrontierRelease.IsFrontierZone(200, 214), Is.True); // Emain Macha
            Assert.That(AutonomousFrontierRelease.IsFrontierZone(200, 200), Is.False); // Lough Derg
            Assert.That(AutonomousFrontierRelease.IsFrontierZone(10, 15), Is.False);  // Camelot itself
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
