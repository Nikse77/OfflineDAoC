using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using DOL.AI.Brain;
using DOL.Logging;

namespace DOL.GS
{
    /// <summary>
    /// The monsters a party's ranged pull could not draw. A monster that was struck and did not come, or failed
    /// <see cref="AutonomousDefensivePull.AttemptsPerTarget"/> attempts, is set aside for a while (the party pulls another one);
    /// <see cref="AutonomousDefensivePull.MaximumBlockedTargets"/> set aside in a row ask for the ordinary (melee) pull.
    /// </summary>
    public sealed class PullTargetLedger
    {
        private readonly Dictionary<int, int> _attempts = new();
        private readonly Dictionary<int, long> _blockedUntil = new();

        /// <summary>Monsters set aside in a row since the last one that came.</summary>
        public int BlockedInARow { get; set; }

        /// <summary>One failed attempt; true when the party should now pull the ordinary way (and the count starts again).</summary>
        public bool Note(int targetId, bool struck, long now)
        {
            _attempts.TryGetValue(targetId, out int attempts);
            attempts++;
            if (struck || attempts >= AutonomousDefensivePull.AttemptsPerTarget)
            {
                _attempts.Remove(targetId);
                if (_blockedUntil.Count > 64)
                    foreach (int old in _blockedUntil.Where(pair => pair.Value <= now).Select(pair => pair.Key).ToArray())
                        _blockedUntil.Remove(old);
                _blockedUntil[targetId] = now + AutonomousDefensivePull.BlockedTargetMilliseconds;
                BlockedInARow++;
            }
            else
                _attempts[targetId] = attempts;
            if (BlockedInARow < AutonomousDefensivePull.MaximumBlockedTargets)
                return false;
            BlockedInARow = 0;
            return true;
        }

        public bool IsBlocked(int targetId, long now) => _blockedUntil.TryGetValue(targetId, out long until) && now < until;
    }

    /// <summary>One bounded, real ranged pull per level-50 PvE party. No new timers or world scans.</summary>
    public static class AutonomousDefensivePull
    {
        public const int ContactRadius = 400;
        public const int TimeoutMilliseconds = 30_000;
        // Owner 2026-10-28: parties stood at their camp for fifteen minutes and more with the puller "ranged pulling" and nothing
        // shot (a spell it could not cast, no line of sight, out of range) or with the target shot but never coming: each attempt
        // ended silently after 30 s and the next began 10 s later, for ever. After the targets below have been set aside in a row
        // the party pulls the ordinary way for a while.
        public const int MeleeFallbackMilliseconds = 300_000;
        // Owner 2026-10-29: the puller must keep pulling until the monster comes, or change target. A target that did not come
        // after this many attempts is set aside for the party for a while and the next camp target is pulled; only when this many
        // targets in a row have been set aside does the party pull the ordinary way (melee) for five minutes.
        public const int AttemptsPerTarget = 2;
        public const int MaximumBlockedTargets = 3;
        public const long BlockedTargetMilliseconds = 300_000;
        // 2026-10-29 log: 645 failed pulls in two hours, nearly all "shot=True" with the target 600-1,100 away and never coming.
        // A target that was shot and has not come in this time is not coming: the attempt ends without waiting out its 30 s.
        public const int ShotWaitMilliseconds = 15_000;
        private sealed class State
        {
            public GameBot[] Members;
            public GameBot Shooter;
            public GameLiving Target;
            public Vector3 Anchor, FiringPoint;
            public Spell Spell;
            public eActiveWeaponSlot PreviousWeapon;
            public readonly Dictionary<IControlledBrain, eAggressionState> Pets = new();
            public long Until, RetryAfter, NextMove;
            public bool Shot, Active;
            public long ShotTick;
            public eAttackResult ShotResult;
            public readonly PullTargetLedger Ledger = new();
            public int Failures { get => Ledger.BlockedInARow; set => Ledger.BlockedInARow = value; }
            public long MeleeFallbackUntil;
        }
        private static readonly ConditionalWeakTable<Group, State> States = new();
        private static readonly ConditionalWeakTable<GameBot, State> Holding = new();

