using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using DOL.AI.Brain;

namespace DOL.GS;

/// <summary>
/// In-memory coordination for already-assigned RvR groups. A committed force
/// remains attached through travel, deaths, and regrouping until the objective
/// resolves or its four-hour battle window expires. This layer neither writes
/// objective tenure nor changes keeps, relics, damage, or
/// rewards.  It only tells existing groups where real world combat can happen.
/// </summary>
public static partial class AutonomousRvrEventLayer
{
    public const long BattleLifetimeMilliseconds = 4 * 60 * 60_000L;
    /// <summary>Time a marching relic army has to reach its keep (a keep siege has the usual hour).</summary>
    public const long RelicMarchMilliseconds = 2 * 60 * 60_000L;

    [DOL.GS.ServerProperties.ServerProperty("autonomous", "rvr_siege_staged_assault",
        "Keep sieges gather the attackers at a siege camp and assault together (108 present, or a viable force at the one-hour deadline) instead of each warband walking in alone.", true)]
    public static bool StagedAssault = true;
    public const long AttendanceFreshnessMilliseconds = 15_000;
    /// <summary>
    /// A bot siege ends as defended once its attackers, having reached the keep, have been gone from it for ten minutes
    /// (run 2026-10-07: Midgard was wiped at Caer Erasleigh and Albion at Blendrake Faste, yet both events held 55+
    /// defenders on post for the rest of a four-hour timer). A player's own attack keeps its defense rules.
    /// </summary>
    public const long RepelMilliseconds = 10 * 60_000;
    public const long StragglerTimeoutMilliseconds = 8 * 60_000L;
    public const long TargetCooldownMilliseconds = 12 * 60_000;
    public const int OrdinaryAssaultCap = 128;
    public const int RelicAssaultCap = 240;
    public const int RelicCarrierRealmCap = 240;

    public enum Intent { Roam, HuntEnemy, AssaultKeep, AssaultRelicKeep, DefendEvent }

    /// <summary>
    /// Owner 2026-11-02: the menu the coordinator offers a leader when the force has no objective - join an assault,
    /// join a defense, join the relic, patrol (the zone is assigned by the coordinator), open a new assault. The leader
    /// picks one at random among those available.
    /// </summary>
    public enum Choice { Assault, Defense, Relic, Patrol, OpenAssault }

    /// <summary>
    /// Owner 2026-11-02: "più assalti insieme, metti il numero in una costante facilmente modificabile, per ora 1".
    /// How many assaults one realm may have running at the same time before "open an assault" leaves its menu.
    /// </summary>
    public const int MaximumAssaultsPerRealm = 1;

    public sealed record Force(string GroupId, eRealm Realm, int MemberCount, int AverageLevel, int HealerCount,
        bool CanSupplySiege = true, bool RoamingReserve = false, long[] MemberIds = null, int MinimumMemberLevel = 50);
    public sealed record LiveObjective(string Id, string Name, Intent Kind, eRealm OwningRealm, ushort RegionId,
        int X, int Y, int Z, bool IsRelicKeep, int EnemyCount, int FriendlyCount, int GuardStrength, int ClosedDoors,
        bool IsRelicCarrier = false, bool IsPortalKeep = false, bool UnderAttack = false);
    public sealed record Plan(Intent Intent, string TargetId, string Name, ushort RegionId, int X, int Y, int Z,
        bool IsSharedEvent, string Reason);

    private sealed class ActiveEvent
    {
        public required string TargetId;
        public required LiveObjective Target;
        public required eRealm AttackerRealm;
        public required eRealm DefenderRealm;
        public required bool RelicKeep;
        public required long ExpiresTick;
        public bool BattleStarted;
        public bool PreparationNoticeSent;
        public long CreatedTick;
        /// <summary>Owner 2026-11-01: opened as a conquest of a keep in another realm's frontier, not as a retake; the end of
        /// the attempt starts the realm's forty-minute gap before it may try another conquest.</summary>
        public bool Conquest;
        public bool AttackObserved;
        /// <summary>Started by a player or the test harness (ForceStart): joined first, and it takes the army of an
        /// automatic siege by the same realm that has not begun fighting yet.</summary>
        public bool Forced;
        public bool ThirdRealmIntervenes = Random.Shared.NextDouble() < RealmEventPolicy.ThirdRealmChance;
        public bool DefenseMustering, DefenseMarching;
        public long DefenseMusterDeadlineTick, DefenseMusterStartTick;
        /// <summary>The losing realm's one counterattack after a player took its keep (StartRetake).</summary>
        public bool Retake;
        public int RetakeCap;
        public bool DefenseMusterExtended;
        public readonly Dictionary<long, (string Force, long Tick, GameBot Bot)> DefenseMustered = new();
        public long BattleStartedTick;
        /// <summary>Attackers gathered when the army marched (sizes the defense, DefenderCapFor).</summary>
        public int MarchedAttackers;
        /// <summary>Last time any attacker was present at the target after the battle began (RepelMilliseconds).</summary>
        public long AttackersLastSeenTick;
        public bool DefenseReaction;
        public DefenseScale DefenseScale;
        public long LastPressureTick;
        public string PlayerAccount;
        // Staged assault, first stage: the attacking warbands gather at their own border keep and
        // march together once enough are there (or at the muster deadline).
        public bool Marching = true;
        public long MusterDeadlineTick;
        public bool MusterExtended;
        public int MusterExtensions;
        public readonly Dictionary<long, (string Force, long Tick, GameBot Bot, Vector3 Position)> Mustered = new();
        // The marching column: one leader walks the route, the mustered army keeps with it (run 17: armies that
        // walked 50,000+ units one by one lost half their strength to roaming enemies before the siege camp).
        public GameBot MarchLeader;
        // The leader reports each turn it actually leads (TryMarchWithArmy); a leader held by something else (its
        // group's rendezvous, resting, shopping) is replaced, and set aside for a while.
        public long MarchLeaderSinceTick, MarchLeaderHeartbeatTick;
        public readonly Dictionary<long, long> StaleLeaders = new();
        // Last tick each marcher actually took a march turn (its group's own rendezvous can hold it instead).
        public readonly Dictionary<long, long> MarchTurns = new();
        public readonly Dictionary<long, (string Force, eRealm Realm, long Tick, GameBot Bot, Vector3 Position, ushort Region)> Present = new();
        public readonly Dictionary<string, int[]> Slots = new(StringComparer.Ordinal);
        public readonly Dictionary<long, (string Force, long ProgressTick, double BestDistance, Vector3 Position, ushort Region)> Travel = new();
        public readonly Dictionary<string, int> Attackers = new(StringComparer.Ordinal);
        /// <summary>The attacking groups in joining order (organizer first); bookkeeping and logs only (siege rework phase 1).</summary>
        public readonly AutonomousRvrCoalition Coalition = new();
        public readonly Dictionary<string, int> Defenders = new(StringComparer.Ordinal);
        public readonly Dictionary<string, int> ThirdRealm = new(StringComparer.Ordinal);
    }

    private static readonly object Sync = new();
    private static readonly Dictionary<string, ActiveEvent> Events = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, (long ExpiresTick, Dictionary<eRealm, Dictionary<string, int>> Participants)> CarrierEvents = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, LiveObjective> CarrierTargets = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, long> Cooldowns = new(StringComparer.Ordinal);

    /// <summary>
    /// Owner 2026-11-01: a bot that finds no route to a keep (RVR_KEEP_ROUTE_FAILED) used to retry the same planning, almost a
    /// thousand navmesh queries, every 30-120 seconds for as long as the siege lasted. Now its force gives that keep up for this long
    /// and goes where the planner sends it next.
    /// </summary>
    public const long UnreachableAvoidMilliseconds = 20 * 60_000;
    private static readonly Dictionary<(string Force, string Target), long> Avoided = new();

    public static bool IsAvoided(string forceId, string targetId, long nowTick)
    {
        if (string.IsNullOrEmpty(forceId) || string.IsNullOrEmpty(targetId)) return false;
        lock (Sync)
            return Avoided.TryGetValue((forceId, targetId), out long until) && until > nowTick;
    }

    /// <summary>
    /// A member of <paramref name="forceId"/> found no route to <paramref name="targetId"/>: the force leaves that siege and does not
    /// choose the keep again for <see cref="UnreachableAvoidMilliseconds"/>. A rally left with no attackers ends at once, so that its
    /// realm may open another one.
    /// </summary>
    public static void ReportUnreachable(string forceId, string targetId, long nowTick)
    {
        if (string.IsNullOrWhiteSpace(forceId) || string.IsNullOrWhiteSpace(targetId)) return;
        lock (Sync)
        {
            if (Avoided.TryGetValue((forceId, targetId), out long known) && known > nowTick) return;
            Avoided[(forceId, targetId)] = nowTick + UnreachableAvoidMilliseconds;
            var log = DOL.Logging.LoggerManager.Create(typeof(AutonomousRvrEventLayer));
            if (log.IsInfoEnabled) log.Info($"RVR_KEEP_UNREACHABLE force={forceId} target={targetId} avoidMinutes={UnreachableAvoidMilliseconds / 60_000}");
            if (!Events.TryGetValue(targetId, out ActiveEvent active)) return;
            bool attacker = active.Attackers.ContainsKey(forceId);
            active.Attackers.Remove(forceId);
            active.Defenders.Remove(forceId);
            active.ThirdRealm.Remove(forceId);
            active.Slots.Remove(forceId);
            foreach (long id in active.Travel.Where(pair => pair.Value.Force == forceId).Select(pair => pair.Key).ToArray())
                active.Travel.Remove(id);
            foreach (long id in active.Present.Where(pair => pair.Value.Force == forceId).Select(pair => pair.Key).ToArray())
                active.Present.Remove(id);
            foreach (long id in active.Mustered.Where(pair => pair.Value.Force == forceId).Select(pair => pair.Key).ToArray())
                active.Mustered.Remove(id);
            foreach (long id in active.DefenseMustered.Where(pair => pair.Value.Force == forceId).Select(pair => pair.Key).ToArray())
                active.DefenseMustered.Remove(id);
            if (attacker && !active.BattleStarted && !active.Forced && active.Attackers.Count == 0)
                EndEvent(active, nowTick, "Rally failed: no attacker can reach the keep");
        }
    }
    private static readonly Dictionary<string, string> ReleasedForces = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, long> WipedWithdrawn = new(StringComparer.Ordinal);
    private static long _nextStragglerSweep;
    private static readonly HashSet<string> SelectedKeeps = new(StringComparer.Ordinal);
    private static readonly HashSet<string> SelectedRelics = new(StringComparer.Ordinal);

    public static bool ForceStart(LiveObjective target, eRealm attacker, long now, out string reason)
    {
        lock (Sync)
        {
            if (target == null || target.IsPortalKeep || target.IsRelicCarrier ||
                attacker is not (eRealm.Albion or eRealm.Midgard or eRealm.Hibernia) ||
                target.OwningRealm == attacker)
            { reason = "Choose an enemy capturable keep and an attacking realm."; return false; }
            if (Events.ContainsKey(target.Id) || OnCooldown(target.Id, now))
            { reason = "This objective already has an event or an active cooldown."; return false; }
            // A player may force as many sieges as they like (owner, 2026-10-07); the automatic
            // scheduler's one-attack-per-realm and start-chance limits never apply here.
            Events[target.Id] = new ActiveEvent
            {
                TargetId = target.Id, Target = target, AttackerRealm = attacker,
                DefenderRealm = target.OwningRealm, RelicKeep = target.IsRelicKeep, Forced = true,
                CreatedTick = now, ExpiresTick = now + RealmEventPolicy.RecruitmentMilliseconds(Random.Shared.NextDouble())
            };
            SupersedeAutomaticSieges(Events[target.Id], now);
            RealmEventNotices.Queue(target.Id, attacker, $"Warbands are marching on {Place(target.Name)}. Join the assault as you arrive!");
            RealmEventRecords.Begin(target.Id, target.Name, target.IsRelicKeep ? "Relic keep" : "Keep", GlobalConstants.RealmToName(attacker), "Forced continuous siege; defender: " + GlobalConstants.RealmToName(target.OwningRealm));
            // Staged assault: the attackers gather at their physical siege camp and the battle opens
            // when enough are there (or at the one-hour deadline with a viable force). Arriving one
            // at a time, attackers fed a defended keep piecemeal and no keep was ever taken.
            if (!StagedAssault)
                StartBattle(Events[target.Id], now, "forced assault opened; forces converge without formation staging");
            else
            {
                BeginMuster(Events[target.Id], now);
                RealmEventRecords.Progress(target.Id, "Rally", "Forced siege: attackers muster at their border keep, then march to the siege camp", 0, 0);
            }
            reason = "Assault opened. Reinforcements travel immediately; existing battles and roaming reserves are preserved.";
            return true;
        }
    }

    public static bool ResetCooldown(string id)
    {
        lock (Sync)
        {
            if (Events.ContainsKey(id) || CarrierEvents.ContainsKey(id)) return false;
            Cooldowns.Remove(id);
            return true;
        }
    }

    public static long CooldownRemaining(string id, long now)
    {
        lock (Sync) return Math.Max(0, Cooldowns.GetValueOrDefault(id) - now);
    }

