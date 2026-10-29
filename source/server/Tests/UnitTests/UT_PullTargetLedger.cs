using NUnit.Framework;

namespace DOL.GS.Tests;

[TestFixture]
public sealed class UT_PullTargetLedger
{
    [Test]
    public void ATargetIsPulledAgainOnceBeforeItIsSetAside()
    {
        var ledger = new PullTargetLedger();
        Assert.That(ledger.Note(7, struck: false, now: 1_000), Is.False);
        Assert.That(ledger.IsBlocked(7, 1_001), Is.False, "the first failed attempt: shoot again");
        Assert.That(ledger.Note(7, struck: false, now: 50_000), Is.False);
        Assert.That(ledger.IsBlocked(7, 50_001), Is.True, "the second failure: another target");
    }

    [Test]
    public void AStruckTargetThatDidNotComeIsSetAsideAtOnce()
    {
        var ledger = new PullTargetLedger();
        ledger.Note(7, struck: true, now: 1_000);
        Assert.That(ledger.IsBlocked(7, 1_001), Is.True);
    }

    [Test]
    public void ASetAsideTargetIsPulledAgainAfterFiveMinutes()
    {
        var ledger = new PullTargetLedger();
        ledger.Note(7, struck: true, now: 1_000);
        Assert.That(ledger.IsBlocked(7, 1_000 + AutonomousDefensivePull.BlockedTargetMilliseconds - 1), Is.True);
        Assert.That(ledger.IsBlocked(7, 1_000 + AutonomousDefensivePull.BlockedTargetMilliseconds), Is.False);
    }

    [Test]
    public void ThreeTargetsSetAsideInARowAskForTheOrdinaryPull()
    {
        var ledger = new PullTargetLedger();
        Assert.That(ledger.Note(1, struck: true, now: 1_000), Is.False);
        Assert.That(ledger.Note(2, struck: true, now: 2_000), Is.False);
        Assert.That(ledger.Note(3, struck: true, now: 3_000), Is.True, "the third target in a row: melee for a while");
        Assert.That(ledger.BlockedInARow, Is.EqualTo(0), "and the count starts again");
    }

    [Test]
    public void AMonsterThatCameResetsTheCount()
    {
        var ledger = new PullTargetLedger();
        ledger.Note(1, struck: true, now: 1_000);
        ledger.Note(2, struck: true, now: 2_000);
        ledger.BlockedInARow = 0; // the party's engagement (Finish with engage) clears it
        Assert.That(ledger.Note(3, struck: true, now: 3_000), Is.False);
    }

    [Test]
    public void OtherTargetsAreNotAffected()
    {
        var ledger = new PullTargetLedger();
        ledger.Note(1, struck: true, now: 1_000);
        Assert.That(ledger.IsBlocked(2, 1_001), Is.False);
    }
}