        // Every PvE party of three or more pulls from range into the party (owner, 2026-10-06): a tank
        // walking into a large pack never works at any level. allLevelFifty is kept for callers.
        public const int MinimumPullParty = 3;
        public const float MaximumFiringAdvance = 450;
        public static bool UsesDefensivePull(bool groupPve, int memberCount, bool allLevelFifty) =>
            groupPve && memberCount >= MinimumPullParty;

        public static bool IsPullSpell(Spell spell, int level) => spell != null && spell.Level <= level &&
            spell.Target == eSpellTarget.ENEMY && spell.Range >= 1000 && spell.Radius == 0 && spell.Damage > 0 && !spell.NeedInstrument &&
            spell.SpellType is eSpellType.DirectDamage or eSpellType.Lifedrain or eSpellType.DamageOverTime;

        private static Spell PullSpell(GameBot bot) => bot.Spells?.Where(spell => IsPullSpell(spell, bot.Level))
            .OrderByDescending(spell => spell.Range).ThenByDescending(spell => spell.Level).FirstOrDefault();

        // Flying-target handoff must choose a spell that can be fired now.
        // This is deliberately separate from the established level-50 pull
        // selection so ordinary targets retain their original behavior.
        public static Spell SelectReadyPullSpell(IEnumerable<Spell> spells, int level, int mana,
            Func<Spell, int> powerCost, Func<Spell, int> disabledDuration) =>
            spells?.Where(spell => IsPullSpell(spell, level) &&
                    powerCost(spell) <= mana && disabledDuration(spell) == 0)
                .OrderByDescending(spell => spell.Range).ThenByDescending(spell => spell.Level)
                .FirstOrDefault();

        private static Spell ReadyPullSpell(GameBot bot) => bot == null ? null :
            SelectReadyPullSpell(bot.Spells, bot.Level, bot.Mana, bot.PowerCost, bot.GetSkillDisabledDuration);

        private static bool HasUsableDistanceWeapon(GameBot bot) =>
            BotRangedCombat.CanUse(bot, bot.Inventory?.GetItem(eInventorySlot.DistanceWeapon));

        public static bool HasRangedPull(GameBot bot) => bot?.CharacterClass != null &&
            !SavageBotCombatPolicy.MustMeleePull((eCharacterClass)bot.CharacterClass.ID) &&
            (PullSpell(bot) != null || BotRangedCombat.CanUse(bot, bot.Inventory?.GetItem(eInventorySlot.DistanceWeapon)));

        /// <summary>The party's ranged-pull state for the pull-hold trace (diagnostics only).</summary>
        public static string Describe(Group group)
        {
            if (group == null || !States.TryGetValue(group, out State state)) return "dp=none";
            lock (state)
            {
                long now = GameLoop.GameLoopTime;
                return $"dp(active={state.Active} failures={state.Failures} retryInMs={Math.Max(0, state.RetryAfter - now)} " +
                       $"meleeFallbackInMs={Math.Max(0, state.MeleeFallbackUntil - now)} shooter={state.Shooter?.Name} " +
                       $"target={state.Target?.Name} shot={state.Shot})";
            }
        }

        public static bool OwnsRangedPosition(GameNPC npc) => npc is GameBot bot && bot.Group != null &&
            States.TryGetValue(bot.Group, out State state) && state.Active && state.Shooter == bot;

        public static bool IsScopedFlyingHandoff(ushort region, GameNPC.eFlags flags,
            bool designatedHasMeleeApproach, bool groupCanPull) =>
            region is 248 or 249 or 60 or 160 or 191 &&
            (flags & GameNPC.eFlags.FLYING) != 0 && !designatedHasMeleeApproach && groupCanPull;

        /// <summary>Only a DF/epic-dungeon flyer unreachable by the normal
        /// tank receives a temporary ranged group shooter.  The designated
        /// puller and all ordinary camps retain their existing behavior.</summary>
        public static bool TryBeginFlyingHandoff(GameBot designated, GameNPC target, bool corridorBlocker = false)
            => TryChooseFlyingHandoffShooter(designated, target, corridorBlocker, out GameBot shooter) &&
               TryBeginCore(shooter, target, flyingHandoff: true);