    public static Dictionary<string, string> ForceTargets()
    {
        lock (Sync)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var e in Events.Values)
                foreach (string force in e.Attackers.Keys.Concat(e.Defenders.Keys).Concat(e.ThirdRealm.Keys))
                    result[force] = e.TargetId;
            foreach (var e in CarrierEvents)
                foreach (string force in e.Value.Participants.Values.SelectMany(p => p.Keys)) result[force] = e.Key;
            return result;
        }
    }

    public static double MajorAssaultWeight(int memberCount, int averageLevel)
    {
        double size = Math.Clamp((memberCount - 2d) / 6d, 0d, 1d);
        double level = Math.Clamp((averageLevel - 25d) / 25d, 0d, 1d);
        return Math.Clamp(size * 0.55d + level * 0.45d, 0d, 1d);
    }

    /// <summary>
    /// Event notices create an inclination, never conscription.  The ceiling
    /// deliberately leaves a roaming reserve even while an event has open
    /// capacity; a force that opened an assault is already recorded and is not
    /// evaluated by this gate again.
    /// </summary>
    public static bool ShouldJoinActiveEvent(Force force, int existingParticipants, int cap,
        bool relicImportance, bool defending, double roll)
    {
        if (force == null || cap <= 0 || existingParticipants + force.MemberCount > cap)
            return false;

        double forceStrength = MajorAssaultWeight(force.MemberCount, force.AverageLevel);
        double importance = relicImportance ? 0.33d : 0.19d;
        double defenderNeed = defending ? 0.12d : 0d;
        double attendance = Math.Clamp(existingParticipants / (double)cap, 0d, 1d);
        // Attendance raises the attraction so established rallies fill before
        // new forces scatter across sparse objectives. A roaming reserve still
        // remains outside the event system.
        double probability = Math.Clamp(0.18d + importance + defenderNeed + forceStrength * 0.20d + attendance * 0.36d,
            0.12d, 0.78d);
        return Math.Clamp(roll, 0d, 1d) < probability;
    }

    /// <summary>
    /// Owner 2026-11-02: the "open an assault" menu entry is only offered to a force that can carry a siege - at least
    /// four members, average level 50 and siege supplies. Kept as a predicate so the rule is testable now that the old
    /// ChooseIntent roll is gone.
    /// </summary>
    public static bool MayOpenAssault(Force force) =>
        force != null && force.MemberCount >= 4 && force.AverageLevel >= 50 && force.CanSupplySiege;

    public const double RelicSiegeShare = 0.12;

    // Chance-based concurrent sieges (one attack per realm). Off in the legacy event-layer tests, which
    // document the one-siege-at-a-time ChooseIntent rules.
    public static bool SiegeScheduling = true;
    private static long _nextSiegeRoll, _lastSiegeActiveTick;
    private static bool _siegeStartReady;

    /// <summary>Chance per minute that a new siege starts, by how many are running and how long none has.</summary>
    public static double SiegeStartChancePerMinute(int active, double idleMinutes) => active switch
    {
        0 => Math.Min(1, 0.25 + 0.25 * Math.Max(0, idleMinutes)),
        1 => 0.04,
        2 => 0.012,
        _ => 0,
    };

    // Called under Sync. One roll a minute server-wide; a won roll waits for the next siege-capable force.
    private static bool SiegeStartAllowed(long nowTick)
    {
        int active = Events.Values.Count(entry => !entry.DefenseReaction);
        if (active > 0) _lastSiegeActiveTick = nowTick;
        if (_siegeStartReady) return true;
        if (nowTick < _nextSiegeRoll) return false;
        _nextSiegeRoll = nowTick + 60_000;
        double idleMinutes = active == 0 ? (nowTick - _lastSiegeActiveTick) / 60_000d : 0;
        _siegeStartReady = Random.Shared.NextDouble() < SiegeStartChancePerMinute(active, idleMinutes);
        return _siegeStartReady;
    }

    public static Plan ChooseOrJoin(Force force, IReadOnlyCollection<LiveObjective> objectives, long nowTick, double roll,
        PatrolPosition? where = null)
    {
        lock (Sync)
        {
            // Defensive boundary: protected hubs cannot become new events,
            // reinforcements or reserve patrols, even if a caller supplies one.
            bool patrolOver = SiegeScheduling && force != null && AutonomousRvrPatrolLock.Expired(force.GroupId, nowTick);
            Plan plan = ChooseCore(force, objectives?.Where(objective => !objective.IsPortalKeep).ToArray(), nowTick, roll, where);
            // Anything but a patrol (a defense, an assault, a hunt, nothing) interrupts it: the held point is forgotten.
            if (force?.GroupId != null && (plan == null || plan.Intent != Intent.Roam))
                PatrolHeld.Remove(force.GroupId);
            // Owner 2026-11-01: after its thirty minutes of patrol the force chose; an event ends the lock, a patrol renews it.
            if (patrolOver && plan != null)
                AutonomousRvrPatrolLock.ChoiceMade(force.GroupId, nowTick, plan.IsSharedEvent);
            // Owner 2026-11-01: a defense interrupts the patrol; the lock is released and a new thirty-minute patrol starts once
            // the defense is over.
            else if (SiegeScheduling &&
                     (plan?.Intent == Intent.DefendEvent ||
                      plan is { IsSharedEvent: true, Intent: Intent.AssaultKeep or Intent.AssaultRelicKeep }) &&
                     AutonomousRvrPatrolLock.Holds(force.GroupId, nowTick))
                AutonomousRvrPatrolLock.ChoiceMade(force.GroupId, nowTick, sharedEvent: true);
            if (force != null)
            {
                if (plan?.IsSharedEvent == true && force.MemberIds != null && Events.TryGetValue(plan.TargetId, out var joined))
                    foreach (long id in force.MemberIds)
                        joined.Travel.TryAdd(id, (force.GroupId, nowTick, double.PositiveInfinity, default, 0));
                // A warband occupies exactly one event reservation, including
                // when switching from the siege to a moving relic carrier.
                foreach (ActiveEvent active in Events.Values.Where(active => active.TargetId != plan?.TargetId || plan?.IsSharedEvent != true))
                {
                    active.Attackers.Remove(force.GroupId);
                    active.Defenders.Remove(force.GroupId);
                    active.ThirdRealm.Remove(force.GroupId);
                    active.Slots.Remove(force.GroupId);
                }
                foreach (var entry in CarrierEvents.Where(entry => entry.Key != plan?.TargetId || plan?.IsSharedEvent != true))
                    foreach (var realm in entry.Value.Participants.Values) realm.Remove(force.GroupId);
            }
            return plan;
        }
    }

    /// <summary>
    /// Owner 2026-11-01: joining the defense of an assault on one of the realm's own keeps. Shared by the rally choice and by a
    /// force still on patrol, which may break off to defend but not to attack.
    /// </summary>
    private static Plan JoinDefense(Force force, ActiveEvent active, LiveObjective target, long nowTick, double roll)
    {
        if (force?.Realm != active.DefenderRealm || target == null || force.AverageLevel < 50) return null;
        bool canRespond = SiegeScheduling
            ? RealmEventPolicy.CanRespond(true, active.AttackObserved, active.BattleStarted, active.ThirdRealmIntervenes,
                nowTick - active.BattleStartedTick)
            : RealmEventPolicy.CanRecruitRealm(true, active.AttackObserved || active.BattleStarted,
                active.Attackers.Values.Sum(), active.Defenders.Values.Sum(), Capacity(active));
        if (!canRespond) return null;
        if (active.BattleStarted && !active.Defenders.ContainsKey(force.GroupId) &&
            !ShouldJoinActiveEvent(force, active.Defenders.Values.Sum(), Capacity(active), active.RelicKeep, true, roll))
            return null;
        if (!TryJoin(active.Defenders, force, DefenderCapFor(active))) return null;
        if (SiegeScheduling && !active.DefenseMustering) BeginDefenseMuster(active, nowTick);
        return ToPlan(Intent.DefendEvent, target, true,
            "Choosing to converge to defend the live opposing-realm assault; participation is capped.");
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> LastMenuDraw = new();

    /// <summary>Where a caller stands, so a patrol point can be recognised as reached.</summary>
    public readonly record struct PatrolPosition(ushort RegionId, int X, int Y);

    /// <summary>
    /// Owner 2026-11-03: "tenere un punto di pattuglia finché il gruppo non lo raggiunge oppure finché non sono passati 5 minuti
    /// oppure finché la pattuglia non viene interrotta". The menu is drawn at every review (about a minute) and a patrol used to
    /// pick a new random point each time: the log of 11:24-11:29 showed parties changing point every minute and never arriving.
    /// </summary>
    public const long PatrolHoldMilliseconds = 5 * 60_000;
    /// <summary>The leader's arrival radius (the controller's CampArrivalRadius): only the living leader of a party plans, so it is
    /// the leader's position that is passed in.</summary>
    public const int PatrolReachRadius = 1_250;
    private static readonly Dictionary<string, (LiveObjective Target, long Since)> PatrolHeld = new();

    /// <summary>
    /// Owner 2026-11-03 (diagnostics only): what the leader's random menu gave a force, written when it differs from the last
    /// draw of that force. Run of 2026-11-03 06:15: of six Midgard parties free at the same time only two joined an assault and
    /// the log could not say whether the others drew "patrol" or were refused.
    /// </summary>
    private static void LogMenuDraw(Force force, bool patrolLocked, List<Choice> tried, Plan plan)
    {
        // A bot on its own ("rvr-<id>") draws a menu of its own at every review; only real parties are worth a line.
        if (force.GroupId.StartsWith("rvr-", StringComparison.Ordinal)) return;
        try
        {
            string drawn = string.Join(">", tried);
            string outcome = plan == null ? "none" : $"{plan.Intent}:{plan.TargetId}";
            string key = $"{patrolLocked}|{drawn}|{outcome}";
            if (LastMenuDraw.TryGetValue(force.GroupId, out string last) && last == key) return;
            LastMenuDraw[force.GroupId] = key;
            var log = DOL.Logging.LoggerManager.Create(typeof(AutonomousRvrEventLayer));
            if (log.IsInfoEnabled)
            {
                // The menu is shuffled and tried entry by entry until one can be granted: the entries before the last one
                // found nothing to do (no running event to join, no relic carrier...), they were not chosen.
                string granted = plan == null || tried.Count == 0 ? "none" : tried[^1].ToString();
                string refused = tried.Count > (plan == null ? 0 : 1) ? string.Join(">", tried.Take(plan == null ? tried.Count : tried.Count - 1)) : "none";
                log.Info($"RVR_MENU_DRAW force={force.GroupId} realm={force.Realm} members={force.MemberCount} locked={patrolLocked} " +
                         $"granted={granted} refusedBefore={refused} plan={outcome} events={Events.Values.Count(entry => !entry.DefenseReaction)}");
            }
        }
        catch { }
    }

    private static Plan ChooseCore(Force force, IReadOnlyCollection<LiveObjective> objectives, long nowTick, double roll,
        PatrolPosition? where = null)
    {
        if (force == null || string.IsNullOrWhiteSpace(force.GroupId) || force.Realm == eRealm.None || objectives == null)
            return null;

        lock (Sync)
        {
            Cleanup(nowTick, objectives);
            if (force.AverageLevel < 50 || force.MinimumMemberLevel < 50)
                return ReservePlan(force, objectives.Where(o => !o.IsRelicCarrier && o.Kind is not Intent.AssaultKeep and not Intent.AssaultRelicKeep).ToArray(),
                    nowTick, where);
            if (ReleasedForces.ContainsKey(force.GroupId)) return null;
            // Owner 2026-11-03: a group wiped out in a siege patrols; it does not take another place in an event for a while.
            if (WipedWithdrawn.TryGetValue(force.GroupId, out long wipedUntil) && wipedUntil > nowTick) return null;

            Plan committed = CommittedPlan(force, objectives);
            if (committed != null)
                return committed;

            // Owner 2026-11-01: a group begins with thirty minutes of patrol and patrols at least thirty between one objective and
            // the next; the patrol may be broken to defend but never to attack, so an assault or a hunt waits for it to run out.
            // A player's attack or a forced siege (Forced, DefenseReaction) is not held back.
            bool patrolLocked = SiegeScheduling && !Events.Values.Any(entry => entry.Forced || entry.DefenseReaction) &&
                                AutonomousRvrPatrolLock.Holds(force.GroupId, nowTick);

            if (force.RoamingReserve)
                return ReservePlan(force, objectives, nowTick, where);

            // Owner 2026-11-02: the coordinator offers the leader this menu - join an assault, join a defense, join the
            // relic, patrol, open an assault - and the leader picks one at RANDOM among those available ("il capo sceglie
            // liberamente (che immagino significhi casualmente)"). The attempts are therefore shuffled instead of
            // priority-ordered: the first entry that can be granted wins. While the thirty-minute patrol holds, the menu
            // is reduced to defense, patrol and - owner 2026-11-03 - joining an assault that has not yet got its minimum
            // number of attackers (NeedsMinimumArmy): the patrol may be broken to defend or to bring an assault to its minimum.
            List<Choice> menu = patrolLocked
                ? [Choice.Defense, Choice.Assault, Choice.Patrol]
                : [Choice.Assault, Choice.Defense, Choice.Relic, Choice.OpenAssault, Choice.Patrol];
            List<Choice> tried = [];
            foreach (int attempt in Enumerable.Range(0, menu.Count).OrderBy(_ => Random.Shared.Next()).ToArray())
            {
                tried.Add(menu[attempt]);
                Plan granted = menu[attempt] switch
                {
                    Choice.Assault or Choice.Defense => JoinRunningEvent(),
                    Choice.Relic => JoinRelic(),
                    Choice.OpenAssault => OpenAssault(),
                    _ => ReservePlan(force, objectives, nowTick, where),
                };
                if (granted != null)
                {
                    LogMenuDraw(force, patrolLocked, tried, granted);
                    return granted;
                }
            }
            LogMenuDraw(force, patrolLocked, tried, null);
            return null;

            Plan JoinRelic()
            {
                LiveObjective carrier = objectives.FirstOrDefault(objective =>
                    objective.IsRelicCarrier && !OnCooldown(objective.Id, nowTick));
                if (carrier == null) return null;
                return TryJoinCarrier(carrier, force, nowTick, roll)
                    ? ToPlan(force.Realm == carrier.OwningRealm ? Intent.DefendEvent : Intent.HuntEnemy, carrier, true,
                        force.Realm == carrier.OwningRealm
                            ? "Escorting the live allied relic carrier with capped support."
                            : "Contesting the live enemy relic carrier with capped three-realm participation.")
                    : null;
            }

            Plan JoinRunningEvent()
            {
            // Any running event this realm may join: assault as attacker, defense as defender, contest as third realm.
            // Owner 2026-11-02: a player-forced siege keeps its precedence, the others are shuffled because the leader
            // picks one menu entry at random instead of following a fixed order.
            foreach (ActiveEvent active in Events.Values
                         .Where(active => !active.DefenseReaction)
                         .OrderByDescending(active => active.Forced)
                         .ThenBy(_ => Random.Shared.Next()))
            {
                LiveObjective target = objectives.FirstOrDefault(objective => objective.Id == active.TargetId);
                if (target == null)
                    continue;
                if (Avoided.TryGetValue((force.GroupId, active.TargetId), out long avoidUntil) && avoidUntil > nowTick) continue;
                if (!IsLatestDefenseFocus(active)) continue;
                if (target.UnderAttack) active.AttackObserved = true;
                // The attacking commitment raises the alarm before combat. Defenders
                // form next; the third realm joins after both primary forces exist.
                if (force.Realm != active.AttackerRealm && !(SiegeScheduling
                        ? RealmEventPolicy.CanRespond(force.Realm == active.DefenderRealm, active.AttackObserved,
                            active.BattleStarted, active.ThirdRealmIntervenes, nowTick - active.BattleStartedTick)
                        : RealmEventPolicy.CanRecruitRealm(force.Realm == active.DefenderRealm, active.AttackObserved || active.BattleStarted,
                            active.Attackers.Values.Sum(), active.Defenders.Values.Sum(), Capacity(active))))
                    continue;
                if (force.Realm == active.AttackerRealm)
                {
                    // Owner 2026-11-02: an assault is never assigned to a force still inside its thirty-minute patrol
                    // window: it would be counted at the rally and never walk there, which is what kept the night musters at
                    // 0-14 against 64 needed. A player-forced siege and a defense still interrupt the patrol.
                    // Owner 2026-11-03: "la pattuglia può essere interrotta per difendere o per raggiungere il numero minimo di
                    // attaccanti per far partire un assalto".
                    bool patrolFree = !SiegeScheduling || active.Forced || active.DefenseReaction ||
                        !AutonomousRvrPatrolLock.Holds(force.GroupId, nowTick) || NeedsMinimumArmy(active);
                    if (patrolFree && force.AverageLevel >= 50 && (force.MemberCount == 1 || force.MemberCount >= AutonomousRvrCoalition.MinimumGroupMembers) &&
                        (!active.BattleStarted || active.Attackers.ContainsKey(force.GroupId) ||
                        ShouldJoinActiveEvent(force, active.Attackers.Values.Sum(), Capacity(active), active.RelicKeep, false, roll)) &&
                        TryJoin(active.Attackers, force, Capacity(active)))
                        return ToPlan(Intent: active.RelicKeep ? Intent.AssaultRelicKeep : Intent.AssaultKeep, target, true,
                            $"Choosing to rally to the active {(active.RelicKeep ? "relic-keep" : "keep")} assault; participation is capped.");
                    continue;
                }
                if (force.Realm == active.DefenderRealm)
                {
                    Plan defense = JoinDefense(force, active, target, nowTick, roll);
                    if (defense != null) return defense;
                    continue;
                }
                // The third realm contests the same event under its own cap;
                // it must never overwrite the first attacker's registration.
                if (force.AverageLevel >= 50 && (!active.BattleStarted || active.ThirdRealm.ContainsKey(force.GroupId) ||
                    ShouldJoinActiveEvent(force, active.ThirdRealm.Values.Sum(), Capacity(active), active.RelicKeep, false, roll)) &&
                    TryJoin(active.ThirdRealm, force, SiegeScheduling ? RealmEventPolicy.ThirdRealmCap(active.RelicKeep) : Capacity(active)))
                    return ToPlan(active.RelicKeep ? Intent.AssaultRelicKeep : Intent.AssaultKeep,
                        target, true, "Third-realm force contesting the siege under its own participation cap.");
            }
            return null;
            }

            Plan OpenAssault()
            {
            // Owner 2026-11-02: "più assalti insieme, metti il numero in una costante facilmente modificabile, per ora 1".
            // A realm opens no more than MaximumAssaultsPerRealm assaults at once; a running carrier also keeps its
            // forces in patrol instead of opening a second empty rally.
            if (CarrierEvents.Count != 0 || Events.Values.Count(active => !active.DefenseReaction &&
                    (!SiegeScheduling || active.AttackerRealm == force.Realm)) >= MaximumAssaultsPerRealm)
                return null;

            bool siegeCapable = MayOpenAssault(force);
            // Owner 2026-10-31 (step 4 of the RvR plan): no more roll to open a siege. A capable force first retakes a keep of its own
            // frontier that another realm holds; with all of them held, and forty minutes after the end of the realm's last conquest attempt, it
            // goes to conquer a keep in one of the other two frontiers (AutonomousRvrCampaign).
            LiveObjective assault = null;
            bool conquest = false;
            if (SiegeScheduling && siegeCapable)
            {
                LiveObjective[] assaultable = objectives.Where(objective => objective.Kind is Intent.AssaultKeep or Intent.AssaultRelicKeep &&
                    objective.OwningRealm != force.Realm && objective.OwningRealm != eRealm.None && !Events.ContainsKey(objective.Id) &&
                    !OnCooldown(objective.Id, nowTick) && (!objective.IsRelicKeep || force.MemberCount >= 6) &&
                    !(Avoided.TryGetValue((force.GroupId, objective.Id), out long avoidedUntil) && avoidedUntil > nowTick)).ToArray();
                ushort home = AutonomousRvrCampaign.HomeRegion(force.Realm);
                bool homeLost = objectives.Any(objective => objective.RegionId == home && objective.Kind is Intent.AssaultKeep or Intent.AssaultRelicKeep &&
                    objective.OwningRealm != force.Realm && objective.OwningRealm != eRealm.None);
                assault = AutonomousRvrCampaign.SelectAssault(force.Realm, assaultable, homeLost,
                    AutonomousRvrCampaign.LastConquestTick(force.Realm), nowTick,
                    region => AutonomousRvrCampaign.EntryKeep(force.Realm, region), Random.Shared, out conquest);
            }
            else if (!SiegeScheduling)
            {
                // Legacy scheduling: an assault is a plan on a keep without a realm-wide rally. The choice is still the
                // coordinator's and still random among the attackable keeps, so the menu behaves the same with or
                // without the event system.
                LiveObjective[] attackable = objectives.Where(objective =>
                        objective.Kind is Intent.AssaultKeep or Intent.AssaultRelicKeep &&
                        objective.OwningRealm != force.Realm && objective.OwningRealm != eRealm.None &&
                        !Events.ContainsKey(objective.Id) && !OnCooldown(objective.Id, nowTick) &&
                        (!objective.IsRelicKeep || force.MemberCount >= 6)).ToArray();
                if (attackable.Length != 0)
                    assault = attackable[Random.Shared.Next(attackable.Length)];
            }
            // Owner 2026-11-02: this menu entry opens a keep assault and nothing else. Hunting an opposing force is no
            // longer a choice of its own - the group only meets enemies inside an event - so with no keep to attack the
            // entry is simply unavailable and another menu entry is tried.
            if (assault == null)
                return null;
            Intent intent = assault.IsRelicKeep ? Intent.AssaultRelicKeep : Intent.AssaultKeep;
            LiveObjective selected = assault;
            if (selected == null)
                return null;

            if (intent is Intent.AssaultKeep or Intent.AssaultRelicKeep)
            {
                ActiveEvent active = new()
                {
                    TargetId = selected.Id,
                    Target = selected,
                    AttackerRealm = force.Realm,
                    DefenderRealm = selected.OwningRealm,
                    RelicKeep = selected.IsRelicKeep,
                    Conquest = conquest,
                    ExpiresTick = nowTick + RealmEventPolicy.RecruitmentMilliseconds(Random.Shared.NextDouble()),
                    CreatedTick = nowTick,
                };
                active.Attackers[force.GroupId] = force.MemberCount;
                Events[selected.Id] = active;
                _siegeStartReady = false;
                RealmEventRecords.Begin(selected.Id, selected.Name, selected.IsRelicKeep ? "Relic keep" : "Keep", GlobalConstants.RealmToName(force.Realm), "Automatic continuous siege; defender: " + GlobalConstants.RealmToName(selected.OwningRealm));
                if (!StagedAssault)
                    StartBattle(active, nowTick, "automatic assault opened; each assigned bot converges without formation staging");
                else
                {
                    BeginMuster(active, nowTick);
                    RealmEventRecords.Progress(selected.Id, "Rally", "Automatic siege: attackers muster at their border keep, then march to the siege camp", force.MemberCount, 0);
                }
                RealmEventNotices.Queue(active.TargetId, force.Realm, StagedAssault
                    ? $"Warbands are gathering to assault {Place(selected.Name)}. Join them at the siege camp!"
                    : $"Warbands are marching on {Place(selected.Name)}. Reinforcements join the battle on arrival.");
                (selected.IsRelicKeep ? SelectedRelics : SelectedKeeps).Add(selected.Id);
                return ToPlan(intent, selected, true,
                    $"Opening a time-limited {(selected.IsRelicKeep ? "relic-keep" : "keep")} assault; same-realm warbands may rally and defenders may answer.");
            }
            return null;
            }
        }
    }

    private static Plan ToPlan(Intent Intent, LiveObjective target, bool shared, string reason) =>
        new(Intent, target.Id, target.Name, target.RegionId, target.X, target.Y, target.Z, shared, reason);

    /// <summary>
    /// Owner 2026-10-31: a patrol (Roam) picks its random place among the zones that hold the portal keeps
    /// (AutonomousRvrRoamZones). A hunt is not limited to them: an enemy force is hunted wherever it is in the frontier, and a
    /// relic carrier is followed wherever it goes.
    /// </summary>
    private static bool RoamsHere(LiveObjective objective) =>
        objective.IsRelicCarrier || AutonomousRvrRoamZones.IsRoamPoint(objective.RegionId, objective.X, objective.Y);

    /// <summary>
    /// Owner 2026-10-31 (step 3 of the RvR plan): a force patrols the zone it was given (Emain Macha, Odin's Gate or Hadrian's Wall,
    /// shared equally by the forces of its realm, AutonomousRvrRoamZones.PatrolRegionFor). With nothing to patrol in it, any of the
    /// three zones will do.
    /// </summary>
    private static LiveObjective PickPatrol(Force force, IEnumerable<LiveObjective> objectives)
    {
        LiveObjective[] patrols = objectives.Where(objective => objective.Kind == Intent.Roam).ToArray();
        long now = GameLoop.GameLoopTime;
        return patrols.Where(objective => AutonomousRvrRoamZones.IsPatrolPoint(force.GroupId, force.Realm, now,
                objective.RegionId, objective.X, objective.Y)).OrderBy(_ => Random.Shared.Next()).FirstOrDefault()
            ?? patrols.Where(RoamsHere).OrderBy(_ => Random.Shared.Next()).FirstOrDefault();
    }

    private static Plan ReservePlan(Force force, IReadOnlyCollection<LiveObjective> objectives, long nowTick,
        PatrolPosition? where)
    {
        // Owner 2026-11-03: the patrol point is held until the force reaches it, five minutes pass, or the patrol is
        // interrupted (ChooseOrJoin forgets the point when the plan is anything but a patrol). Called under Sync.
        // With no patrol or hunt on offer at all the menu entry gives nothing, as before, whatever point was held.
        LiveObjective fresh = PickPatrol(force, objectives)
            ?? objectives.Where(objective => objective.Kind == Intent.HuntEnemy && !objective.IsRelicCarrier && objective.EnemyCount > 0)
                .OrderBy(_ => Random.Shared.Next()).FirstOrDefault();
        if (fresh == null)
        {
            PatrolHeld.Remove(force.GroupId);
            return null;
        }
        LiveObjective target = null;
        long since = nowTick;
        string why = "first"; // first point, or the previous one was forgotten by an interruption
        string previous = "none";
        long heldSeconds = 0;
        long distance = -1;
        if (PatrolHeld.TryGetValue(force.GroupId, out var held))
        {
            previous = held.Target.Id;
            heldSeconds = Math.Max(0, nowTick - held.Since) / 1000;
            if (where is { } at && at.RegionId == held.Target.RegionId)
                distance = (long)Math.Sqrt((double)(at.X - held.Target.X) * (at.X - held.Target.X) +
                                           (double)(at.Y - held.Target.Y) * (at.Y - held.Target.Y));
            bool reached = distance >= 0 && distance <= PatrolReachRadius;
            if (!reached && nowTick >= held.Since && nowTick - held.Since < PatrolHoldMilliseconds)
            {
                target = held.Target;
                since = held.Since;
                why = "kept";
            }
            else
                why = reached ? "reached" : "expired";
        }
        target ??= fresh;
        // Owner 2026-11-03 (diagnostics only): one line each time a party's patrol point changes, with why. "kept" is not
        // written (it would be one line per review per party). A bot on its own ("rvr-<id>") is not worth a line.
        if (why != "kept" && !force.GroupId.StartsWith("rvr-", StringComparison.Ordinal))
        {
            try
            {
                var log = DOL.Logging.LoggerManager.Create(typeof(AutonomousRvrEventLayer));
                if (log.IsInfoEnabled)
                    log.Info($"RVR_PATROL_POINT force={force.GroupId} realm={force.Realm} reason={why} previous={previous} " +
                             $"heldSeconds={heldSeconds} leaderDistanceToPrevious={distance} next={target.Id} region={target.RegionId}");
            }
            catch { }
        }
        if (target.Kind == Intent.Roam)
            PatrolHeld[force.GroupId] = (target, since);
        else
            PatrolHeld.Remove(force.GroupId);
        return ToPlan(target.Kind == Intent.HuntEnemy ? Intent.HuntEnemy : Intent.Roam, target, false,
            "Keeping a deliberate roaming reserve instead of automatically joining the active event.");
    }

    /// <summary>
    /// Each realm's share of a bot siege. Run 2026-10-07: battle reinforcements filled defenders and the third realm to
    /// the attacker cap (64 each), so armies that marched with 33-34 met 54-56 defenders plus the keep's guards and every
    /// siege failed. Defenders answer the army that actually marched (DefenseRatio of it, at least 8); the third realm
    /// keeps its own cap.
    /// </summary>
    private static int SiegeRealmCap(ActiveEvent active, eRealm realm) =>
        realm == active.AttackerRealm ? Capacity(active) : realm == active.DefenderRealm ? DefenderCapFor(active) :
        SiegeScheduling ? RealmEventPolicy.ThirdRealmCap(active.RelicKeep) : Capacity(active);

    private static int DefenderCapFor(ActiveEvent active)
    {
        if (!SiegeScheduling) return Capacity(active);
        int cap = RealmEventPolicy.DefenderCap(active.RelicKeep);
        return active.MarchedAttackers <= 0 ? cap
            : Math.Clamp((int)Math.Round(active.MarchedAttackers * Math.Clamp(RealmEventPolicy.DefenseRatio, 0.25, 2.0)), 8, cap);
    }

    private static int Capacity(ActiveEvent active) => active.DefenseReaction ? 240 : active.Retake ? active.RetakeCap :
        SiegeScheduling ? RealmEventPolicy.AttackerCap(active.RelicKeep) : active.RelicKeep ? RelicAssaultCap : OrdinaryAssaultCap;

    // Battle start: the dynamic sieges use 85% of their attacker cap at the posts (half on a strike or at the
    // deadline); the legacy tests keep the 108/170 and 32/48 rules.
    private static bool Ready(ActiveEvent active, bool deadline, long nowTick) => SiegeScheduling
        ? Attendance(active, active.AttackerRealm, nowTick) >= RealmEventPolicy.DynamicStartThreshold(active.RelicKeep, deadline)
        : RealmEventPolicy.SiegeReady(active.RelicKeep, deadline, Attendance(active, active.AttackerRealm, nowTick),
            Attendance(active, active.DefenderRealm, nowTick));

    private static Plan CommittedPlan(Force force, IReadOnlyCollection<LiveObjective> objectives)
    {
        foreach (ActiveEvent active in Events.Values)
        {
            // A local route/candidate refresh is not an event cancellation.
            // Keep the committed destination while the normal route recovery
            // handles any temporary inability to reach it.
            LiveObjective target = objectives.FirstOrDefault(objective => objective.Id == active.TargetId) ?? active.Target;
            if (active.Attackers.ContainsKey(force.GroupId))
            {
                TryJoin(active.Attackers, force, Capacity(active));
                return ToPlan(active.RelicKeep ? Intent.AssaultRelicKeep : Intent.AssaultKeep, target, true,
                    "This force remains committed to the siege through defeat and regrouping.");
            }
            if (active.Defenders.ContainsKey(force.GroupId))
            {
                TryJoin(active.Defenders, force, DefenderCapFor(active));
                return ToPlan(Intent.DefendEvent, target, true,
                    "This force remains committed to defending the siege through defeat and regrouping.");
            }
            if (active.ThirdRealm.ContainsKey(force.GroupId))
            {
                TryJoin(active.ThirdRealm, force, SiegeRealmCap(active, OtherRealm(active)));
                return ToPlan(active.RelicKeep ? Intent.AssaultRelicKeep : Intent.AssaultKeep, target, true,
                    "This third-realm force remains committed to the siege through defeat and regrouping.");
            }
        }

        foreach (var entry in CarrierEvents)
        foreach (var realm in entry.Value.Participants)
        {
            if (!realm.Value.ContainsKey(force.GroupId))
                continue;
            LiveObjective target = objectives.FirstOrDefault(objective => objective.Id == entry.Key) ?? CarrierTargets.GetValueOrDefault(entry.Key);
            if (target == null)
                continue;
            TryJoin(realm.Value, force, RelicCarrierRealmCap);
            return ToPlan(force.Realm == target.OwningRealm ? Intent.DefendEvent : Intent.HuntEnemy, target, true,
                force.Realm == target.OwningRealm
                    ? "This force remains committed to the relic escort through defeat and regrouping."
                    : "This force remains committed to intercepting the relic through defeat and regrouping.");
        }
        return null;
    }

    public sealed record BattleNotice(string TargetId, string Kind, eRealm Attacker, eRealm Defender,
        int Attackers, int Defenders, int ThirdRealm, int CapPerRealm, long RemainingMilliseconds,
        bool BattleStarted = false, int PresentAttackers = 0, int PresentDefenders = 0, int PresentThirdRealm = 0,
        long MusterRemainingMilliseconds = -1, int CoalitionGroups = 0, string CoalitionLeader = null);

    public static BattleNotice[] Snapshot()
    {
        lock (Sync)
        {
            Expire(GameLoop.GameLoopTime);
            return Events.Values.Where(entry => entry.ExpiresTick > GameLoop.GameLoopTime)
                .Select(entry => new BattleNotice(entry.TargetId, entry.DefenseReaction ? "SIEGE — immediate defense response" : entry.BattleStarted ? "SIEGE — ongoing" : entry.RelicKeep ? "Relic siege rally" : "Keep siege rally",
                    entry.AttackerRealm, entry.DefenderRealm, entry.Attackers.Values.Sum(), entry.Defenders.Values.Sum(),
                    entry.ThirdRealm.Values.Sum(), Capacity(entry),
                    Math.Max(0, entry.ExpiresTick - GameLoop.GameLoopTime), entry.BattleStarted,
                    Attendance(entry, entry.AttackerRealm, GameLoop.GameLoopTime),
                    Attendance(entry, entry.DefenderRealm, GameLoop.GameLoopTime),
                    Attendance(entry, OtherRealm(entry), GameLoop.GameLoopTime),
                    // gathering: time to the march order; 0 = the army is marching; -1 = no muster stage
                    entry.BattleStarted || entry.DefenseReaction || entry.MusterDeadlineTick == 0 ? -1
                        : entry.Marching ? 0 : Math.Max(1, entry.MusterDeadlineTick - GameLoop.GameLoopTime),
                    entry.Coalition.Count, entry.Coalition.Leader)).ToArray();
        }
    }

    public sealed record RallyOrder(string TargetId, eRealm Attacker, eRealm Defender, int[] Slots, long RemainingMilliseconds,
        bool HomeMuster = false);

    // Quicker rallies (owner 2026-10-07: relic rallies took over an hour): 12 minutes to gather, one 5-minute extension.
    public const long HomeMusterMilliseconds = 12 * 60_000;
    public const long MusterExtensionMilliseconds = 5 * 60_000;
    // Relic sieges need the larger army (MarchThreshold 170). Run 9 (2026-10-14): the Dun Dagda muster marched at its
    // 12+5 minute deadline with 60 of 171 assigned, reached the keep with 12 and lost 186 bots to Hibernia. A relic
    // muster gets 25 minutes and, while it is under 120, up to six 10-minute extensions. Run 15 (2026-10-16): after two
    // extensions the Dun Dagda army marched with 98, about 60 reached the keep and the relic guards (three times a
    // keep guard's health, untouchable from range) and 44 defenders killed them in four minutes; run 13's 135 took the
    // gate to 3%.
    public const long RelicMusterMilliseconds = 25 * 60_000;
    public const long RelicMusterExtensionMilliseconds = 10 * 60_000;
    public const int RelicMusterExtensions = 6;
    // The army marches once it can start the battle on arrival (108 for a keep, 170 for a relic keep,
    // or 85% of the attackers assigned when fewer). Run 14: marching at 64 sent half an army against
    // full-strength defenders; it lost the field before the battle could start (34 present, then 8).
    public static int MarchThreshold(bool relic, int assigned) =>
        Math.Max(MinimumArmy, Math.Min(relic ? 170 : 108, assigned * 85 / 100));

    /// <summary>
    /// Owner 2026-11-01: an army never marches with fewer than this many (sixteen, two full groups; owner 2026-11-03: twenty,
    /// three groups of eight in practice). The night run sent armies of 8, 5 and
    /// even 2 against keeps with guards and a lord. A rally that has not gathered this many by its deadline fails; its forces go
    /// back to patrol. A siege forced by a player and a retake are not held to it.
    /// </summary>
    public const int MinimumArmy = AutonomousRvrCoalition.MinimumBots;

    /// <summary>
    /// Owner 2026-11-03: an assault still mustering whose assigned attackers are fewer than <see cref="MinimumArmy"/> may take a
    /// force away from its patrol; once it has them, a patrolling force is left alone.
    /// </summary>
    private static bool NeedsMinimumArmy(ActiveEvent active) =>
        !active.Marching && !active.BattleStarted && !active.DefenseReaction &&
        !AutonomousRvrCoalition.IsViable(RealGroups(active), active.Attackers.Values.Sum());

    /// <summary>The attacking groups that count as groups (four members or more); lone recruits are bots, not groups.</summary>
    private static int RealGroups(ActiveEvent active) =>
        active.Attackers.Values.Count(members => members >= AutonomousRvrCoalition.MinimumGroupMembers);

    /// <summary>Siege rework phase 2: the muster is a coalition when at least two groups and sixteen bots are at the muster point.</summary>
    private static bool MusterViable(ActiveEvent active) =>
        AutonomousRvrCoalition.IsViable(active.Mustered.Values.Select(entry => entry.Force).Distinct(StringComparer.Ordinal)
            .Count(force => active.Attackers.TryGetValue(force, out int members) && members >= AutonomousRvrCoalition.MinimumGroupMembers),
            active.Mustered.Count);

    /// <summary>Attackers at their posts that open a marched siege without the leader: 60% of the army that marched, at least 8.</summary>
    public static int ArrivalQuorum(int marchedAttackers) => Math.Max(8, marchedAttackers * 6 / 10);

    /// <summary>The march leader reached the keep: the marched army attacks at once.</summary>
    public static void ReportArmyArrived(string targetId, long nowTick)
    {
        lock (Sync)
            if (Events.TryGetValue(targetId, out var active) && !active.BattleStarted && MarchedArmy(active))
                StartBattle(active, nowTick, "the marching army reached the keep and attacks at once; defenders and third-realm reinforcements may respond");
    }

    /// <summary>An army that mustered at home and has been given the march order (the start threshold already met).</summary>
    private static bool MarchedArmy(ActiveEvent active) =>
        active.Marching && active.MusterDeadlineTick != 0 && !active.DefenseReaction;

    /// <summary>
    /// The realm that lost a keep to a player's attack makes one organised attempt to take it back: a short muster at
    /// its outpost, a march, then the strike on arrival, sized like the defense that failed. It ends when the keep is
    /// retaken or after 30 minutes of battle; it never repeats (a retake is not a player defense).
    /// </summary>
    private static void StartRetake(ActiveEvent lost, eRealm newOwner, long nowTick)
    {
        eRealm retaker = lost.DefenderRealm;
        if (retaker is not (eRealm.Albion or eRealm.Midgard or eRealm.Hibernia) || retaker == newOwner) return;
        Cooldowns.Remove(lost.TargetId);
        var target = lost.Target with { OwningRealm = newOwner };
        var retake = new ActiveEvent
        {
            TargetId = target.Id, Target = target, AttackerRealm = retaker, DefenderRealm = newOwner,
            RelicKeep = lost.RelicKeep, CreatedTick = nowTick, Retake = true,
            RetakeCap = Math.Max(64, ResponseCap(true, lost.DefenseScale)),
            ExpiresTick = nowTick + RetakeMusterMilliseconds + RealmEventPolicy.RecruitmentMilliseconds(0),
        };
        Events[target.Id] = retake;
        BeginMuster(retake, nowTick, RetakeMusterMilliseconds);
        string loser = GlobalConstants.RealmToName(retaker);
        RealmEventNotices.Queue(target.Id, retaker, $"{Place(target.Name)} has fallen! Rally at the outpost: we take it back.");
        RealmEventNotices.QueueScreen(target.Id, retaker, $"{loser} rallies to retake {Place(target.Name)}!", AnnouncementKind.RvrBattleground);
        RealmEventNotices.QueueScreen(target.Id, newOwner, $"{loser} is rallying to take back {Place(target.Name)}!", AnnouncementKind.RvrBattleground);
        RealmEventRecords.Begin(target.Id, target.Name, target.IsRelicKeep ? "Relic keep" : "Keep", loser,
            "Retake attempt after a player captured the keep; one try");
        var log = DOL.Logging.LoggerManager.Create(typeof(AutonomousRvrEventLayer));
        if (log.IsInfoEnabled) log.Info($"RVR_RETAKE_STARTED target={target.Id} keep=\"{target.Name}\" retaker={loser} " +
            $"holder={GlobalConstants.RealmToName(newOwner)} cap={retake.RetakeCap} musterMinutes={RetakeMusterMilliseconds / 60_000}");
    }

    private static void BeginMuster(ActiveEvent active, long nowTick, long milliseconds = HomeMusterMilliseconds)
    {
        if (active.RelicKeep && !active.Retake && milliseconds == HomeMusterMilliseconds) milliseconds = RelicMusterMilliseconds;
        active.Marching = false;
        active.MusterDeadlineTick = nowTick + milliseconds;
        // The forward rally keeps its full hour after the march order.
        active.ExpiresTick = Math.Max(active.ExpiresTick, nowTick + Math.Max(milliseconds, HomeMusterMilliseconds) + RealmEventPolicy.RecruitmentMilliseconds(0));
    }

    // The defense gathers as one quick rally (nearest friendly keep) and goes in together: it marches at
    // 85% of its cap or after six minutes. Trickling defenders were picked off one at a time.
    public const long DefenseMusterMilliseconds = 6 * 60_000;
    public const long DefenseMusterExtensionMilliseconds = 4 * 60_000;
    /// <summary>A player's attack: the defenders gather (80% of the force, at most ten minutes), then march in together.</summary>
    public const long PlayerDefenseMusterMilliseconds = 10 * 60_000;
    /// <summary>The losing realm musters (80% of its army, at most ten minutes) for its one retake attempt, then fights for 30.</summary>
    public const long RetakeMusterMilliseconds = 10 * 60_000;
    public const long RetakeBattleMilliseconds = 30 * 60_000;

    /// <summary>
    /// How many gathered defenders a player-triggered defense (and the retake that follows a lost keep) waits for
    /// before it marches (owner 2026-10-07: a huge force matters more than speed): 80% of the force, or the
    /// 10-minute muster runs out, whichever comes first. The third realm never musters (it stays chaotic).
    /// </summary>
    public static int DefenseWaveSize(int called) => Math.Max(Math.Min(called, 6), called * 80 / 100);

    private static void BeginDefenseMuster(ActiveEvent active, long nowTick, long milliseconds = DefenseMusterMilliseconds)
    {
        active.DefenseMustering = true;
        active.DefenseMarching = false;
        active.DefenseMusterDeadlineTick = nowTick + milliseconds;
        active.DefenseMusterStartTick = nowTick;
        var log = DOL.Logging.LoggerManager.Create(typeof(AutonomousRvrEventLayer));
        if (log.IsInfoEnabled) log.Info($"RVR_DEFENSE_MUSTER target={active.TargetId} defenders={GlobalConstants.RealmToName(active.DefenderRealm)} " +
            $"cap={(active.DefenseReaction ? ResponseCap(true, active.DefenseScale) : DefenderCapFor(active))} minutes={milliseconds / 60_000}");
    }

    private static int DefenseMusterAttendance(ActiveEvent active, long nowTick) => active.DefenseMustered.Values
        .Count(entry => nowTick - entry.Tick <= AttendanceFreshnessMilliseconds && entry.Bot?.IsAlive == true &&
            entry.Bot.ObjectState == GameObject.eObjectState.Active && active.Defenders.ContainsKey(entry.Force));

    private static void GiveDefenseMarch(ActiveEvent active, long nowTick, string reason)
    {
        if (active.DefenseMarching) return;
        active.DefenseMarching = true;
        RealmEventNotices.Queue(active.TargetId, active.DefenderRealm, $"The defense rally marches to relieve {Place(active.Target.Name)}!");
        var log = DOL.Logging.LoggerManager.Create(typeof(AutonomousRvrEventLayer));
        if (log.IsInfoEnabled) log.Info($"RVR_DEFENSE_MARCH target={active.TargetId} mustered={DefenseMusterAttendance(active, nowTick)} reason=\"{reason}\"");
    }

    private static int MusterAttendance(ActiveEvent active, long nowTick) => active.Mustered.Values
        .Count(entry => nowTick - entry.Tick <= AttendanceFreshnessMilliseconds && entry.Bot?.IsAlive == true &&
            entry.Bot.ObjectState == GameObject.eObjectState.Active && active.Attackers.ContainsKey(entry.Force));

    /// <summary>The organizing group's leader stands at the muster (owner 2026-11-03); the deadline marches without it.</summary>
    private static bool OrganizerHasArrived(ActiveEvent active)
    {
        SyncCoalition(active);
        return AutonomousRvrCoalition.OrganizerArrived(active.Coalition.Leader,
            active.Mustered.Values.Select(entry => (entry.Force, entry.Bot?.Group?.LivingLeader == entry.Bot)));
    }

    private static void GiveMarchOrder(ActiveEvent active, long nowTick, string reason)
    {
        if (active.Marching) return;
        active.Marching = true;
        active.MarchedAttackers = MusterAttendance(active, nowTick);
        // A relic keep lies deep in the enemy realm: the Dun Dagda army (run 4) needed well over an hour from its
        // Emain Macha muster through the defenders on the way and failed at the one-hour mark.
        active.ExpiresTick = Math.Max(active.ExpiresTick, nowTick + (active.RelicKeep ? RelicMarchMilliseconds
            : RealmEventPolicy.RecruitmentMilliseconds(0)));
        RealmEventNotices.Queue(active.TargetId, active.AttackerRealm, $"The army marches on {Place(active.Target.Name)}! Form up at the siege camp.");
        var log = DOL.Logging.LoggerManager.Create(typeof(AutonomousRvrEventLayer));
        if (log.IsInfoEnabled) log.Info($"RVR_SIEGE_MARCH target={active.TargetId} mustered={MusterAttendance(active, nowTick)} reason=\"{reason}\"");
    }

    /// <summary>
    /// The live leader of a marching army (the mustered attacker with the lowest id; the next one takes over if it
    /// falls). Null before the march, once the battle has started, or for an event without a muster.
    /// </summary>
    /// <summary>
    /// A siege objective's name as players read it in announcements. Objectives are labelled "keep Caer Sursbrooke" /
    /// "relic keep Dun Dagda" (bot goal lists), and the announcements added "the keep" again: "Our army has laid siege to
    /// the keep keep Caer Sursbrooke!" (owner report 2026-10-16).
    /// </summary>
    public static string Place(string objectiveName)
    {
        if (string.IsNullOrEmpty(objectiveName)) return objectiveName;
        foreach (string prefix in new[] { "relic keep ", "keep " })
            if (objectiveName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return objectiveName.Substring(prefix.Length);
        return objectiveName;
    }

    public static GameBot MarchLeader(string targetId)
    {
        lock (Sync)
        {
            if (!Events.TryGetValue(targetId, out var active) || active.BattleStarted || !active.Marching || active.Mustered.Count == 0)
                return null;
            long now = GameLoop.GameLoopTime;
            if (IsLiveArmyMember(active, active.MarchLeader))
            {
                // Run 2026-10-13: the relic army for Castle Excalibur waited 45 minutes at Hadrian's Wall
                // ("Waiting with the army") behind a leader whose group sat at its rendezvous and never led.
                // A leader in a fight is leading too: the column holds or joins it (run 6: armies fought all the way
                // and their leaders were replaced every minute).
                // Owner 2026-11-03: the organizer's leader gets two minutes before it is replaced - it often reaches the
                // muster, but is still crossing or forming up when the army is told to march (seen twice on 2026-11-03).
                long silence = MarchLeaderSilenceLimit(IsOrganizerLeader(active, active.MarchLeader));
                if (now - Math.Max(active.MarchLeaderSinceTick, active.MarchLeaderHeartbeatTick) < silence ||
                    active.MarchLeader.InCombatInLast((int)silence))
                    return active.MarchLeader;
                active.StaleLeaders[active.MarchLeader.DatabaseID] = now + MarchLeaderStaleMilliseconds;
                var log = DOL.Logging.LoggerManager.Create(typeof(AutonomousRvrEventLayer));
                if (log.IsInfoEnabled) log.Info($"RVR_MARCH_LEADER_REPLACED target={targetId} leader={active.MarchLeader.Name} " +
                    $"reason=\"no leading turn for {silence / 1000} seconds\"");
            }
            foreach (long id in active.StaleLeaders.Where(pair => pair.Value <= now).Select(pair => pair.Key).ToArray())
                active.StaleLeaders.Remove(id);
            // Run 5: most of the Castle Excalibur army sat at its parties' rendezvous, and picking leaders by id
            // replaced one every 30 seconds for an hour. Prefer a marcher that is actually taking march turns.
            GameBot[] candidates = active.Mustered.Values.Select(entry => entry.Bot)
                .Where(bot => IsLiveArmyMember(active, bot) && !active.StaleLeaders.ContainsKey(bot.DatabaseID)).ToArray();
            // Run 28 (2026-10-20): the next leader was the marcher with the lowest id, wherever it stood. A straggler 64,000
            // units behind the head of Dun Dagda's column became leader, the front walked back to it, and the siege opened
            // with 62 of 170 at the keep. The leader is now the marcher with most of the army around it (the main body),
            // then the one nearest the target.
            GameBot[] turning = candidates
                .Where(bot => active.MarchTurns.TryGetValue(bot.DatabaseID, out long turn) && now - turn < MarchLeaderSilenceMilliseconds)
                .ToArray();
            // Siege rework phase 3: the march is led by the leader of the coalition's organizing group (else of the next
            // group in joining order); the main-body election only decides when no group leader can lead.
            long? coalitionLeader = AutonomousRvrCoalition.PickMarchLeader(active.Coalition.Groups,
                active.Mustered.Values.Where(entry => candidates.Contains(entry.Bot))
                    .Select(entry => (entry.Force, entry.Bot.DatabaseID, entry.Bot.Group?.LivingLeader == entry.Bot)));
            active.MarchLeader = coalitionLeader is long leaderId
                ? candidates.First(bot => bot.DatabaseID == leaderId)
                : MainBodyLeader(turning.Length > 0 ? turning : candidates, active.Target);
            active.MarchLeaderSinceTick = now;
            return active.MarchLeader;
        }
    }

    /// <summary>The marcher with the most of the column within <see cref="MainBodyRadius"/>, then the one nearest the target.</summary>
    public static GameBot MainBodyLeader(GameBot[] marchers, LiveObjective target)
    {
        GameBot best = null;
        int bestNear = -1;
        long bestDistance = long.MaxValue;
        foreach (GameBot bot in marchers)
        {
            int near = 0;
            foreach (GameBot other in marchers)
                if (other != bot && other.CurrentRegionID == bot.CurrentRegionID && bot.IsWithinRadius(other, MainBodyRadius))
                    near++;
            long dx = bot.X - (target?.X ?? bot.X), dy = bot.Y - (target?.Y ?? bot.Y);
            long distance = bot.CurrentRegionID == (target?.RegionId ?? bot.CurrentRegionID) ? dx * dx + dy * dy : long.MaxValue / 2;
            if (near > bestNear || near == bestNear && (distance < bestDistance || distance == bestDistance && bot.DatabaseID < best.DatabaseID))
            {
                best = bot;
                bestNear = near;
                bestDistance = distance;
            }
        }
        return best;
    }

    public const int MainBodyRadius = 2_500;

    public const long MarchLeaderSilenceMilliseconds = 30_000;
    /// <summary>How long the organizing group's leader may go without a leading turn before the army replaces it.</summary>
    public const long OrganizerSilenceMilliseconds = 120_000;

    public static long MarchLeaderSilenceLimit(bool organizerLeader) =>
        organizerLeader ? OrganizerSilenceMilliseconds : MarchLeaderSilenceMilliseconds;

    private static bool IsOrganizerLeader(ActiveEvent active, GameBot bot) =>
        bot != null && active.Coalition.Leader is string organizer && bot.Group?.LivingLeader == bot &&
        active.Mustered.TryGetValue(bot.DatabaseID, out var entry) && string.Equals(entry.Force, organizer, StringComparison.Ordinal);
    public const long MarchLeaderStaleMilliseconds = 10 * 60_000;

    /// <summary>A marcher took a march turn; for the leader that is a leading turn (walking, regrouping or holding).</summary>
    public static void ReportMarchLeading(string targetId, GameBot bot)
    {
        lock (Sync)
            if (Events.TryGetValue(targetId, out var active) && bot != null)
            {
                active.MarchTurns[bot.DatabaseID] = GameLoop.GameLoopTime;
                if (active.MarchLeader == bot) active.MarchLeaderHeartbeatTick = GameLoop.GameLoopTime;
            }
    }

    /// <summary>True when this marcher took a march turn within the silence limit (it is walking, holding or fighting with the army).</summary>
    public static bool IsMarching(string targetId, GameBot bot)
    {
        lock (Sync)
            return bot != null && Events.TryGetValue(targetId, out var active) &&
                   active.MarchTurns.TryGetValue(bot.DatabaseID, out long turn) &&
                   GameLoop.GameLoopTime - turn < MarchLeaderSilenceMilliseconds;
    }

    /// <summary>The mustered attackers still alive and in the world (the marching column).</summary>
    public static GameBot[] ArmyMembers(string targetId)
    {
        lock (Sync)
            return Events.TryGetValue(targetId, out var active) && !active.BattleStarted
                ? active.Mustered.Values.Select(entry => entry.Bot).Where(bot => IsLiveArmyMember(active, bot)).Distinct().ToArray()
                : [];
    }

    private static bool IsLiveArmyMember(ActiveEvent active, GameBot bot) =>
        bot != null && bot.IsAlive && bot.ObjectState == GameObject.eObjectState.Active &&
        active.Attackers.ContainsKey(bot.TempProperties.GetProperty<string>("RvrEventForce") ?? $"rvr-{bot.DatabaseID}");

    /// <summary>An attacker at (or leaving) its border-keep muster post.</summary>
    public static void ReportMusterAttendance(string targetId, string forceId, long botId, bool inPosition, long nowTick,
        GameBot bot, Vector3 position)
    {
        lock (Sync)
        {
            if (!Events.TryGetValue(targetId, out var active)) return;
            if (active.DefenseMustering && !active.DefenseMarching && active.Defenders.ContainsKey(forceId))
            {
                if (inPosition) active.DefenseMustered[botId] = (forceId, nowTick, bot);
                else active.DefenseMustered.Remove(botId);
                int needed = active.DefenseReaction
                    ? DefenseWaveSize(active.Defenders.Values.Sum())
                    : Math.Max(4, DefenderCapFor(active) * 85 / 100);
                if (DefenseMusterAttendance(active, nowTick) >= needed)
                    GiveDefenseMarch(active, nowTick, "the defense rally has gathered");
                return;
            }
            if (active.Marching || !active.Attackers.ContainsKey(forceId)) return;
            if (inPosition) active.Mustered[botId] = (forceId, nowTick, bot, position);
            else active.Mustered.Remove(botId);
            int assigned = active.Attackers.Values.Sum();
            if (MusterAttendance(active, nowTick) >= (active.Retake ? DefenseWaveSize(assigned) : MarchThreshold(active.RelicKeep, assigned)) &&
                OrganizerHasArrived(active))
                GiveMarchOrder(active, nowTick, "enough attackers mustered at the border keep");
        }
    }

    private static eRealm OtherRealm(ActiveEvent active) =>
        new[] { eRealm.Albion, eRealm.Midgard, eRealm.Hibernia }.First(realm => realm != active.AttackerRealm && realm != active.DefenderRealm);

    private static Dictionary<string, int> Participants(ActiveEvent active, eRealm realm) =>
        realm == active.AttackerRealm ? active.Attackers : realm == active.DefenderRealm ? active.Defenders : active.ThirdRealm;

    /// <summary>
    /// Where a dead attacker of a running siege releases: its realm's muster for that frontier (the portal outpost the
    /// army gathered at, where its frontier teleporter lands), not the capital. Run 17 (2026-10-17): the Dun Dagda army
    /// fell from 121 at the keep to 39 in an hour; every dead Midgard attacker released in Jordheim and was still on
    /// the road when the battle was lost. Defenders release at their own realm's bind points nearby as before.
    /// </summary>
    public static bool TrySiegeRelease(string forceId, eRealm realm, long nowTick, out ushort region, out Point3D point)
    {
        region = 0; point = null;
        LiveObjective target;
        lock (Sync)
        {
            Expire(nowTick);
            var active = Events.Values.FirstOrDefault(entry => realm == entry.AttackerRealm && entry.Attackers.ContainsKey(forceId));
            if (active == null) return false;
            target = active.Target;
        }
        if (!AutonomousRvrStaging.TryGetSiegeMuster(realm, target.RegionId, out AutonomousRvrStaging.BorderKeep muster))
            return false;
        region = muster.RegionId;
        point = new Point3D((int)muster.Position.X, (int)muster.Position.Y, (int)muster.Position.Z);
        return true;
    }

    public static RallyOrder GetRallyOrder(string forceId, eRealm realm, long nowTick)
    {
        lock (Sync)
        {
            Expire(nowTick);
            // Owner 2026-10-07: a player-triggered defense must answer as one organised wave, not a trickle (run 24:
            // 240 Midgard defenders came in a few at a time and 109 died before forming a line). Its defenders get
            // muster orders although the battle is already on; allied helpers and the third realm stay chaotic.
            var active = Events.Values.FirstOrDefault(entry => (!entry.BattleStarted ||
                    entry.DefenseReaction && realm == entry.DefenderRealm && entry.DefenseMustering && !entry.DefenseMarching) &&
                Participants(entry, realm).ContainsKey(forceId));
            if (active == null) return null;
            var participants = Participants(active, realm);
            int count = participants[forceId];
            // Reuse freed posts even when differently sized warbands leave
            // gaps. Exact 128 attendance must not depend on divisibility by eight.
            if (!active.Slots.TryGetValue(forceId, out int[] slots) || slots.Length != count)
            {
                var used = participants.Keys.Where(id => id != forceId && active.Slots.ContainsKey(id))
                    .SelectMany(id => active.Slots[id]).ToHashSet();
                slots = realm == active.AttackerRealm && count >= AutonomousRvrCoalition.MinimumGroupMembers
                    ? AutonomousRvrRally.BlockAlignedSlots(used, count, Capacity(active)) : null;
                slots ??= Enumerable.Range(0, Capacity(active)).Where(slot => !used.Contains(slot)).Take(count).ToArray();
                active.Slots[forceId] = slots;
            }
            return new(active.TargetId, active.AttackerRealm, active.DefenderRealm, slots, active.ExpiresTick - nowTick,
                !active.Marching && realm == active.AttackerRealm ||
                (SiegeScheduling || active.DefenseReaction) && realm == active.DefenderRealm && active.DefenseMustering && !active.DefenseMarching);
        }
    }

    public static bool IsRallying(string forceId, long nowTick)
    {
        lock (Sync)
            return Events.Values.Any(entry => !entry.BattleStarted && entry.ExpiresTick > nowTick &&
                (entry.Attackers.ContainsKey(forceId) || entry.Defenders.ContainsKey(forceId) || entry.ThirdRealm.ContainsKey(forceId)));
    }

    /// <summary>
    /// A force in any running siege, mustering and marching included. Run 8 (2026-10-14): the relic army for Dun Dagda
    /// spent ninety minutes "Marching with the army" while its groups' rendezvous held them ("Meeting up with group",
    /// "Waiting for regroup leader"), some still in Midgard; the rendezvous stood aside only once a battle had begun.
    /// </summary>
    public static bool IsEventForce(string forceId, long nowTick)
    {
        lock (Sync)
            return Events.Values.Any(entry => entry.ExpiresTick > nowTick &&
                (entry.Attackers.ContainsKey(forceId) || entry.Defenders.ContainsKey(forceId) || entry.ThirdRealm.ContainsKey(forceId))) ||
                CarrierEvents.Values.Any(entry => entry.ExpiresTick > nowTick && entry.Participants.Values.Any(realm => realm.ContainsKey(forceId)));
    }

    public static bool IsBattleForce(string forceId, long nowTick)
    {
        lock (Sync)
            return Events.Values.Any(entry => entry.BattleStarted && entry.ExpiresTick > nowTick &&
                (entry.Attackers.ContainsKey(forceId) || entry.Defenders.ContainsKey(forceId) || entry.ThirdRealm.ContainsKey(forceId))) ||
                CarrierEvents.Values.Any(entry => entry.ExpiresTick > nowTick && entry.Participants.Values.Any(realm => realm.ContainsKey(forceId)));
    }

    public static Plan KeepPlan(string forceId, eRealm realm, long nowTick)
    {
        lock (Sync)
        {
            var active = Events.Values.FirstOrDefault(entry => entry.ExpiresTick > nowTick && Participants(entry, realm).ContainsKey(forceId));
            return active == null ? null : ToPlan(realm == active.DefenderRealm ? Intent.DefendEvent :
                active.RelicKeep ? Intent.AssaultRelicKeep : Intent.AssaultKeep, active.Target, true,
                active.BattleStarted ? "Return to the ongoing siege" : "Assemble at the physical siege rally");
        }
    }

    /// <summary>
    /// Owner 2026-11-01: every force currently committed to a live event, with its realm. The faction-chat announcement
    /// (AutonomousRvrChatAnnouncer) reads the leader's next objective from this, then asks KeepPlan for that force; the
    /// events themselves stay private, so nothing mutable escapes.
    /// </summary>
    public static List<(string ForceId, eRealm Realm)> ActiveForces(long nowTick)
    {
        var forces = new List<(string ForceId, eRealm Realm)>();
        lock (Sync)
        {
            foreach (ActiveEvent active in Events.Values)
            {
                if (active.ExpiresTick <= nowTick)
                    continue;
                foreach (string forceId in active.Attackers.Keys)
                    forces.Add((forceId, active.AttackerRealm));
                foreach (string forceId in active.Defenders.Keys)
                    forces.Add((forceId, active.DefenderRealm));
            }
        }
        return forces;
    }

    private static int Attendance(ActiveEvent active, eRealm realm, long nowTick) => active.Present.Values
        .Where(entry => entry.Realm == realm && nowTick - entry.Tick <= AttendanceFreshnessMilliseconds && entry.Tick <= nowTick &&
            (entry.Bot == null || entry.Bot.IsAlive && entry.Bot.ObjectState == GameObject.eObjectState.Active &&
                entry.Bot.CurrentRegionID == entry.Region && (active.BattleStarted || FightingAtTarget(active, entry.Bot) ||
                !entry.Bot.InCombat && !entry.Bot.IsMoving && (entry.Bot.Brain as BotBrain)?.HasAggro != true &&
                Vector3.DistanceSquared(new(entry.Bot.X, entry.Bot.Y, entry.Bot.Z), entry.Position) <= AutonomousRvrRally.ArrivalRadius * AutonomousRvrRally.ArrivalRadius)))
        .GroupBy(entry => entry.Force).Sum(group => Math.Min(group.Count(), Participants(active, realm).GetValueOrDefault(group.Key)));

    /// <summary>
    /// Fighting inside the rally-post ring of the target counts as physically there (run 12/13: attackers
    /// fought defenders 2,000-9,500 units from the keep and were not counted because they had left
    /// their posts to fight).
    /// </summary>
    public const int FightingAttendanceRadius = 9_500;

    private static bool FightingAtTarget(ActiveEvent active, GameBot bot) =>
        (bot.InCombat || bot.IsAttacking || (bot.Brain as BotBrain)?.HasAggro == true) &&
        bot.CurrentRegionID == active.Target.RegionId &&
        Vector2.DistanceSquared(new(bot.X, bot.Y), new(active.Target.X, active.Target.Y)) <=
        (float)FightingAttendanceRadius * FightingAttendanceRadius;

    public static bool ProtectsParticipantFromInactivity(GameBot bot, long nowTick)
    {
        if (bot?.IsAutonomousWorldBot != true || !bot.IsAlive ||
            bot.ObjectState != GameObject.eObjectState.Active ||
            !AutonomousObjectiveAssignments.Is(bot, eAutonomousObjectiveKind.RvR)) return false;
        string force = bot.TempProperties.GetProperty<string>("RvrEventForce") ?? $"rvr-{bot.DatabaseID}";
        lock (Sync)
        {
            foreach (var active in Events.Values)
            {
                if (active.ExpiresTick <= nowTick || !Participants(active, bot.Realm).ContainsKey(force)) continue;
                if (active.BattleStarted)
                {
                    // Real combat is intentional participation, even without a kill.
                    if (IsParticipatingInBattleCombat(bot)) return true;
                    // The army may be waiting at its assigned rally post, or a
                    // defender may be holding an interior wall with no current
                    // attacker. Neither is an abandoned travel objective.
                    if (active.Present.TryGetValue(bot.DatabaseID, out var staged) &&
                        staged.Force == force && ReferenceEquals(staged.Bot, bot) &&
                        nowTick >= staged.Tick && nowTick - staged.Tick <= AttendanceFreshnessMilliseconds &&
                        bot.CurrentRegionID == staged.Region &&
                        Vector3.DistanceSquared(new(bot.X, bot.Y, bot.Z), staged.Position) <=
                            AutonomousRvrRally.ArrivalRadius * AutonomousRvrRally.ArrivalRadius)
                        return true;
                    if (IsHoldingSiegeDefense(bot.Realm == active.DefenderRealm, bot.CurrentRegionID == active.Target.RegionId,
                        Vector2.DistanceSquared(new(bot.X, bot.Y), new(active.Target.X, active.Target.Y)),
                        Math.Abs(bot.Z - active.Target.Z))) return true;
                    continue;
                }
                if (active.Present.TryGetValue(bot.DatabaseID, out var present) &&
                    present.Force == force && present.Realm == bot.Realm && ReferenceEquals(present.Bot, bot) &&
                    present.Tick <= nowTick && nowTick - present.Tick <= AttendanceFreshnessMilliseconds &&
                    bot.CurrentRegionID == present.Region &&
                    Vector3.DistanceSquared(new(bot.X, bot.Y, bot.Z), present.Position) <=
                        AutonomousRvrRally.ArrivalRadius * AutonomousRvrRally.ArrivalRadius)
                    return true;
            }
            return IsParticipatingInBattleCombat(bot) && CarrierEvents.Values.Any(entry =>
                entry.ExpiresTick > nowTick && entry.Participants.TryGetValue(bot.Realm, out var forces) && forces.ContainsKey(force));
        }
    }

    private static bool IsParticipatingInBattleCombat(GameBot bot) => bot.InCombat || bot.IsAttacking ||
        bot.Group?.GetMembersInTheGroup().Any(member => member != bot && member.IsAlive &&
            member.CurrentRegionID == bot.CurrentRegionID && bot.IsWithinRadius(member, 2000) &&
            (member.InCombat || member.IsAttacking)) == true;

    public static bool IsHoldingSiegeDefense(bool defender, bool sameRegion, float distanceSquared, int heightDifference) =>
        defender && sameRegion && float.IsFinite(distanceSquared) && distanceSquared >= 0 &&
        distanceSquared <= 3000 * 3000 && heightDifference >= 0 && heightDifference <= 1500;

    public static void ReportAttendance(string targetId, string forceId, eRealm realm, long botId, bool inPosition, long nowTick,
        GameBot bot = null, Vector3 position = default)
    {
        lock (Sync)
        {
            Expire(nowTick);
            if (!Events.TryGetValue(targetId, out var active) ||
                !Participants(active, realm).ContainsKey(forceId)) return;
            if (inPosition) active.Present[botId] = (forceId, realm, nowTick, bot, position, bot?.CurrentRegionID ?? 0);
            else active.Present.Remove(botId);
            if (active.BattleStarted) return;
            // Run 18: the army that marched in (43 mustered, 48 at the posts) then sat at Nottmoor's posts
            // waiting for 85% of the cap (54) while it was whittled down to 19. Owner 2026-10-07 (all RvR, relic and
            // battleground sieges): once the threshold is reached the siege starts, no waiting at the keep. The
            // muster threshold was met before the march, so the army attacks as soon as it reaches the keep.
            // Run 2026-10-13: a straggler that walked in on its own opened Caer Sursbrooke with one attacker present
            // (the column far behind) and the siege was repelled. Posts open it only once most of the army that marched
            // stands there; the march leader's arrival (ReportArmyArrived) still opens it for the whole column.
            if (MarchedArmy(active) && inPosition && realm == active.AttackerRealm &&
                Attendance(active, active.AttackerRealm, nowTick) >= ArrivalQuorum(active.MarchedAttackers))
                StartBattle(active, nowTick, "the marching army reached the keep and attacks at once; defenders and third-realm reinforcements may respond");
            else if (nowTick - active.CreatedTick >= RealmEventPolicy.EarliestAssaultMilliseconds &&
                Ready(active, active.Marching, nowTick))
                StartBattle(active, nowTick, "the attacking force is physically staged; defenders and third-realm reinforcements may respond");
        }
    }

    public static bool TryConsumeRelease(string forceId, long nowTick, out string reason)
    {
        // Called for every solo actor. With no siege, carrier event or pending
        // release there is nothing Expire could end or release, so answer
        // without the layer-wide lock. (Unsynchronized Count reads: a stale
        // zero only defers a brand-new release to the next check.)
        if (Events.Count == 0 && CarrierEvents.Count == 0 && ReleasedForces.Count == 0)
        {
            reason = null;
            return false;
        }
        lock (Sync)
        {
            Expire(nowTick);
            return ReleasedForces.Remove(forceId, out reason);
        }
    }

    public static void ReportTravel(string targetId, string forceId, long memberId, double distance, bool arrived, long nowTick, GameBot bot = null)
    {
        lock (Sync)
        {
            if (!Events.TryGetValue(targetId, out var active) || active.BattleStarted ||
                !(active.Attackers.ContainsKey(forceId) || active.Defenders.ContainsKey(forceId) || active.ThirdRealm.ContainsKey(forceId))) return;
            if (!active.Travel.TryGetValue(memberId, out var previous))
                previous = (forceId, nowTick, double.PositiveInfinity, default, 0);
            Vector3 position = bot == null ? previous.Position : new(bot.X, bot.Y, bot.Z);
            ushort region = bot?.CurrentRegionID ?? previous.Region;
            // Long multi-region journeys and real horse travel must not look
            // stalled merely because their coordinates are on another map.
            bool moved = bot?.IsAlive == true && (region != previous.Region ||
                Vector3.DistanceSquared(position, previous.Position) >= 256 * 256);
            if (arrived || moved || distance < previous.BestDistance - 128)
                active.Travel[memberId] = (forceId, nowTick, distance, position, region);
            else active.Travel[memberId] = previous;
        }
    }

    /// <summary>
    /// Owner 2026-11-03 (siege rework phase 5): a group wiped out in a siege leaves the event and goes back to patrol (the
    /// regroup at its realm's portal keep is the ordinary one). It keeps no place in the coalition; its patrol lock starts anew,
    /// so it does not at once join again. False when the force is in no event.
    /// </summary>
    public static bool WithdrawWipedForce(string forceId, long nowTick)
    {
        lock (Sync)
        {
            bool found = false;
            foreach (var active in Events.Values)
            {
                if (!(active.Attackers.ContainsKey(forceId) || active.Defenders.ContainsKey(forceId) || active.ThirdRealm.ContainsKey(forceId)))
                    continue;
                found = true;
                active.Attackers.Remove(forceId);
                active.Defenders.Remove(forceId);
                active.ThirdRealm.Remove(forceId);
                active.Slots.Remove(forceId);
                foreach (long id in active.Travel.Where(pair => pair.Value.Force == forceId).Select(pair => pair.Key).ToArray())
                    active.Travel.Remove(id);
                foreach (long id in active.Present.Where(pair => pair.Value.Force == forceId).Select(pair => pair.Key).ToArray())
                    active.Present.Remove(id);
                foreach (long id in active.Mustered.Where(pair => pair.Value.Force == forceId).Select(pair => pair.Key).ToArray())
                    active.Mustered.Remove(id);
                foreach (long id in active.DefenseMustered.Where(pair => pair.Value.Force == forceId).Select(pair => pair.Key).ToArray())
                    active.DefenseMustered.Remove(id);
                SyncCoalition(active);
                var log = DOL.Logging.LoggerManager.Create(typeof(AutonomousRvrEventLayer));
                if (log.IsInfoEnabled) log.Info($"RVR_FORCE_WIPED_WITHDRAWN force={forceId} target={active.TargetId} " +
                    $"battle={active.BattleStarted} attackersLeft={active.Attackers.Values.Sum()}");
            }
            if (found)
            {
                AutonomousRvrPatrolLock.Begin(forceId, nowTick);
                WipedWithdrawn[forceId] = nowTick + AutonomousRvrPatrolLock.MinimumMilliseconds;
            }
            return found;
        }
    }

    public static void RemoveForce(string forceId)
    {
        AutonomousRvrRoamZones.Release(forceId);
        AutonomousRvrPatrolLock.Release(forceId);
        lock (Sync)
        {
            foreach (var active in Events.Values)
            {
                active.Attackers.Remove(forceId);
                active.Defenders.Remove(forceId);
                active.ThirdRealm.Remove(forceId);
                active.Slots.Remove(forceId);
                foreach (long id in active.Travel.Where(pair => pair.Value.Force == forceId).Select(pair => pair.Key).ToArray())
                    active.Travel.Remove(id);
                foreach (long id in active.Present.Where(pair => pair.Value.Force == forceId).Select(pair => pair.Key).ToArray())
                    active.Present.Remove(id);
            }
            foreach (var active in CarrierEvents.Values)
                foreach (var realm in active.Participants.Values) realm.Remove(forceId);
        }
    }

    /// <summary>
    /// Siege recruits who joined alone ("rvr-&lt;id&gt;" forces), per event and side, for the coordinator to merge into full
    /// parties (owner 2026-10-07: attackers and defenders move as full groups). Marching: the army has left its muster
    /// (or the battle is on), so smaller leftover parties are formed too.
    /// </summary>
    public sealed record SoloSiegeRoster(string EventId, eRealm Realm, bool Marching, long[] MemberIds);

    public static List<SoloSiegeRoster> SoloSiegeRosters(long nowTick)
    {
        var result = new List<SoloSiegeRoster>();
        lock (Sync)
        {
            foreach (ActiveEvent active in Events.Values.Where(entry => entry.ExpiresTick > nowTick))
            {
                bool marching = active.BattleStarted || active.Marching;
                foreach ((eRealm realm, Dictionary<string, int> side) in new[]
                         { (active.AttackerRealm, active.Attackers), (active.DefenderRealm, active.Defenders), (OtherRealm(active), active.ThirdRealm) })
                {
                    long[] solos = side.Keys.Where(force => force.StartsWith("rvr-", StringComparison.Ordinal))
                        .Select(force => long.TryParse(force.AsSpan(4), out long id) ? id : 0).Where(id => id > 0).ToArray();
                    if (solos.Length > 0) result.Add(new(active.TargetId, realm, marching, solos));
                }
            }
            foreach (var entry in CarrierEvents.Where(entry => entry.Value.ExpiresTick > nowTick))
                foreach ((eRealm realm, Dictionary<string, int> side) in entry.Value.Participants)
                {
                    long[] solos = side.Keys.Where(force => force.StartsWith("rvr-", StringComparison.Ordinal))
                        .Select(force => long.TryParse(force.AsSpan(4), out long id) ? id : 0).Where(id => id > 0).ToArray();
                    if (solos.Length > 0) result.Add(new(entry.Key, realm, true, solos));
                }
        }
        return result;
    }

    /// <summary>
    /// The coordinator formed a party from solo recruits of one event: their reservations become the party's (same cap),
    /// and every per-bot record (travel, presence, muster) now names the party's force.
    /// </summary>
    public static bool MergeSoloForces(string eventId, eRealm realm, long[] memberIds, string partyForceId)
    {
        lock (Sync)
        {
            string[] solos = memberIds.Select(id => $"rvr-{id}").ToArray();
            Dictionary<string, int> side = null;
            if (Events.TryGetValue(eventId, out ActiveEvent active))
                side = realm == active.AttackerRealm ? active.Attackers : realm == active.DefenderRealm ? active.Defenders : active.ThirdRealm;
            else if (CarrierEvents.TryGetValue(eventId, out var carrier))
                carrier.Participants.TryGetValue(realm, out side);
            if (side == null || solos.Any(force => !side.ContainsKey(force))) return false;
            foreach (string force in solos) side.Remove(force);
            side[partyForceId] = solos.Length;
            ReleasedForces.Remove(partyForceId);
            if (active != null)
            {
                foreach (string force in solos) active.Slots.Remove(force);
                var ids = memberIds.ToHashSet();
                foreach (long id in active.Travel.Keys.Where(ids.Contains).ToArray())
                    active.Travel[id] = active.Travel[id] with { Force = partyForceId };
                foreach (long id in active.Present.Keys.Where(ids.Contains).ToArray())
                    active.Present[id] = active.Present[id] with { Force = partyForceId };
                foreach (long id in active.Mustered.Keys.Where(ids.Contains).ToArray())
                    active.Mustered[id] = active.Mustered[id] with { Force = partyForceId };
                foreach (long id in active.DefenseMustered.Keys.Where(ids.Contains).ToArray())
                    active.DefenseMustered[id] = active.DefenseMustered[id] with { Force = partyForceId };
            }
            return true;
        }
    }

    public static long CarrierRemainingMilliseconds(string targetId, long nowTick)
    {
        lock (Sync)
            return CarrierEvents.TryGetValue(targetId, out var active) ? Math.Max(0, active.ExpiresTick - nowTick) : 0;
    }

    private static void StartBattle(ActiveEvent active, long nowTick, string reason)
    {
        active.BattleStarted = true;
        active.BattleStartedTick = nowTick;
        active.ExpiresTick = nowTick + (active.Retake ? RetakeBattleMilliseconds : BattleLifetimeMilliseconds);
        RealmEventRecords.Progress(active.TargetId, "Battle", reason,
            active.Attackers.Values.Sum() + active.Defenders.Values.Sum() + active.ThirdRealm.Values.Sum(),
            Attendance(active, active.AttackerRealm, nowTick) + Attendance(active, active.DefenderRealm, nowTick) + Attendance(active, OtherRealm(active), nowTick));
        RealmEventNotices.Queue(active.TargetId, active.AttackerRealm,
            $"The assault on {Place(active.Target.Name)} is advancing. Reinforcements are welcome.");
        RealmEventNotices.Queue(active.TargetId, active.DefenderRealm,
            $"The assault on {Place(active.Target.Name)} has begun! Hold the walls and stand by your realm.");
        if (active.ThirdRealm.Count > 0) RealmEventNotices.Queue(active.TargetId, OtherRealm(active),
            $"The armies at {Place(active.Target.Name)} are committed to battle. Watch their flanks, warbands!");
        string attacker = GlobalConstants.RealmToName(active.AttackerRealm);
        RealmEventNotices.QueueScreen(active.TargetId, active.AttackerRealm, $"Our army has laid siege to {(active.RelicKeep ? "the relic keep " : "")}{Place(active.Target.Name)}!", AnnouncementKind.RvrBattleground);
        RealmEventNotices.QueueScreen(active.TargetId, active.DefenderRealm, $"{Place(active.Target.Name)} is under siege by {attacker}!", AnnouncementKind.RvrBattleground);
        RealmEventNotices.QueueScreen(active.TargetId, OtherRealm(active),
            $"{attacker} has laid siege to {GlobalConstants.RealmToName(active.DefenderRealm)}'s {(active.RelicKeep ? "relic keep " : "")}{Place(active.Target.Name)}.", AnnouncementKind.RvrBattleground);
        var log = DOL.Logging.LoggerManager.Create(typeof(AutonomousRvrEventLayer));
        if (log.IsInfoEnabled) log.Info($"RVR_SIEGE_STARTED target={active.TargetId} reason=\"{reason}\" " +
            $"present={Attendance(active, active.AttackerRealm, nowTick)}/{Attendance(active, active.DefenderRealm, nowTick)}/{Attendance(active, OtherRealm(active), nowTick)}");
    }

    // Expire runs from many places on every bot's turn; deadlines are minutes long, so twice a second is plenty
    // (28 MB of iterators and closures in a 30 s allocation trace, 2026-10-17).
    public const long ExpireIntervalMilliseconds = 500;
    private static long _lastExpireTick = long.MinValue;

    /// <summary>
    /// True while the last live-clock pass is under <see cref="ExpireIntervalMilliseconds"/> old. Before the first pass
    /// (<c>long.MinValue</c>) <c>now - last</c> overflowed to a negative number, so every live call returned early and
    /// muster deadlines, the march after them, repels and the four-hour cap never ran on the live clock (found by
    /// stefanrows/OfflineDAoC 0.345.0).
    /// </summary>
    public static bool ExpireThrottled(long nowTick, long lastTick) =>
        lastTick != long.MinValue && nowTick >= lastTick && nowTick - lastTick < ExpireIntervalMilliseconds;

    /// <summary>
    /// Siege rework phase 1: keeps the coalition list in line with the attacking groups and says what changed. The organizer is
    /// the first group the event ever had (the one that opened it); a group that left is replaced as leader by the next in
    /// joining order. No movement depends on it yet.
    /// </summary>
    private static void SyncCoalition(ActiveEvent active)
    {
        var changes = active.Coalition.Reconcile(active.Attackers
            .Where(pair => pair.Value >= AutonomousRvrCoalition.MinimumGroupMembers).Select(pair => pair.Key));
        if (changes.Count == 0) return;
        var log = DOL.Logging.LoggerManager.Create(typeof(AutonomousRvrEventLayer));
        if (!log.IsInfoEnabled) return;
        foreach (var (kind, group) in changes)
        {
            string marker = kind switch
            {
                AutonomousRvrCoalition.Change.Joined => active.Coalition.Count == 1 || active.Coalition.Leader == group
                    ? "RVR_COALITION_FORMED" : "RVR_COALITION_JOINED",
                AutonomousRvrCoalition.Change.Left => "RVR_COALITION_LEFT",
                _ => "RVR_COALITION_PROMOTED",
            };
            log.Info($"{marker} target={active.TargetId} group={group} groups={active.Coalition.Count} " +
                     $"bots={active.Attackers.Values.Sum()} leader={active.Coalition.Leader}");
        }
    }

    private static void Expire(long nowTick)
    {
        // Only the live clock is throttled; a caller asking about another moment (tests step time by a millisecond) runs.
        if (nowTick == GameLoop.GameLoopTime)
        {
            long last = System.Threading.Interlocked.Read(ref _lastExpireTick);
            if (ExpireThrottled(nowTick, last))
                return;
            if (System.Threading.Interlocked.CompareExchange(ref _lastExpireTick, nowTick, last) != last)
                return;
        }
        foreach (var active in Events.Values)
        {
            SyncCoalition(active);
            if (!RealmEventBanter.ReminderDue(active.BattleStarted, active.PreparationNoticeSent, active.ExpiresTick - nowTick)) continue;
            active.PreparationNoticeSent = true;
            RealmEventNotices.Queue(active.TargetId, active.AttackerRealm, RealmEventBanter.SiegeReminder(Place(active.Target.Name), false));
            RealmEventNotices.Queue(active.TargetId, active.DefenderRealm, RealmEventBanter.SiegeReminder(Place(active.Target.Name), true));
            if (active.ThirdRealm.Count > 0) RealmEventNotices.Queue(active.TargetId, OtherRealm(active),
                $"The armies at {Place(active.Target.Name)} have about twenty minutes left to muster. Keep watch for an opening, scouts.");
        }
        foreach (var active in Events.Values.Where(entry => !entry.Marching && nowTick >= entry.MusterDeadlineTick).ToArray())
        {
            // Run 15: the 20-minute deadline sent 21 of 64 to Caer Erasleigh while the rest were still on the
            // road (medallions, frontier teleporter); too few to ever start the battle. A thin muster waits
            // once more, ten minutes, for the army that is genuinely on its way.
            int gathered = active.Mustered.Count;
            bool mayExtend = active.RelicKeep ? active.MusterExtensions < RelicMusterExtensions : !active.MusterExtended;
            if (!active.Retake && mayExtend && (gathered < RealmEventPolicy.DynamicStartThreshold(active.RelicKeep, true) || !MusterViable(active)))
            {
                active.MusterExtended = true;
                active.MusterExtensions++;
                active.MusterDeadlineTick = nowTick + (active.RelicKeep ? RelicMusterExtensionMilliseconds : MusterExtensionMilliseconds);
                active.ExpiresTick = Math.Max(active.ExpiresTick, active.MusterDeadlineTick + RealmEventPolicy.RecruitmentMilliseconds(0));
                var log = DOL.Logging.LoggerManager.Create(typeof(AutonomousRvrEventLayer));
                if (log.IsInfoEnabled) log.Info($"RVR_MUSTER_EXTENDED target={active.TargetId} mustered={gathered} " +
                    $"needed={RealmEventPolicy.DynamicStartThreshold(active.RelicKeep, true)} minutes={(active.RelicKeep ? RelicMusterExtensionMilliseconds : MusterExtensionMilliseconds) / 60_000}");
                continue;
            }
            if (!active.Retake && !active.Forced && !MusterViable(active))
            {
                EndEvent(active, nowTick, $"Rally failed: {gathered} attackers of {active.Mustered.Values.Select(entry => entry.Force).Distinct(StringComparer.Ordinal).Count()} groups gathered, " +
                    $"fewer than {MinimumArmy} bots or {AutonomousRvrCoalition.MinimumGroups} groups");
                continue;
            }
            GiveMarchOrder(active, nowTick, "muster deadline reached; marching with the attackers gathered");
        }
        foreach (var active in Events.Values.Where(entry => entry.DefenseMustering && !entry.DefenseMarching &&
                     nowTick >= entry.DefenseMusterDeadlineTick).ToArray())
        {
            // Run 16: the 6-minute defense muster marched 4 of 32 to Nottmoor; the owner wants one quick rally
            // that goes in together, not a trickle. A muster under half strong waits once more (4 minutes).
            // Run 25: a 3-minute player-defense muster marched with 1 of 175 (the rest were still walking in from 60k
            // away). A player's attack now musters until 80% have gathered or ten minutes pass (owner: a huge force
            // matters more than speed); there is no further extension.
            if (!active.DefenseReaction && !active.DefenseMusterExtended && active.DefenseMustered.Count < DefenderCapFor(active) / 2)
            {
                active.DefenseMusterExtended = true;
                active.DefenseMusterDeadlineTick = nowTick + DefenseMusterExtensionMilliseconds;
                var log = DOL.Logging.LoggerManager.Create(typeof(AutonomousRvrEventLayer));
                if (log.IsInfoEnabled) log.Info($"RVR_DEFENSE_MUSTER_EXTENDED target={active.TargetId} mustered={active.DefenseMustered.Count} " +
                    $"cap={DefenderCapFor(active)} minutes={DefenseMusterExtensionMilliseconds / 60_000}");
                continue;
            }
            GiveDefenseMarch(active, nowTick, "defense muster deadline reached; marching with the defenders gathered");
        }
        foreach (var active in Events.Values.Where(entry => entry.BattleStarted && !entry.DefenseReaction).ToArray())
        {
            // Only an army that reached the keep and was then driven off: a battle can begin before its attackers
            // arrive (continuous assault, deadline start), and an army still on the road is not repelled.
            if (Attendance(active, active.AttackerRealm, nowTick) > 0) { active.AttackersLastSeenTick = nowTick; continue; }
            if (active.AttackersLastSeenTick > 0 && nowTick - active.AttackersLastSeenTick >= RepelMilliseconds)
                EndEvent(active, nowTick, $"Siege defended: no attacker reached the keep for {RepelMilliseconds / 60_000} minutes", active.DefenderRealm);
        }
        foreach (var active in Events.Values.Where(entry => entry.ExpiresTick <= nowTick).ToArray())
        {
            if (active.BattleStarted)
                EndEvent(active, nowTick, active.DefenseReaction ? "Siege defended: four-hour defense response expired" : "Siege defended: four-hour battle timer expired");
            else if (Ready(active, true, nowTick))
                StartBattle(active, nowTick, "preparation deadline reached with a viable attacking force");
            else
                EndEvent(active, nowTick, "Rally failed: insufficient physical attackers at the one-hour deadline");
        }
        if (nowTick >= _nextStragglerSweep)
        {
            _nextStragglerSweep = nowTick + 10_000;
            foreach (var active in Events.Values.Where(entry => !entry.BattleStarted).ToArray())
            foreach (var stalled in active.Travel.Where(pair => nowTick - pair.Value.ProgressTick >= StragglerTimeoutMilliseconds)
                         .GroupBy(pair => pair.Value.Force).ToArray())
            {
                // Missing members remain assigned so they can reinforce the
                // deadline-started battle. Keep bounded diagnostics, not eviction.
                string reason = $"Assigned members {string.Join(",", stalled.Select(pair => pair.Key))} made no approach progress for eight minutes; retaining siege assignment";
                foreach (var member in stalled)
                    active.Travel[member.Key] = member.Value with { ProgressTick = nowTick };
                var log = DOL.Logging.LoggerManager.Create(typeof(AutonomousRvrEventLayer));
                if (log.IsWarnEnabled) log.Warn($"RVR_RALLY_DELAY target={active.TargetId} force={stalled.Key} reason=\"{reason}\"");
            }
        }
        foreach (var entry in CarrierEvents.Where(pair => pair.Value.ExpiresTick <= nowTick).ToArray())
        {
            foreach (eRealm realm in entry.Value.Participants.Keys)
                RealmEventNotices.Queue(entry.Key, realm, $"Our time in the struggle for {CarrierTargets.GetValueOrDefault(entry.Key)?.Name ?? "the relic"} has run out. The escort and interception forces are standing down.");
            RealmEventRecords.Finish(entry.Key, "Timed out", "Relic escort/interception: four-hour battle timer expired.");
            foreach (string force in entry.Value.Participants.Values.SelectMany(realm => realm.Keys))
                ReleasedForces[force] = "Relic event ended: four-hour battle timer expired";
            CarrierEvents.Remove(entry.Key);
            CarrierTargets.Remove(entry.Key);
            Cooldowns[entry.Key] = nowTick + TargetCooldownMilliseconds;
        }
    }

    /// <summary>
    /// The realm record of a siege that was neither captured nor defended, named by why it ended (owner 2026-10-29: it
    /// read "Ended (unconfirmed)", nearly always because the keep stopped being a target, e.g. another realm took it).
    /// </summary>
    public static string SiegeEndOutcome(string reason) =>
        reason?.StartsWith("Siege objective is no longer available") == true ? "Ended: keep no longer a target" :
        reason?.Contains("four-hour") == true || reason?.Contains("timer") == true ? "Timed out" :
        string.IsNullOrWhiteSpace(reason) ? "Ended" : "Ended: " + reason.Split(':', '.')[0].Trim();

    private static void EndEvent(ActiveEvent active, long nowTick, string reason, eRealm winner = eRealm.None)
    {
        foreach (eRealm realm in new[] { active.AttackerRealm, active.DefenderRealm, OtherRealm(active) })
            RealmEventNotices.Queue(active.TargetId, realm, RealmEventBanter.SiegeOutcome(Place(active.Target.Name),
                realm == active.DefenderRealm, reason.StartsWith("Siege defended"), reason.StartsWith("Rally failed"), winner, realm));
        RealmEventRecords.Finish(active.TargetId, reason.StartsWith("Rally failed") ? "Failed rally" : reason.StartsWith("Keep captured") ? "Captured" :
            reason.StartsWith("Siege defended") ? reason.Contains("expired") ? "Defended (timeout)" : "Defended" : SiegeEndOutcome(reason), reason,
            active.Attackers.Values.Sum() + active.Defenders.Values.Sum() + active.ThirdRealm.Values.Sum());
        foreach (string force in active.Attackers.Keys.Concat(active.Defenders.Keys).Concat(active.ThirdRealm.Keys))
            ReleasedForces[force] = reason;
        Events.Remove(active.TargetId);
        Cooldowns[active.TargetId] = nowTick + TargetCooldownMilliseconds;
        // Owner 2026-11-01: the conquest gap of the realm runs from the end of its attempt, whichever way it ended.
        if (active.Conquest) AutonomousRvrCampaign.RecordConquest(active.AttackerRealm, nowTick);
        var log = DOL.Logging.LoggerManager.Create(typeof(AutonomousRvrEventLayer));
        if (log.IsInfoEnabled) log.Info($"RVR_EVENT_ENDED target={active.TargetId} reason=\"{reason}\"");
    }

    /// <summary>
    /// Run 7 (2026-10-14): a relic siege forced on Dun Dagda got no attackers at all, because Midgard's warbands were
    /// already committed to an automatic siege of Dun Crauchon that had not begun. A forced siege takes over such a
    /// rally: its warbands are recorded as the forced siege's attackers and muster for it instead.
    /// </summary>
    private static void SupersedeAutomaticSieges(ActiveEvent forced, long nowTick)
    {
        foreach (ActiveEvent active in Events.Values.Where(active => active != forced && !active.Forced && !active.DefenseReaction &&
                     !active.BattleStarted && active.AttackerRealm == forced.AttackerRealm).ToArray())
        {
            foreach (var (force, members) in active.Attackers)
                if (forced.Attackers.Values.Sum() + members <= Capacity(forced)) forced.Attackers[force] = members;
            int moved = active.Attackers.Count;
            RealmEventRecords.Finish(active.TargetId, "Superseded", $"A forced siege of {forced.Target.Name} took this rally's army",
                active.Attackers.Values.Sum() + active.Defenders.Values.Sum() + active.ThirdRealm.Values.Sum());
            // Its defenders and third realm go back to their own choices (not released: nothing ended for them).
            Events.Remove(active.TargetId);
            Cooldowns[active.TargetId] = nowTick + TargetCooldownMilliseconds;
            var log = DOL.Logging.LoggerManager.Create(typeof(AutonomousRvrEventLayer));
            if (log.IsInfoEnabled)
                log.Info($"RVR_EVENT_SUPERSEDED target={active.TargetId} by={forced.TargetId} warbands={moved}");
        }
    }

    private static bool TryJoin(Dictionary<string, int> participants, Force force, int cap)
    {
        if (participants.Values.Sum() - participants.GetValueOrDefault(force.GroupId) + force.MemberCount > cap)
            return false;
        participants[force.GroupId] = force.MemberCount;
        return true;
    }

    private static int AssaultPressure(Force force, LiveObjective objective) =>
        objective.EnemyCount * 4 + objective.GuardStrength * 2 + objective.ClosedDoors * 3 - force.MemberCount * 2;

    private static bool OnCooldown(string targetId, long nowTick) => Cooldowns.TryGetValue(targetId, out long until) && until > nowTick;

    private static bool TryJoinCarrier(LiveObjective carrier, Force force, long nowTick, double roll)
    {
        string carrierId = carrier.Id;
        CarrierTargets[carrierId] = carrier;
        if (!CarrierEvents.TryGetValue(carrierId, out var active))
        {
            active = (nowTick + BattleLifetimeMilliseconds, new Dictionary<eRealm, Dictionary<string, int>>());
            CarrierEvents[carrierId] = active;
            RealmEventRecords.Begin(carrierId, carrier.Name, "Relic", GlobalConstants.RealmToName(carrier.OwningRealm), "Relic escort / interception");
            RealmEventRecords.Progress(carrierId, "Battle", "Relic escort / interception", 0, 0);
        }
        if (!active.Participants.TryGetValue(force.Realm, out Dictionary<string, int> realmParticipants))
            active.Participants[force.Realm] = realmParticipants = new Dictionary<string, int>(StringComparer.Ordinal);
        if (!realmParticipants.ContainsKey(force.GroupId) &&
            !ShouldJoinActiveEvent(force, realmParticipants.Values.Sum(), RelicCarrierRealmCap, true, false, roll))
            return false;
        return TryJoin(realmParticipants, force, RelicCarrierRealmCap);
    }

    private static void Cleanup(long nowTick, IReadOnlyCollection<LiveObjective> objectives)
    {
        Expire(nowTick);
        foreach (LiveObjective carrier in objectives.Where(o => o.IsRelicCarrier && CarrierEvents.ContainsKey(o.Id)))
            CarrierTargets[carrier.Id] = carrier;
        foreach (ActiveEvent active in Events.Values)
        {
            if (objectives.Any(target => target.Id == active.TargetId && target.UnderAttack))
            {
                if (!active.AttackObserved)
                {
                    active.AttackObserved = true;
                    RealmEventNotices.Queue(active.TargetId, active.DefenderRealm,
                        $"{Place(active.Target.Name)} is under attack! Available warbands can reinforce the defenders.");
                    RealmEventNotices.Queue(active.TargetId, OtherRealm(active),
                        $"Fighting has broken out at {Place(active.Target.Name)}. There is an opportunity to intervene.");
                }
                // The marching army striking the keep starts the siege once most of the army that marched is
                // there (ArrivalQuorum, as at the rally posts). Run 17 (2026-10-17): the first bot of Albion's column
                // to hit Caer Berkstead's guards opened the siege with 1 attacker present, and a relic army that
                // fights its way in piecemeal is what falls to the keep's guards. Incidental combat before the
                // march still cannot start it.
                if (!active.BattleStarted && (MarchedArmy(active) &&
                        Attendance(active, active.AttackerRealm, nowTick) >= ArrivalQuorum(active.MarchedAttackers) ||
                        active.Marching && !active.DefenseReaction &&
                        nowTick - active.CreatedTick >= RealmEventPolicy.EarliestAssaultMilliseconds && Ready(active, true, nowTick)))
                    StartBattle(active, nowTick, "the marching army's attack on the keep has begun");
            }
        }
        // Each caller supplies only objectives it can reach, not an authoritative
        // world inventory. An absent row cannot cancel another force's expedition.
        foreach (string id in Events.Where(pair => pair.Value.ExpiresTick <= nowTick ||
                     objectives.Any(target => target.Id == pair.Key && target.OwningRealm != pair.Value.DefenderRealm))
                     .Select(pair => pair.Key).ToArray())
        {
            var changed = objectives.FirstOrDefault(target => target.Id == id && target.OwningRealm != Events[id].DefenderRealm);
            EndEvent(Events[id], nowTick, changed == null ? "Siege objective is no longer available" : "Keep captured: ownership changed",
                changed?.OwningRealm ?? eRealm.None);
        }
        foreach (string id in Cooldowns.Where(pair => pair.Value <= nowTick).Select(pair => pair.Key).ToArray())
            Cooldowns.Remove(id);
        foreach (var key in Avoided.Where(pair => pair.Value <= nowTick).Select(pair => pair.Key).ToArray())
            Avoided.Remove(key);
        foreach (string id in CarrierEvents.Where(pair => pair.Value.ExpiresTick <= nowTick)
                     .Select(pair => pair.Key).ToArray())
        {
            RealmEventRecords.Finish(id, "Timed out", "Relic event expired during objective cleanup.");
            foreach (string force in CarrierEvents[id].Participants.Values.SelectMany(realm => realm.Keys))
                ReleasedForces[force] = "Relic captured or event no longer available";
            CarrierEvents.Remove(id);
            CarrierTargets.Remove(id);
            Cooldowns[id] = nowTick + TargetCooldownMilliseconds;
        }
    }

    public static bool IsForceCommitted(string forceId, long nowTick)
    {
        if (string.IsNullOrWhiteSpace(forceId))
            return false;
        lock (Sync)
            return Events.Values.Any(active => active.ExpiresTick > nowTick &&
                       (active.Attackers.ContainsKey(forceId) || active.Defenders.ContainsKey(forceId) ||
                        active.ThirdRealm.ContainsKey(forceId))) ||
                   CarrierEvents.Values.Any(active => active.ExpiresTick > nowTick &&
                       active.Participants.Values.Any(realm => realm.ContainsKey(forceId)));
    }

    /// <summary>Owner 2026-11-01: true when the force is on a relic operation - an assault on a relic keep, or the escort
    /// of a relic carrier. Those are the only RvR groups allowed inside the frontier dungeons that lead to Summoner's
    /// Hall; any other group that ends up in one is sent home by AutonomousRvrDungeonGuard.</summary>
    public static bool OnRelicDuty(string forceId)
    {
        if (string.IsNullOrWhiteSpace(forceId))
            return false;
        lock (Sync)
            return Events.Values.Any(active => active.RelicKeep &&
                       (active.Attackers.ContainsKey(forceId) || active.Defenders.ContainsKey(forceId) ||
                        active.ThirdRealm.ContainsKey(forceId))) ||
                   CarrierEvents.Values.Any(active => active.Participants.Values.Any(realm => realm.ContainsKey(forceId)));
    }

    public static bool IsPlayerDefenseResponse(string targetId, long nowTick)
    {
        if (string.IsNullOrWhiteSpace(targetId)) return false;
        lock (Sync)
            return Events.TryGetValue(targetId, out var active) && active.ExpiresTick > nowTick &&
                (active.DefenseReaction || !string.IsNullOrEmpty(active.PlayerAccount));
    }

    public static bool IsTargetActive(string targetId, long nowTick)
    {
        if (string.IsNullOrWhiteSpace(targetId))
            return false;
        lock (Sync)
            return (Events.TryGetValue(targetId, out ActiveEvent active) && active.ExpiresTick > nowTick) ||
                   (CarrierEvents.TryGetValue(targetId, out var carrier) && carrier.ExpiresTick > nowTick);
    }

    public static void EndTarget(string targetId, long nowTick, eRealm winner = eRealm.None)
    {
        if (string.IsNullOrWhiteSpace(targetId))
            return;
        lock (Sync)
        {
            if (Events.TryGetValue(targetId, out var active))
            {
                EndEvent(active, nowTick, "Keep captured: siege ended", winner);
                // Owner 2026-10-07: when a player takes the keep, the realm that lost it comes back once to retake it.
                if (active.DefenseReaction && winner != eRealm.None && winner == active.AttackerRealm)
                    StartRetake(active, winner, nowTick);
            }
            if (CarrierEvents.TryGetValue(targetId, out var escort))
            {
                string name = CarrierTargets.GetValueOrDefault(targetId)?.Name ?? "The relic";
                foreach (eRealm realm in escort.Participants.Keys.Append(winner).Where(r => r != eRealm.None).Distinct())
                    RealmEventNotices.Queue(targetId, realm, winner == eRealm.None
                        ? $"{name} has been secured at a shrine. The struggle on the road has ended."
                        : realm == winner ? $"{name} is safe in our shrine! Honour to its bearers and defenders."
                        : $"{GlobalConstants.RealmToName(winner)} has secured {name} at a shrine. Our struggle on the road is over.");
            }
            bool removed = CarrierEvents.Remove(targetId, out var carrier);
            if (removed) RealmEventRecords.Finish(targetId, "Captured / returned", "Relic mounted or recovered; escort/interception ended.");
            CarrierTargets.Remove(targetId);
            if (removed)
                foreach (string force in carrier.Participants.Values.SelectMany(realm => realm.Keys))
                    ReleasedForces[force] = "Relic captured: event ended";
            if (removed)
                Cooldowns[targetId] = nowTick + TargetCooldownMilliseconds;
        }
    }

    public static void TransferToRelicCarrier(string keepId, string carrierId, long nowTick, LiveObjective carrierAnchor = null)
    {
        lock (Sync)
        {
            if (!Events.TryGetValue(keepId, out var active) || active.ExpiresTick <= nowTick) return;
            // Even if the relic left before the battle formally began, every side follows the relic now (owner
            // 2026-10-07: all sides focus the relic wherever it is, carried with an escort or not).
            if (!active.BattleStarted) active.ExpiresTick = Math.Max(active.ExpiresTick, nowTick + BattleLifetimeMilliseconds);
            CarrierEvents[carrierId] = (active.ExpiresTick, new()
            {
                [active.AttackerRealm] = new(active.Attackers, StringComparer.Ordinal),
                [active.DefenderRealm] = new(active.Defenders, StringComparer.Ordinal),
                [OtherRealm(active)] = new(active.ThirdRealm, StringComparer.Ordinal)
            });
            // Retain the last known native location through partial route
            // snapshots. Capture/expiry still releases the whole escort.
            CarrierTargets[carrierId] = carrierAnchor ?? active.Target with
                { Id = carrierId, IsRelicCarrier = true, OwningRealm = active.AttackerRealm };
            foreach (eRealm realm in new[] { active.AttackerRealm, active.DefenderRealm, OtherRealm(active) })
                RealmEventNotices.Queue(keepId, realm, $"The relic has left {Place(active.Target.Name)}! The battle continues on the road; its fate is not yet decided.");
            RealmEventRecords.Finish(keepId, "Relic taken", "Relic removed; the battle continued as an escort/interception event.");
            RealmEventRecords.Begin(carrierId, carrierAnchor?.Name ?? active.Target.Name, "Relic", GlobalConstants.RealmToName(active.AttackerRealm), "Continued from " + active.Target.Name);
            RealmEventRecords.Progress(carrierId, "Battle", "Relic escort / interception", active.Attackers.Values.Sum() + active.Defenders.Values.Sum() + active.ThirdRealm.Values.Sum(), 0);
            Events.Remove(keepId);
            Cooldowns[keepId] = nowTick + TargetCooldownMilliseconds;
            if (active.Conquest) AutonomousRvrCampaign.RecordConquest(active.AttackerRealm, nowTick);
        }
    }

    public static string RelicCarrierTargetId(GameRelic relic)
    {
        return relic == null ? string.Empty : $"rvr-relic-carrier-{(int)relic.OriginalRealm}-{(int)relic.RelicType}";
    }
}
