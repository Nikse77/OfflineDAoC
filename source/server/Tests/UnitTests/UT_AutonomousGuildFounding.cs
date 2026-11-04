using System.Collections.Generic;
using System.Linq;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    [TestFixture]
    public class UT_AutonomousGuildFounding
    {
        private static AutonomousGuildFounding.Candidate Bot(eRealm realm = eRealm.Albion, int level = 50, bool inAGuild = false, bool autonomous = true) =>
            new(autonomous, realm, level, inAGuild);

        private static AutonomousGuildFounding.Candidate[] Party(int size, eRealm realm = eRealm.Albion, int level = 50) =>
            Enumerable.Range(0, size).Select(_ => Bot(realm, level)).ToArray();

        [Test]
        public void OnlyAFullPartyOfEightJoinsAGuild()
        {
            Assert.That(AutonomousGuildFounding.PartyQualifies(Party(8)), Is.True);
            Assert.That(AutonomousGuildFounding.PartyQualifies(Party(7)), Is.False, "a smaller party waits");
            Assert.That(AutonomousGuildFounding.PartyQualifies(Party(9)), Is.False);
            Assert.That(AutonomousGuildFounding.PartyQualifies(null), Is.False);
        }

        [Test]
        public void EveryMemberMustBeLevelTwentyOfOneRealmWithNoGuild()
        {
            Assert.That(AutonomousGuildFounding.PartyQualifies(Party(8, level: 20)), Is.True);
            Assert.That(AutonomousGuildFounding.PartyQualifies(Party(8, level: 19)), Is.False);

            AutonomousGuildFounding.Candidate[] oneLow = [.. Party(7), Bot(level: 19)];
            Assert.That(AutonomousGuildFounding.PartyQualifies(oneLow), Is.False);

            AutonomousGuildFounding.Candidate[] mixedRealms = [.. Party(7), Bot(eRealm.Midgard)];
            Assert.That(AutonomousGuildFounding.PartyQualifies(mixedRealms), Is.False);

            AutonomousGuildFounding.Candidate[] oneInAGuild = [.. Party(7), Bot(inAGuild: true)];
            Assert.That(AutonomousGuildFounding.PartyQualifies(oneInAGuild), Is.False, "a bot invited into a player's guild keeps it");

            AutonomousGuildFounding.Candidate[] oneCompanion = [.. Party(7), Bot(autonomous: false)];
            Assert.That(AutonomousGuildFounding.PartyQualifies(oneCompanion), Is.False, "companions and helpers never join");
        }

        [Test]
        public void AGuildWithEightFreePlacesAndTheFewestMembersIsChosen()
        {
            (string, int)[] guilds = [("b", 40), ("a", 16), ("c", 8)];
            Assert.That(AutonomousGuildFounding.ChooseGuild(guilds), Is.EqualTo("c"));
        }

        [Test]
        public void AFullOrNearlyFullGuildIsSkipped()
        {
            // 64 is the cap: 57 members leave seven places, fewer than a party of eight.
            Assert.That(AutonomousGuildFounding.ChooseGuild([("a", 57), ("b", 64)]), Is.Null);
            Assert.That(AutonomousGuildFounding.ChooseGuild([("a", 56)]), Is.EqualTo("a"), "exactly eight places is enough");
            Assert.That(AutonomousGuildFounding.ChooseGuild([]), Is.Null);
            Assert.That(AutonomousGuildFounding.ChooseGuild(null), Is.Null);
        }

        [Test]
        public void ATieGoesToTheFirstGuildById()
        {
            Assert.That(AutonomousGuildFounding.ChooseGuild([("z", 16), ("m", 16), ("q", 16)]), Is.EqualTo("m"));
        }

        [Test]
        public void TheCapIsEightPartiesOfEight()
        {
            Assert.That(AutonomousGuildFounding.Capacity, Is.EqualTo(64));
            Assert.That(AutonomousGuildFounding.Capacity, Is.EqualTo(8 * AutonomousGuildFounding.PartySize));
        }
    }
}
