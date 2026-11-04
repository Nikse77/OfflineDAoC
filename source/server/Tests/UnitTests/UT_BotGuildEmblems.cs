using DOL.Database;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    [TestFixture]
    public class UT_BotGuildEmblems
    {
        private static DbInventoryItem Item(eInventorySlot slot, eObjectType type, int color = 7, int emblem = 0) =>
            new() { SlotPosition = (int)slot, Object_Type = (int)type, Color = color, Emblem = emblem };

        [Test]
        public void OnlyACloakOrARealShieldCarriesTheGuildEmblem()
        {
            Assert.That(AutonomousBotGuilds.CarriesEmblem(Item(eInventorySlot.Cloak, eObjectType.Cloth)), Is.True);
            Assert.That(AutonomousBotGuilds.CarriesEmblem(Item(eInventorySlot.LeftHandWeapon, eObjectType.Shield)), Is.True);
            // A weapon in the left hand (dual wield) never shows an emblem.
            Assert.That(AutonomousBotGuilds.CarriesEmblem(Item(eInventorySlot.LeftHandWeapon, eObjectType.LeftAxe)), Is.False);
            Assert.That(AutonomousBotGuilds.CarriesEmblem(Item(eInventorySlot.TorsoArmor, eObjectType.Chain)), Is.False);
        }

        [Test]
        public void EveryoneElsesItemsShowTheirOwnEmblemOrColour()
        {
            // Not a bot (a player, a guard, a monster): unchanged.
            Assert.That(AutonomousBotGuilds.Texture(null, Item(eInventorySlot.Cloak, eObjectType.Cloth, 7)), Is.EqualTo(7));
            Assert.That(AutonomousBotGuilds.Texture(null, Item(eInventorySlot.Cloak, eObjectType.Cloth, 7, 1234)), Is.EqualTo(1234));
        }

        [Test]
        public void ABotOfAnAutomaticGuildMovesToThePlayersGuildButABotOfAnotherGuildDoesNot()
        {
            // Owner 2026-11-04: a player's invite takes a bot out of an automatic guild; any other guild keeps its bot.
            Assert.That(AutonomousBotGuilds.MembershipRefusal("Ranren", null, false, "player-guild"), Is.Null);
            Assert.That(AutonomousBotGuilds.MembershipRefusal("Ranren", "auto-guild", true, "player-guild"), Is.Null);
            Assert.That(AutonomousBotGuilds.MembershipRefusal("Ranren", "other-player-guild", false, "player-guild"), Does.Contain("already in a guild"));
            Assert.That(AutonomousBotGuilds.MembershipRefusal("Ranren", "player-guild", false, "player-guild"), Does.Contain("already in your guild"));
        }

        [Test]
        public void NoGuildIsAutomaticUntilTheGamebotsFoundIt()
        {
            Assert.That(AutonomousBotGuilds.IsAutomatic(null), Is.False);
        }

        [Test]
        public void BotMemberEntriesAreNeverCharacterIds()
        {
            Assert.That(AutonomousBotGuilds.ViewId(42), Is.EqualTo("bot:42"));
            Assert.That(AutonomousBotGuilds.IsBotView("bot:42"), Is.True);
            Assert.That(AutonomousBotGuilds.IsBotView("4f9c0e3e-1b2a-4c55-9f1d-3a7d1e2b6c11"), Is.False);
        }
    }
}