        public static bool CanAttemptFlyingHandoff(GameBot designated, GameNPC target, bool corridorBlocker = false)
            => TryChooseFlyingHandoffShooter(designated, target, corridorBlocker, out _);

        private static bool TryChooseFlyingHandoffShooter(GameBot designated, GameNPC target,
            bool corridorBlocker, out GameBot shooter)
        {
            shooter = null;
            if (designated?.Group == null || target?.IsAlive != true ||
                !IsScopedFlyingHandoff(target.CurrentRegionID, target.Flags, false,
                    designated.CurrentRegion == target.CurrentRegion &&
                    AutonomousBotGroupCoordinator.CanInitiateNewPull(designated, corridorBlocker))) return false;
            var nav = PathfindingProvider.Instance;
            Zone zone = designated.CurrentZone;
            if (zone == null || !nav.IsAvailable || !nav.HasNavmesh(zone)) return false;
            Vector3 from = new(designated.X, designated.Y, designated.Z);
            Vector3 enemy = new(target.X, target.Y, target.Z);
            if (AutonomousDungeonTargetRoute.CanReach(nav, zone, from, enemy)) return false;
            GameBot[] members = designated.Group.GetMembersInTheGroup().OfType<GameBot>()
                .Where(member => member.IsAlive && member.Group == designated.Group &&
                    member.CurrentRegion == designated.CurrentRegion && !member.IsOnStableMasterRoute &&
                    GameServer.ServerRules.IsAllowedToAttack(member, target, true))
                .ToArray();
            if (members.Length < 2) return false;
            shooter = members.Where(member => member != designated &&
                    !SavageBotCombatPolicy.MustMeleePull((eCharacterClass)member.CharacterClass.ID) &&
                    (ReadyPullSpell(member) != null || HasUsableDistanceWeapon(member)))
                .OrderBy(member => member.GetDistanceTo(target))
                .FirstOrDefault(member =>
                {
                    Spell spell = ReadyPullSpell(member);
                    // Do not switch several candidates' equipped weapons just
                    // to inspect their range. 1000 is a conservative preflight
                    // for a stowed bow; the actual range is checked after the
                    // chosen shooter switches in TryBeginCore.
                    int range = spell?.Range ?? (member.ActiveWeaponSlot == eActiveWeaponSlot.Distance
                        ? member.attackComponent.AttackRange : 1000);
                    return TryFiringPoint(nav, member.CurrentZone,
                        new(member.X, member.Y, member.Z), enemy, range, out _);
                });
            return shooter != null;
        }

        // True means this party's pull is handled, including a failed approach.
        // Never fall through into a melee charge merely because a ranged pull failed.
        public static bool TryBegin(GameBot shooter, GameLiving target)
            => TryBeginCore(shooter, target, flyingHandoff: false);

        /// <summary>
        /// Owner 2026-10-26: a /pull (and so /grind) by one of the player's own
        /// /spawn companions with a real distance weapon (no spells): one shot
        /// from a short proven firing point, back to the party, everyone waits
        /// for the monster. False means nothing started and the tank pulls in
        /// melee as before.
        /// </summary>
        public static bool TryBeginCompanionRangedPull(GameBot shooter, GameLiving target)
            => shooter is { IsTemporaryGroupHelper: true, IsAutonomousWorldBot: false } &&
               HasUsableDistanceWeapon(shooter) &&
               TryBeginCore(shooter, target, flyingHandoff: false, companionPull: true);

        public static bool IsCompanionRangedPuller(GameBot bot) => bot?.CharacterClass != null &&
            !SavageBotCombatPolicy.MustMeleePull((eCharacterClass)bot.CharacterClass.ID) && HasUsableDistanceWeapon(bot);

        // Who shoots: an armed tank first, then other melee classes, then the
        // archers. Casters and support classes never take this job (-1).
        public static int RangedPullerRank(BotPartyRole role, bool listCaster, bool dedicatedArcher)
        {
            if (dedicatedArcher) return 2;
            if (listCaster || role == BotPartyRole.Support) return -1;
            return role == BotPartyRole.Tank ? 0 : 1;
        }

