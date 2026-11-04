using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    [TestFixture]
    public class UT_AutonomousGuildGenerators
    {
        private static readonly eRealm[] Realms = [eRealm.Albion, eRealm.Midgard, eRealm.Hibernia];

        [Test]
        public void EveryGeneratedNameIsAValidGuildName()
        {
            var random = new Random(12345);
            foreach (eRealm realm in Realms)
                for (int i = 0; i < 500; i++)
                {
                    string name = AutonomousGuildNames.Generate(realm, _ => false, random);
                    Assert.That(AutonomousGuildNames.IsAcceptable(name), Is.True, name);
                    // The rule of /guild create: letters and spaces only.
                    Assert.That(Regex.IsMatch(name, @"^[a-zA-Z àâäèéêëîïôœùûüÿçÀÂÄÈÉÊËÎÏÔŒÙÛÜŸÇ]+$"), Is.True, name);
                    Assert.That(name.Length, Is.LessThanOrEqualTo(AutonomousGuildNames.MaxLength));
                }
        }

        [Test]
        public void GeneratedNamesAreNeverOnesTheCallerKnows()
        {
            var random = new Random(7);
            var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < 400; i++)
            {
                string name = AutonomousGuildNames.Generate(eRealm.Hibernia, taken.Contains, random);
                Assert.That(name, Is.Not.Null);
                Assert.That(taken.Add(name), Is.True, $"duplicate: {name}");
            }
        }

        [Test]
        public void ANameIsStillFoundWhenEveryPlainCombinationIsTaken()
        {
            // Everything without a numeral is refused: the generator falls back to a numeral.
            string name = AutonomousGuildNames.Generate(eRealm.Albion, candidate => !Regex.IsMatch(candidate, @" (II|III|IV|V|VI|VII|VIII|IX|X|XI|XII)$"), new Random(3));
            Assert.That(name, Is.Not.Null);
            Assert.That(AutonomousGuildNames.IsAcceptable(name), Is.True);
        }

        [TestCase("Order of the Silver Hawk", true)]
        [TestCase("Clan Ironfang", true)]
        [TestCase("Clan Iron-fang", false)]
        [TestCase("Order 7", false)]
        [TestCase("None", false)]
        [TestCase("", false)]
        [TestCase(" Leading space", false)]
        [TestCase("Two  spaces", false)]
        [TestCase("An Extremely Long Guild Name Indeed", false)]
        public void TheNameRulesMatchTheGuildCommand(string name, bool expected) =>
            Assert.That(AutonomousGuildNames.IsAcceptable(name), Is.EqualTo(expected));

        [Test]
        public void TheEmblemNumberFollowsTheClientsLayout()
        {
            // The player guild Sgaruja: logo 10, pattern 3, primary 1, secondary 0.
            Assert.That(AutonomousGuildEmblems.Decode(5512), Is.EqualTo((10, 3, 1, 0)));
            Assert.That(AutonomousGuildEmblems.Encode(10, 3, 1, 0), Is.EqualTo(5512));
            Assert.That(AutonomousGuildEmblems.Encode(128, 0, 0, 0), Is.EqualTo(0x10000));
        }

        [Test]
        public void EveryGeneratedEmblemIsValidForItsRealmAndNeverZero()
        {
            var random = new Random(99);
            foreach (eRealm realm in Realms)
                for (int i = 0; i < 500; i++)
                {
                    int emblem = AutonomousGuildEmblems.Generate(realm, _ => false, random);
                    Assert.That(emblem, Is.GreaterThan(0));
                    Assert.That(AutonomousGuildEmblems.IsValid(realm, emblem), Is.True, emblem.ToString());
                }
        }

        [Test]
        public void TheTwoColoursOfAGeneratedEmblemContrastEnough()
        {
            // Owner 2026-11-04: Tan on Dark Khaki and Light Blue on Purple-grey read as one flat colour.
            Assert.That(AutonomousGuildEmblems.Contrast(primary: 11, secondary: 1), Is.LessThan(AutonomousGuildEmblems.MinimumContrast));
            Assert.That(AutonomousGuildEmblems.Contrast(primary: 7, secondary: 7), Is.LessThan(AutonomousGuildEmblems.MinimumContrast));
            Assert.That(AutonomousGuildEmblems.Contrast(primary: 0, secondary: 3), Is.GreaterThanOrEqualTo(AutonomousGuildEmblems.MinimumContrast));
            var random = new Random(11);
            foreach (eRealm realm in Realms)
                for (int i = 0; i < 1000; i++)
                {
                    var (_, _, primary, secondary) = AutonomousGuildEmblems.Decode(AutonomousGuildEmblems.Generate(realm, _ => false, random));
                    Assert.That(AutonomousGuildEmblems.Contrast(primary, secondary), Is.GreaterThanOrEqualTo(AutonomousGuildEmblems.MinimumContrast));
                }
            // The emblem of a player guild with poor contrast is not "valid" for the generator, but still decodes.
            Assert.That(AutonomousGuildEmblems.IsValid(eRealm.Albion, AutonomousGuildEmblems.Encode(1, 0, 11, 1)), Is.False);
        }

        [Test]
        public void TheRealmLogoListsAreTheRealmsOwnAndLeaveOutTheGuardLogos()
        {
            foreach (eRealm realm in Realms)
            {
                IReadOnlyList<int> logos = AutonomousGuildEmblems.LogosOf(realm);
                Assert.That(logos.Distinct().Count(), Is.EqualTo(logos.Count));
                Assert.That(logos.Min(), Is.GreaterThanOrEqualTo(1));
                Assert.That(logos.Max(), Is.LessThanOrEqualTo(255));
                Assert.That(logos.Intersect([121, 122, 123]), Is.Empty);
            }
            Assert.That(AutonomousGuildEmblems.LogosOf(eRealm.Albion).Intersect(AutonomousGuildEmblems.LogosOf(eRealm.Midgard)), Is.Empty);
            Assert.That(AutonomousGuildEmblems.LogosOf(eRealm.Albion).Intersect(AutonomousGuildEmblems.LogosOf(eRealm.Hibernia)), Is.Empty);
            Assert.That(AutonomousGuildEmblems.LogosOf(eRealm.Midgard).Intersect(AutonomousGuildEmblems.LogosOf(eRealm.Hibernia)), Is.Empty);
        }

        [Test]
        public void GeneratedEmblemsAreUniqueEvenWhenNearlyAllAreTaken()
        {
            var random = new Random(5);
            var taken = new HashSet<int>();
            for (int i = 0; i < 600; i++)
                Assert.That(taken.Add(AutonomousGuildEmblems.Generate(eRealm.Midgard, taken.Contains, random)), Is.True);

            // Leave exactly one free: the walk over all combinations must find it.
            int free = AutonomousGuildEmblems.Encode(AutonomousGuildEmblems.LogosOf(eRealm.Midgard)[17], 2, 9, 4);
            int found = AutonomousGuildEmblems.Generate(eRealm.Midgard, emblem => emblem != free, random);
            Assert.That(found, Is.EqualTo(free));

            // Nothing free: 0.
            Assert.That(AutonomousGuildEmblems.Generate(eRealm.Midgard, _ => true, random), Is.EqualTo(0));
        }
    }
}
