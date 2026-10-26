using DOL.AI.Brain;
using DOL.Database;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    /// <summary>Casters with no power for any attack spell fight with their staff.</summary>
    [TestFixture]
    public class UT_OutOfPowerCasterMelee
    {
        [Test]
        public void OutOfPowerMeansNoAttackSpellIsAffordable()
        {
            Assert.Multiple(() =>
            {
                Assert.That(BrainOutOfPower(new[] { 20, 35 }, 10), Is.True, "nothing affordable");
                Assert.That(BrainOutOfPower(new[] { 20, 35 }, 20), Is.False, "the cheap nuke is affordable");
                Assert.That(BrainOutOfPower(new[] { 0, 35 }, 0), Is.False, "a free spell can still be cast");
                Assert.That(BrainOutOfPower(new int[0], 0), Is.False, "no attack spells known");
            });
        }

        [Test]
        public void OnlyTemporaryCompanionCastersGiveUpStaffMelee()
        {
            Assert.Multiple(() =>
            {
                Assert.That(BotBrain.CasterStaffMeleeAllowed(temporaryCompanion: true, autonomous: false), Is.False,
                    "a /spawn caster stays at range");
                Assert.That(BotBrain.CasterStaffMeleeAllowed(temporaryCompanion: false, autonomous: true), Is.True,
                    "autonomous gamebots keep their staff fallback");
                Assert.That(BotBrain.CasterStaffMeleeAllowed(temporaryCompanion: false, autonomous: false), Is.True,
                    "other bots are unchanged");
            });
        }

        [Test]
        public void TankPeelsForPlayerThenHealersThenCastersThenOthers()
        {
            Assert.Multiple(() =>
            {
                Assert.That(BotBrain.ProtectionPriority(true, false, false), Is.EqualTo(0), "the player first");
                Assert.That(BotBrain.ProtectionPriority(false, true, false), Is.EqualTo(1), "then healers");
                Assert.That(BotBrain.ProtectionPriority(false, false, true), Is.EqualTo(2), "then pure casters");
                Assert.That(BotBrain.ProtectionPriority(false, false, false), Is.EqualTo(3), "then everyone else");
            });
        }

        [Test]
        public void OnlyTemporaryCompanionCastersWaitAfterAPull()
        {
            Assert.Multiple(() =>
            {
                Assert.That(BotBrain.HoldsOffenseAfterPull(true, false, true), Is.True, "a /spawn caster waits");
                Assert.That(BotBrain.HoldsOffenseAfterPull(true, false, false), Is.False, "melee and healers do not");
                Assert.That(BotBrain.HoldsOffenseAfterPull(false, true, true), Is.False, "autonomous casters do not");
            });
        }

        [Test]
        public void PullHoldLastsThreeSecondsUnlessTheCasterIsHit()
        {
            long start = 10_000, until = start + BotBrain.PullOffenseHoldMilliseconds;
            Assert.Multiple(() =>
            {
                Assert.That(BotBrain.PullOffenseStillHeld(start + 2_999, until, false), Is.True, "still waiting");
                Assert.That(BotBrain.PullOffenseStillHeld(start + 3_000, until, false), Is.False, "three seconds passed");
                Assert.That(BotBrain.PullOffenseStillHeld(start + 1_000, until, true), Is.False, "hit: attack at once");
            });
        }

        [TestCase(eSpellType.SummonTheurgistPet, true)]
        [TestCase(eSpellType.SummonAnimistFnF, true)]
        [TestCase(eSpellType.SummonAnimistAmbusher, true)]
        [TestCase(eSpellType.SummonAnimistPet, false)]
        [TestCase(eSpellType.SummonSimulacrum, false)]
        [TestCase(eSpellType.DirectDamage, false)]
        public void TheurgistAndAnimistAttackSummonsWaitWithTheCaster(eSpellType type, bool held)
        {
            var spell = new Spell(new DbSpell { Type = type.ToString(), Target = "Enemy" }, 20);
            Assert.That(BotBrain.IsPullHeldSummon(spell), Is.EqualTo(held));
        }

        [Test]
        public void TemporaryCompanionPaladinsLeaveTheArmorChantOutOfTheirTwist()
        {
            Assert.Multiple(() =>
            {
                Assert.That(CompanionChantAnimation.SkipsInGroupTwist(true, false, eSpellType.SpecArmorFactorBuff), Is.True,
                    "a /spawn Paladin skips Crusader's Ward");
                Assert.That(CompanionChantAnimation.SkipsInGroupTwist(true, false, eSpellType.DamageAdd), Is.False,
                    "it keeps its other chants");
                Assert.That(CompanionChantAnimation.SkipsInGroupTwist(false, true, eSpellType.SpecArmorFactorBuff), Is.False,
                    "autonomous Paladins keep the armor chant");
            });
        }

        [Test]
        public void OnlyTemporaryCompanionsHideTheirNameplate()
        {
            Assert.Multiple(() =>
            {
                Assert.That(CompanionNameplate.HidesName(true, false), Is.True, "a /spawn companion");
                Assert.That(CompanionNameplate.HidesName(false, true), Is.False, "autonomous gamebots keep their names");
                Assert.That(CompanionNameplate.HidesName(false, false), Is.False, "other bots keep their names");
                Assert.That(CompanionNameplate.HidesName(true, false, viewerShowsNames: false), Is.True,
                    "hidden by default");
                Assert.That(CompanionNameplate.HidesName(true, false, viewerShowsNames: true), Is.False,
                    "/companionnames on shows them");
            });
        }

        [Test]
        public void TemporaryCompanionHealersHoldTheirPositionDuringAFight()
        {
            Assert.Multiple(() =>
            {
                Assert.That(CompanionHealerCombatHold.Holds(true, false, true, true, true), Is.True,
                    "a /spawn healer stays put while the group fights");
                Assert.That(CompanionHealerCombatHold.Holds(true, false, true, false, true), Is.False,
                    "it follows again when the fight is over");
                Assert.That(CompanionHealerCombatHold.Holds(true, false, true, true, false), Is.False,
                    "it follows a leader beyond the leash");
                Assert.That(CompanionHealerCombatHold.Holds(true, false, false, true, true), Is.False,
                    "other companions keep their formation");
                Assert.That(CompanionHealerCombatHold.Holds(false, true, true, true, true), Is.False,
                    "autonomous healers are unchanged");
            });
        }

        private static bool BrainOutOfPower(int[] costs, int mana) => BotBrain.OutOfPower(costs, mana);
    }
}
