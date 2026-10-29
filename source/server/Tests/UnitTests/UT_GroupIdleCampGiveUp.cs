using NUnit.Framework;

namespace DOL.GS.Tests;

[TestFixture]
public sealed class UT_GroupIdleCampGiveUp
{
    [TestCase("Grinding", 899L, ExpectedResult = false, TestName = "Fifteen minutes not yet")]
    [TestCase("Grinding", 900L, ExpectedResult = true, TestName = "Fifteen minutes without shared progress: another camp")]
    [TestCase("Grinding", 3_502L, ExpectedResult = true, TestName = "Albion 052 stood 58 minutes")]
    [TestCase("Traveling", 3_000L, ExpectedResult = false, TestName = "Only the grinding phase")]
    [TestCase("Regrouping", 3_000L, ExpectedResult = false, TestName = "A regroup has its own deadlines")]
    [TestCase("Waiting for resurrection", 3_000L, ExpectedResult = false, TestName = "A resurrection wait has its own")]
    [TestCase("Grinding", -1L, ExpectedResult = false, TestName = "Not tracked (-1)")]
    public bool APartyWithoutSharedProgressGivesItsCampUp(string phase, long idleSeconds) =>
        AutonomousBotGroupCoordinator.ShouldGiveUpIdleCamp(phase, idleSeconds);
}
