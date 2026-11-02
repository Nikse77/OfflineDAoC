using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>
    /// Owner 2026-11-02: an emergency relocation never happens during a fight. The pocket step and the floor correction
    /// already refused to move a bot in combat; the ladder that ends in a teleport did not, so a bot could vanish from
    /// the melee and reappear at a muster. A held bot keeps fighting and the route is retried when the fight ends.
    /// </summary>
    [TestFixture]
    public class UT_RouteRelocationHold
    {
        [Test]
        public void AQuietBotMayBeRelocated()
        {
            Assert.That(AutonomousRouteRecoveryPolicy.MayRelocate(alive: true, inCombat: false, attacking: false,
                casting: false, hasAggro: false), Is.True);
        }

        [Test]
        public void ABotInCombatIsHeld()
        {
            Assert.That(AutonomousRouteRecoveryPolicy.MayRelocate(alive: true, inCombat: true, attacking: false,
                casting: false, hasAggro: false), Is.False);
        }

        [Test]
        public void AnAggressiveOrCastingBotIsHeld()
        {
            Assert.That(AutonomousRouteRecoveryPolicy.MayRelocate(alive: true, inCombat: false, attacking: true,
                casting: false, hasAggro: false), Is.False);
            Assert.That(AutonomousRouteRecoveryPolicy.MayRelocate(alive: true, inCombat: false, attacking: false,
                casting: true, hasAggro: false), Is.False);
            Assert.That(AutonomousRouteRecoveryPolicy.MayRelocate(alive: true, inCombat: false, attacking: false,
                casting: false, hasAggro: true), Is.False);
        }

        [Test]
        public void ADeadBotIsNotRelocated()
        {
            Assert.That(AutonomousRouteRecoveryPolicy.MayRelocate(alive: false, inCombat: false, attacking: false,
                casting: false, hasAggro: false), Is.False);
        }
    }
}
