using System.Threading;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>The named sections of a bot's turn that the long-turn warning lists as sectionsBy= (diagnostics only).</summary>
    [TestFixture]
    public class UT_NavQueryProfileSections
    {
        [SetUp]
        public void Fresh() => NavQueryProfile.Reset();

        [Test]
        public void ASlowSectionIsListedWithItsTime()
        {
            using (NavQueryProfile.Section("Exchange.TryList"))
                Thread.Sleep(60);

            Assert.That(NavQueryProfile.Summary(), Does.Match(@"sectionsBy=Exchange\.TryList:\d+ms"));
        }

        [Test]
        public void AFastSectionIsLeftOut()
        {
            using (NavQueryProfile.Section("Brain.Watchdog"))
            {
            }

            Assert.That(NavQueryProfile.Summary(), Does.Not.Contain("sectionsBy"));
        }

        [Test]
        public void SectionsOfTheSameNameAddUpAndTheSlowestComesFirst()
        {
            using (NavQueryProfile.Section("Brain.TravelBuffs")) Thread.Sleep(30);
            using (NavQueryProfile.Section("Brain.TravelBuffs")) Thread.Sleep(30);
            using (NavQueryProfile.Section("Brain.PetUpkeep")) Thread.Sleep(30);

            string summary = NavQueryProfile.Summary();

            Assert.That(summary.IndexOf("Brain.TravelBuffs"), Is.GreaterThan(-1));
            Assert.That(summary.IndexOf("Brain.TravelBuffs"), Is.LessThan(summary.IndexOf("Brain.PetUpkeep")), "60 ms before 30 ms");
        }

        [Test]
        public void ResetForgetsTheSections()
        {
            using (NavQueryProfile.Section("State.Aggro")) Thread.Sleep(40);
            NavQueryProfile.Reset();

            Assert.That(NavQueryProfile.Summary(), Does.Not.Contain("sectionsBy"));
        }
    }
}