        private static int RangedPullerRank(GameBot bot)
        {
            var characterClass = (eCharacterClass)bot.CharacterClass.ID;
            return RangedPullerRank(BotPartyRoles.For(characterClass),
                bot.CharacterClass.ClassType == eClassType.ListCaster,
                BotRangedCombat.IsDedicatedArcher(characterClass));
        }

        public static GameBot SelectCompanionRangedPuller(IEnumerable<GameBot> companions, GameLiving target) => companions
            .Where(bot => bot is { IsTemporaryGroupHelper: true, IsAutonomousWorldBot: false, IsAlive: true } &&
                !bot.IsCrowdControlled && bot.CharacterClass != null &&
                RangedPullerRank(bot) >= 0 && IsCompanionRangedPuller(bot))
            .OrderBy(RangedPullerRank).ThenBy(bot => bot.GetDistanceTo(target)).FirstOrDefault();

        private static bool TryBeginCore(GameBot shooter, GameLiving target, bool flyingHandoff, bool companionPull = false)
        {
            // Suppress only a new scripted pull of this expedition's dragon.
            // Attacked-by-enemy and shared defense paths are unchanged.
            if (AutonomousRealmRaid.IsPendingDragonTarget(shooter,target)) return true;
            if (shooter?.CharacterClass != null &&
                SavageBotCombatPolicy.MustMeleePull((eCharacterClass)shooter.CharacterClass.ID))
                return false;
            if (shooter?.Group == null ||
                !flyingHandoff && !companionPull && !AutonomousBotGroupCoordinator.UsesDefensivePullGroup(shooter.Group)) return false;
            State state = States.GetOrCreateValue(shooter.Group);
            lock (state)
            {
                if (state.Active) return true;
                if (!flyingHandoff && !companionPull && GameLoop.GameLoopTime < state.MeleeFallbackUntil) return false;
                // A player's /pull or /grind is paced by the player; the autonomous 10-30 s retry gate does not apply.
                if (!companionPull && GameLoop.GameLoopTime < state.RetryAfter) return true;
                GameBot[] members = shooter.Group.GetMembersInTheGroup().OfType<GameBot>().ToArray();
                if (target?.IsAlive != true || shooter.CurrentRegion != target.CurrentRegion)
                    return !companionPull;
                if (members.Length < (companionPull ? 1 : flyingHandoff ? 2 : MinimumPullParty))
                    return true;
                // An enemy already in the party is a real defensive fight, not a ranged pull.
                if (!flyingHandoff && members.Any(member => member.IsWithinRadius(target, ContactRadius))) return false;
                // A companion shooter always uses its distance weapon, never a spell.
                Spell spell = companionPull ? null : flyingHandoff ? ReadyPullSpell(shooter) : PullSpell(shooter);
                // A puller with no ranged pull at all (common below level 50) pulls the ordinary way;
                // reporting "handled" here left such a party standing at its camp for good.
                if (spell == null && !flyingHandoff && !HasRangedPull(shooter)) return false;
                state.RetryAfter = GameLoop.GameLoopTime + TimeoutMilliseconds;
                eActiveWeaponSlot previous = shooter.ActiveWeaponSlot;
                if (spell == null)
                {
                    if (flyingHandoff && !HasUsableDistanceWeapon(shooter)) return true;
                    shooter.SwitchWeapon(eActiveWeaponSlot.Distance);
                }
                int range = spell?.Range ?? shooter.attackComponent.AttackRange;
                Vector3 origin = new(shooter.X, shooter.Y, shooter.Z), enemy = new(target.X, target.Y, target.Z);
                if (!TryFiringPoint(PathfindingProvider.Instance, shooter.CurrentZone, origin, enemy, range, out Vector3 point))
                {
                    shooter.SwitchWeapon(previous);
                    // No proven firing point: /pull falls back to the tank's melee pull at once.
                    if (companionPull)
                    {
                        state.RetryAfter = 0;
                        return false;
                    }
                    // Merely too far for the short proven approach: the party keeps walking in and the
                    // camp check (every 1.5 s) pulls once in range. Holding here stood parties 2,000
                    // units from their camp (1,087 unreachable pulls in run 11's first hour, mostly
                    // low-level parties with short pull ranges).
                    if (Vector3.Distance(origin, enemy) - (range - 100) > MaximumFiringAdvance)
                    {
                        state.RetryAfter = 0;
                        return false;
                    }
                    LoggerManager.Create(typeof(AutonomousDefensivePull)).Info(
                        $"AUTONOMOUS_DEFENSIVE_PULL_UNREACHABLE bot={shooter.Name} target={target.Name} region={shooter.CurrentRegionID} x={shooter.X} y={shooter.Y} z={shooter.Z}");
                    // The same target again and again (26 times for one party, 2026-10-28): after the second the party pulls the
                    // ordinary way for a while instead of standing at its camp.
                    if (!flyingHandoff && NoteFailedTarget(state, target.ObjectID, struck: true))
                    {
                        state.RetryAfter = 0;
                        return false;
                    }
                    return true;
                }
                state.Members = members;
                state.Shooter = shooter;
                state.Target = target;
                state.Anchor = origin;
                state.FiringPoint = point;
                state.Spell = spell;
                state.PreviousWeapon = previous;
                state.Shot = false;
                state.ShotTick = 0;
                state.ShotResult = eAttackResult.Any;
                state.Until = GameLoop.GameLoopTime + TimeoutMilliseconds;
                state.NextMove = 0;
                state.Active = true;
                foreach (GameBot member in members)
                {
                    member.WakeRecoveryRest();
                    Holding.Remove(member);
                    Holding.Add(member, state);
                    member.StopAttack();
                    member.StopFollowing();
                    member.StopMovingOnPath();
                    member.StopMoving();
                    var visited = new HashSet<IControlledBrain>();
                    foreach (GameNPC pet in BotGroupPetBuffTargets.AttachedTree(member.ControlledBrain, member, visited))
                        if (pet.Brain is IControlledBrain brain && state.Pets.TryAdd(brain, brain.AggressionState))
                        {
                            brain.Disengage();
                            brain.SetAggressionState(eAggressionState.Passive);
                            brain.FollowOwner();
                        }
                }
                return true;
            }
        }

