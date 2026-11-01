using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>
    /// Owner 2026-11-01: /cpstop is a retreat of fifteen seconds, during which the companions follow and engage nothing;
    /// afterwards they behave as before. /cpassist and /cpbuff need the running world and are not covered here.
    /// </summary>
    [TestFixture]
    public class UT_CompanionOrders
    {
        [Test]
        public void TheRetreatLastsFifteenSeconds()
        {
            Assert.That(CompanionEngagementMode.RetreatMilliseconds, Is.EqualTo(15_000));
            const long now = 1_000_000;
            long until = now + CompanionEngagementMode.RetreatMilliseconds;
            Assert.That(CompanionEngagementMode.RetreatActive(until, now), Is.True, "from the order");
            Assert.That(CompanionEngagementMode.RetreatActive(until, until - 1), Is.True, "one millisecond before it runs out");
            Assert.That(CompanionEngagementMode.RetreatActive(until, until), Is.False, "when it runs out");
            Assert.That(CompanionEngagementMode.RetreatActive(until, until + 1), Is.False, "after it runs out");
        }

        [Test]
        public void WithoutAnOrderNoRetreatRuns() =>
            Assert.That(CompanionEngagementMode.RetreatActive(0, 5), Is.False);
    }
}
