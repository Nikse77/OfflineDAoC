using System;
using System.Runtime.CompilerServices;
using DOL.GS.PacketHandler;

namespace DOL.GS
{
    /// <summary>
    /// Owner 2026-10-27: small human touches for gamebots and companions, animations and the odd line only. A bot answers
    /// a player's emote, dings on a level, bows to whoever raised it, sometimes cheers a realm kill, fidgets while it stands
    /// waiting and waves its party off. Nothing here changes combat, movement or decisions; a bot that is fighting,
    /// casting or stealthed shows nothing.
    /// </summary>
    public static class BotEmotes
    {
        private sealed class State { public long NextEmote, StillSince, NextIdle; }
        private static readonly ConditionalWeakTable<GameBot, State> States = new();

        public const long MinimumGapMilliseconds = 4_000;
        public const long IdleAfterMilliseconds = 20_000;
        public const int IdleMinimumMilliseconds = 30_000;
        public const int IdleMaximumMilliseconds = 90_000;
        public const double VictoryChance = 0.25;
        public const double DingLineChance = 0.25;

        private static readonly eEmote[] IdleEmotes = [eEmote.Yawn, eEmote.Drink, eEmote.Ponder, eEmote.Smile];
        private static readonly string[] DingLines = ["ding!", "Ding!", "ding :)", "finally, ding", "ding! one more"];

        /// <summary>The bot's answer to a player's emote, or null when it lets it pass.</summary>
        public static eEmote? ReplyTo(eEmote emote) => emote switch
        {
            eEmote.Wave => eEmote.Wave,
            eEmote.Bow or eEmote.Curtsey or eEmote.Clap => eEmote.Bow,
            eEmote.Salute or eEmote.Military => eEmote.Salute,
            eEmote.Cheer or eEmote.Victory => eEmote.Cheer,
            eEmote.Hug or eEmote.BlowKiss => eEmote.Blush,
            eEmote.Rude or eEmote.Taunt or eEmote.Slap => eEmote.Shrug,
            eEmote.Dance or eEmote.Laugh or eEmote.Rofl => eEmote.Laugh,
            eEmote.Beckon or eEmote.LetsGo => eEmote.Yes,
            _ => null
        };

        /// <summary>True when a bot standing still since <paramref name="stillSince"/> may fidget now.</summary>
        public static bool ShouldFidget(long now, long stillSince, long nextIdle) =>
            now - stillSince >= IdleAfterMilliseconds && now >= nextIdle;

        /// <summary>1. A player's emote aimed at the bot: it answers in kind after a moment.</summary>
        public static void Answer(GameBot bot, GamePlayer player, eEmote emote)
        {
            if (bot == null || player == null || bot.Realm != player.Realm || ReplyTo(emote) is not { } reply) return;
            Schedule(bot, Random.Shared.Next(1_000, 2_000), () => Act(bot, reply, player));
        }

        /// <summary>2. A level gained: the level-up animation, and sometimes a "ding".</summary>
        public static void LevelUp(GameBot bot)
        {
            if (bot == null) return;
            Schedule(bot, 1_500, () =>
            {
                if (Act(bot, eEmote.LvlUp, null, allowCombat: true) && Random.Shared.NextDouble() < DingLineChance)
                    bot.Say(DingLines[Random.Shared.Next(DingLines.Length)]);
            });
        }

        /// <summary>3. Raised by someone else: a bow or salute toward them (OnResurrectedBy already says its thanks).</summary>
        public static void Thank(GameBot bot, GameLiving caster)
        {
            if (bot == null || caster == null || caster == bot) return;
            Schedule(bot, 2_500, () =>
            {
                if (!caster.IsAlive || !bot.IsWithinRadius(caster, WorldMgr.VISIBILITY_DISTANCE)) return;
                Act(bot, Random.Shared.Next(2) == 0 ? eEmote.Bow : eEmote.Salute, caster);
            });
        }

        /// <summary>4. A realm kill: once the fight is over, sometimes a victory gesture.</summary>
        public static void Victory(GameBot bot)
        {
            if (bot == null || Random.Shared.NextDouble() >= VictoryChance) return;
            eEmote emote = bot.Realm == eRealm.Midgard && Random.Shared.Next(3) == 0 ? eEmote.BangOnShield
                : Random.Shared.Next(2) == 0 ? eEmote.Victory : eEmote.Cheer;
            Schedule(bot, Random.Shared.Next(3_000, 6_000), () => Act(bot, emote, null));
        }

        /// <summary>5. Called every think: a bot standing idle for a while fidgets now and then.</summary>
        public static void Tick(GameBot bot)
        {
            if (bot == null) return;
            State state = States.GetOrCreateValue(bot);
            long now = GameLoop.GameLoopTime;
            if (!bot.IsAlive || bot.IsMoving || bot.InCombat || bot.IsAttacking || bot.IsCasting || bot.IsStealthed ||
                bot.IsOnStableMasterRoute)
            {
                state.StillSince = now;
                return;
            }
            if (state.StillSince == 0) state.StillSince = now;
            if (!ShouldFidget(now, state.StillSince, state.NextIdle)) return;
            state.NextIdle = now + Random.Shared.Next(IdleMinimumMilliseconds, IdleMaximumMilliseconds + 1);
            Act(bot, IdleEmotes[Random.Shared.Next(IdleEmotes.Length)], null);
        }

        /// <summary>6. A party sets out from its meeting point: its leader calls it on.</summary>
        public static void SetOut(GameBot leader)
        {
            if (leader == null) return;
            Schedule(leader, 500, () => Act(leader, Random.Shared.Next(2) == 0 ? eEmote.LetsGo : eEmote.Beckon, null));
        }

        private static void Schedule(GameBot bot, int delay, Action action) =>
            _ = new ECSGameTimer(bot, _ =>
            {
                action();
                return 0;
            }, delay);

        private static bool Act(GameBot bot, eEmote emote, GameObject face, bool allowCombat = false)
        {
            if (!bot.IsAlive || bot.ObjectState != GameObject.eObjectState.Active || bot.IsStealthed || bot.IsCasting ||
                !allowCombat && (bot.InCombat || bot.IsAttacking))
                return false;
            State state = States.GetOrCreateValue(bot);
            long now = GameLoop.GameLoopTime;
            lock (state)
            {
                if (now < state.NextEmote) return false;
                state.NextEmote = now + MinimumGapMilliseconds;
            }
            if (face != null && !bot.IsMoving) bot.TurnTo(face);
            bot.Emote(emote);
            return true;
        }
    }
}
