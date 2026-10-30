using System;
using System.Linq;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>The handle census reads this process's own handles (Windows only).</summary>
    [TestFixture]
    public class UT_HandleCensus
    {
        [Test]
        public void ReadsTheKindsOfTheProcessHandles()
        {
            if (!OperatingSystem.IsWindows())
                Assert.Ignore("Windows only");

            var kinds = HandleCensus.Take();

            Assert.That(kinds, Is.Not.Null);
            Assert.That(kinds.Values.Sum(), Is.GreaterThan(20), "a running test host holds dozens of handles");
            Assert.That(kinds.Keys, Has.Some.AnyOf("Event", "File", "Thread", "Key", "Section"));
        }

        [Test]
        public void DescribesTheLargestKindsFirst()
        {
            if (!OperatingSystem.IsWindows())
                Assert.Ignore("Windows only");

            string text = HandleCensus.Describe(3);

            Assert.That(text, Is.Not.EqualTo("unavailable"));
            Assert.That(text.Split(' ').Length, Is.LessThanOrEqualTo(3));
            Assert.That(text, Does.Match(@"^\w+=\d+( \w+=\d+)*$"));
        }
    }
}