        public static bool TryFiringPoint(IPathfindingMgr nav, Zone zone, Vector3 origin, Vector3 target,
            int range, out Vector3 point)
        {
            point = origin;
            if (nav?.IsAvailable != true || zone == null || range < 1000) return false;
            float distance = Vector3.Distance(origin, target);
            float advance = Math.Max(0, distance - (range - 100));
            // No solo expedition into a pack: at most a short, proven approach from the party.
            if (advance > MaximumFiringAdvance) return false;
            Vector3 desired = advance == 0 ? origin : Vector3.Lerp(origin, target, advance / distance);
            if (!AutonomousNavigationSurface.TryFloor(nav, zone, desired, out point)) return false;
            return Vector3.Distance(origin, point) <= 450 && Vector3.Distance(point, target) <= range - 50 &&
                nav.HasLineOfSight(zone, point, target, nav.DefaultFilters) &&
                AutonomousZoneItinerary.HasCompleteCorridor(nav, zone, origin, point) &&
                AutonomousZoneItinerary.HasCompleteCorridor(nav, zone, point, origin);
        }

        public static bool Hold(GameBot bot)
        {
            if (bot == null || !Holding.TryGetValue(bot, out State state)) return false;
            lock (state)
            {
                if (!state.Active) return false;
                if (bot.Group == null || !state.Target.IsAlive || state.Target.ObjectState != GameObject.eObjectState.Active ||
                    state.Members.Any(member => !member.IsAlive || member.Group != bot.Group ||
                        member.CurrentRegion != state.Shooter.CurrentRegion || member.IsOnStableMasterRoute) ||
                    state.Target.CurrentRegion != bot.CurrentRegion || GameLoop.GameLoopTime >= state.Until ||
                    state.Shot && state.ShotTick > 0 && GameLoop.GameLoopTime - state.ShotTick >= ShotWaitMilliseconds)
                {
                    if (GameLoop.GameLoopTime >= state.Until ||
                        state.Shot && state.ShotTick > 0 && GameLoop.GameLoopTime - state.ShotTick >= ShotWaitMilliseconds)
                        RecordFailedAttempt(state);
                    Finish(state, false);
                    return false;
                }
                if (state.Members.Any(member => member.IsWithinRadius(state.Target, ContactRadius)))
                {
                    Finish(state, true);
                    return false;
                }
                if (bot != state.Shooter) return true;
                Vector3 destination = state.Shot ? state.Anchor : state.FiringPoint;
                if (Vector3.DistanceSquared(new(bot.X, bot.Y, bot.Z), destination) > 45 * 45)
                {
                    if (!bot.IsCasting && GameLoop.GameLoopTime >= state.NextMove)
                    {
                        state.NextMove = GameLoop.GameLoopTime + 1500;
                        bot.PathTo(destination, bot.MaxSpeed);
                    }
                    return true;
                }
                if (bot.IsCasting || bot.castingComponent?.HasPendingSkillRequests == true) return true;
                bot.StopMovingOnPath();
                bot.StopMoving();
                if (state.Shot || bot.IsCrowdControlled) return true;
                int range = state.Spell?.Range ?? bot.attackComponent.AttackRange;
                if (!bot.IsWithinRadius(state.Target, range - 50) ||
                    !PathfindingProvider.Instance.HasLineOfSight(bot.CurrentZone, new(bot.X, bot.Y, bot.Z),
                        new(state.Target.X, state.Target.Y, state.Target.Z), PathfindingProvider.Instance.DefaultFilters)) return true;
                bot.TargetObject = state.Target;
                if (state.Spell != null)
                {
                    if (bot.Mana >= bot.PowerCost(state.Spell) && bot.GetSkillDisabledDuration(state.Spell) == 0)
                        bot.CastSpell(state.Spell, SkillBase.GetSpellLine(GlobalSpellsLines.Mob_Spells));
                }
                else if (!bot.IsAttacking && bot.Endurance >= bot.rangeAttackComponent.ShotEnduranceCost)
                    bot.StartAttack(state.Target);
                return true;
            }
        }

