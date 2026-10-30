using System;
using System.Collections.Generic;
using System.Linq;
using DOL.GS.Quests.Hibernia;
using DOL.GS.ServerProperties;
using NUnit.Framework;

namespace DOL.GS.Tests
{
    /// <summary>The Sluaghbinder is discontinued (owner 2026-10-30): no setting brings it back to the generators.</summary>
    [TestFixture]
    [NonParallelizable]
    public class UT_SluaghbinderEditionSwitch
    {
        private bool _original;

        [SetUp]
        public void Remember() => _original = Properties.ENABLE_SLUAGHBINDER;

        [TearDown]
        public void Restore() => Properties.ENABLE_SLUAGHBINDER = _original;

        [TestCase(true)]
        [TestCase(false)]
        public void NeverRollsListsOrHelpsWithTheClass(bool setting)
        {
            Properties.ENABLE_SLUAGHBINDER = setting;
            Assert.That(Roll(new Random(4242)), Does.Not.Contain(eCharacterClass.Sluaghbinder));
            Assert.That(AutonomousBotIdentityGenerator.GetEraClasses(eRealm.Hibernia), Does.Not.Contain(eCharacterClass.Sluaghbinder));
            Assert.That(AutonomousBotIdentityGenerator.GetEraClasses(eRealm.Hibernia), Has.Count.EqualTo(13));
            Assert.That(AutonomousBotIdentityGenerator.IsClassAvailable(eCharacterClass.Sluaghbinder), Is.False);
            Assert.That(AutonomousBotIdentityGenerator.IsClassAvailable(eCharacterClass.Druid), Is.True);
            Assert.Throws<ArgumentException>(() =>
                AutonomousBotIdentityGenerator.GenerateForClass(eRealm.Hibernia, eGender.Male, eCharacterClass.Sluaghbinder));
        }

        [Test]
        public void TheQuestLineDoesNotStart() =>
            Assert.That(SluaghbinderEpicQuestRuntime.Discontinued, Is.True);

        private static List<eCharacterClass> Roll(Random random)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return Enumerable.Range(0, 700)
                .Select(index => AutonomousBotIdentityGenerator.Generate(eRealm.Hibernia,
                    index % 2 == 0 ? eGender.Male : eGender.Female, names, random).CharacterClass)
                .ToList();
        }
    }
}
