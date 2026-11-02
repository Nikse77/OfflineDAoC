using System.Numerics;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>
    /// Owner 2026-11-02: a broken route must never send an RvR bot to its capital - the capital is where its group is
    /// not, so the bot walks back empty while the army starves at the muster. It goes to its army's muster, else to its
    /// realm's border keep, and only to a point that is a real hop away from where it stands.
    /// </summary>
    [TestFixture]
    public class UT_RvrRecoveryPoint
    {
        private static readonly AutonomousRvrRecovery.RecoveryPoint Muster =
            new(1, new Vector3(605_743f, 293_676f, 4_839f), "the portal outpost");
        private static readonly AutonomousRvrRecovery.RecoveryPoint BorderKeep =
            new(200, new Vector3(333_229f, 419_539f, 5_336f), "Druim Ligen");

        [Test]
        public void TheMusterIsChosenBeforeTheBorderKeep()
        {
            bool picked = AutonomousRvrRecovery.TryPick(rvrDuty: true, new Vector3(0f, 0f, 0f),
                new[] { Muster, BorderKeep }, out var chosen);

            Assert.That(picked, Is.True);
            Assert.That(chosen.Name, Is.EqualTo(Muster.Name));
        }

        [Test]
        public void TheBorderKeepIsUsedWhenTheMusterIsUnderfoot()
        {
            bool picked = AutonomousRvrRecovery.TryPick(rvrDuty: true, Muster.Position,
                new[] { Muster, BorderKeep }, out var chosen);

            Assert.That(picked, Is.True);
            Assert.That(chosen.Name, Is.EqualTo(BorderKeep.Name));
        }

        [Test]
        public void APointUnderTheBotNeverCountsAsRepair()
        {
            bool picked = AutonomousRvrRecovery.TryPick(rvrDuty: true, Muster.Position,
                new[] { Muster }, out _);

            Assert.That(picked, Is.False);
        }

        [Test]
        public void ANonRvrBotIsNotRelocatedByThisRule()
        {
            Assert.That(AutonomousRvrRecovery.TryPick(rvrDuty: false, new Vector3(0f, 0f, 0f),
                new[] { Muster, BorderKeep }, out _), Is.False);
        }
    }
}