        public static void OnAttack(GameLiving actor, AttackData attack)
        {
            if (actor?.Group == null || !States.TryGetValue(actor.Group, out State state)) return;
            lock (state)
            {
                if (!state.Active || state.Shooter != actor || attack?.Attacker != actor ||
                    attack.Target != state.Target || !attack.CausesCombat) return;
                state.Shot = true;
                state.ShotTick = GameLoop.GameLoopTime;
                state.ShotResult = attack.AttackResult;
                state.Shooter.StopAttack();
            }
        }

        public static void OnThreat(GameLiving member, GameLiving attacker)
        {
            if (member?.Group == null || !States.TryGetValue(member.Group, out State state)) return;
            lock (state)
                if (state.Active && (attacker != state.Target || member.IsWithinRadius(attacker, ContactRadius)))
                    Finish(state, attacker == state.Target);
        }

        public static void Cancel(Group group)
        {
            if (group == null || !States.TryGetValue(group, out State state)) return;
            lock (state) if (state.Active) Finish(state, false);
        }

        /// <summary>
        /// A failed attempt on one target. A target that was struck and did not come, or failed <see cref="AttemptsPerTarget"/>
        /// times, is set aside for <see cref="BlockedTargetMilliseconds"/> (the party then pulls another one); true when
        /// <see cref="MaximumBlockedTargets"/> targets in a row were set aside: the party pulls the ordinary way for a while.
        /// </summary>
        private static bool NoteFailedTarget(State state, int targetId, bool struck)
        {
            long now = GameLoop.GameLoopTime;
            if (!state.Ledger.Note(targetId, struck, now))
                return false;
            state.MeleeFallbackUntil = now + MeleeFallbackMilliseconds;
            return true;
        }

        /// <summary>True when the party's ranged pull set this monster aside (it did not come); the camp search skips it.</summary>
        public static bool IsTargetBlocked(Group group, GameLiving target)
        {
            if (group == null || target == null || !States.TryGetValue(group, out State state)) return false;
            lock (state)
                return state.Ledger.IsBlocked(target.ObjectID, GameLoop.GameLoopTime);
        }

