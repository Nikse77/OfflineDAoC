using NUnit.Framework;

namespace DOL.GS.Tests;

[TestFixture]
public sealed class UT_TrainingDummyExcluded
{
    [TestCase("training dummy", ExpectedResult = true)]
    [TestCase("Training Dummy", ExpectedResult = true)]
    [TestCase("fighting dummy", ExpectedResult = true)]
    [TestCase("dummy golem", ExpectedResult = false, TestName = "A golem is a monster")]
    [TestCase("wind mephit", ExpectedResult = false)]
    [TestCase("", ExpectedResult = false)]
    [TestCase(null, ExpectedResult = false)]
    public bool PracticeDummiesAreNotCampTargets(string name) => AutonomousAuditedCampPolicy.IsTrainingDummyName(name);
}
