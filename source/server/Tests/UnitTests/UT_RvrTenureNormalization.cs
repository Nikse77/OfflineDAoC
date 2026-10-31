using System;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>Owner 2026-10-31: an RvR tenure lasts 120 to 180 minutes; the periodic normalization must not cut it back to 120.</summary>
    [TestFixture]
    public class UT_RvrTenureNormalization
    {
        private static OfflineWorldBotRecord Record(eAutonomousObjectiveKind kind, int minutes, DateTime assigned) => new()
        {
            ObjectiveKind = kind.ToString(),
            ObjectiveAssignmentId = "test-assignment",
            ObjectiveAssignedUtc = assigned.ToString("O"),
            ObjectiveExpiresUtc = assigned.AddMinutes(minutes).ToString("O"),
            ObjectivePveMode = "Time",
        };

        [Test]
        public void AnRvrTenureOfOneHundredSeventyMinutesIsKept()
        {
            DateTime assigned = new(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);
            OfflineWorldBotRecord record = Record(eAutonomousObjectiveKind.RvR, 170, assigned);

            Assert.That(AutonomousObjectiveAssignments.NormalizeLegacyTimedAssignment(record, assigned), Is.False);
            Assert.That(DateTime.Parse(record.ObjectiveExpiresUtc, null, System.Globalization.DateTimeStyles.RoundtripKind),
                Is.EqualTo(assigned.AddMinutes(170)));
        }

        [Test]
        public void AnRvrTenureOverTheMaximumIsCutToIt()
        {
            DateTime assigned = new(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);
            OfflineWorldBotRecord record = Record(eAutonomousObjectiveKind.RvR, 400, assigned);

            Assert.That(AutonomousObjectiveAssignments.NormalizeLegacyTimedAssignment(record, assigned), Is.True);
            Assert.That(DateTime.Parse(record.ObjectiveExpiresUtc, null, System.Globalization.DateTimeStyles.RoundtripKind).ToUniversalTime(),
                Is.EqualTo(assigned.AddMinutes(AutonomousObjectiveAssignments.MaximumRvrTenure.TotalMinutes)));
        }

        [Test]
        public void APveAssignmentKeepsItsOneHundredTwentyMinuteCeiling()
        {
            DateTime assigned = new(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);
            OfflineWorldBotRecord record = Record(eAutonomousObjectiveKind.SoloPve, 170, assigned);

            Assert.That(AutonomousObjectiveAssignments.NormalizeLegacyTimedAssignment(record, assigned), Is.True);
            Assert.That(DateTime.Parse(record.ObjectiveExpiresUtc, null, System.Globalization.DateTimeStyles.RoundtripKind).ToUniversalTime(),
                Is.EqualTo(assigned.AddMinutes(120)));
        }
    }
}