        // An attempt that ran out its 30 s without contact (nothing shot, or the shot target never came) says why (the shooter's own view) and, at the second one in a row, hands
        // the party to the ordinary pull for five minutes.
        private static void RecordFailedAttempt(State state)
        {
            GameBot shooter = state.Shooter;
            // A shot that struck and still did not bring the target says the ranged pull does not draw this one: another target.
            bool struck = state.Shot && state.ShotResult is eAttackResult.HitUnstyled or eAttackResult.HitStyle;
            bool fallBack = NoteFailedTarget(state, state.Target.ObjectID, struck);
            try
            {
                int range = state.Spell?.Range ?? shooter.attackComponent.AttackRange;
                bool lineOfSight = PathfindingProvider.Instance.HasLineOfSight(shooter.CurrentZone, new(shooter.X, shooter.Y, shooter.Z),
                    new(state.Target.X, state.Target.Y, state.Target.Z), PathfindingProvider.Instance.DefaultFilters);
                LoggerManager.Create(typeof(AutonomousDefensivePull)).Warn(
                    $"AUTONOMOUS_DEFENSIVE_PULL_FAILED group={shooter.Group?.GetHashCode()} shooter={shooter.Name} class={shooter.ClassName} level={shooter.Level} " +
                    $"target={state.Target.Name} spell={state.Spell?.Name ?? "distance weapon"} range={range} shot={state.Shot} " +
                    $"distanceToTarget={(int)shooter.GetDistanceTo(state.Target)} " +
                    $"distanceToFiringPoint={(int)Vector3.Distance(new(shooter.X, shooter.Y, shooter.Z), state.FiringPoint)} lineOfSight={lineOfSight} " +
                    $"mana={shooter.Mana} cost={(state.Spell == null ? 0 : shooter.PowerCost(state.Spell))} " +
                    $"disabledMs={(state.Spell == null ? 0 : shooter.GetSkillDisabledDuration(state.Spell))} casting={shooter.IsCasting} " +
                    $"shotResult={state.ShotResult} msSinceShot={(state.ShotTick > 0 ? GameLoop.GameLoopTime - state.ShotTick : -1)} " +
                    $"targetInCombat={state.Target.InCombat} targetTargets={state.Target.TargetObject?.Name ?? "none"} " +
                    $"targetFlying={(state.Target as GameNPC)?.Flags.HasFlag(GameNPC.eFlags.FLYING)} " +
                    $"crowdControlled={shooter.IsCrowdControlled} failures={state.Failures} meleeFallback={fallBack} " +
                    $"targetSetAside={state.Ledger.IsBlocked(state.Target.ObjectID, GameLoop.GameLoopTime)}");
            }
            catch (Exception)
            {
                // The report is diagnostics only (and needs a live logger and navmesh): the fallback below never depends on it.
            }
        }

        private static void Finish(State state, bool engage)
        {
            state.Active = false;
            if (engage) state.Failures = 0;
            state.RetryAfter = GameLoop.GameLoopTime + 10_000;
            state.Shooter.StopAttack();
            state.Shooter.SwitchWeapon(state.PreviousWeapon);
            foreach (var pet in state.Pets)
                if (pet.Key.AggressionState == eAggressionState.Passive) pet.Key.SetAggressionState(pet.Value);
            state.Pets.Clear();
            foreach (GameBot member in state.Members)
            {
                Holding.Remove(member);
                if (member.Brain is BotBrain brain)
                {
                    if (engage && member.IsAlive && member.Group == state.Shooter.Group &&
                        member.CurrentRegion == state.Target.CurrentRegion)
                    {
                        // Only /spawn casters wait (see BotBrain.HoldOffenseAfterPull).
                        brain.HoldOffenseAfterPull();
                        brain.AddToAggroList(state.Target, Math.Max(25, state.Target.EffectiveLevel * 10));
                        brain.CommitDungeonPull(state.Target);
                        brain.FSM.SetCurrentState(eFSMStateType.AGGRO);
                    }
                    else brain.RemoveFromAggroList(state.Target);
                }
            }
        }
    }
}
