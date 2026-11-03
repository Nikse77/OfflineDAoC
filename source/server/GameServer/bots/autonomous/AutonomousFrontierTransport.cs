using System;
using System.Linq;
using System.Numerics;
using DOL.AI.Brain;
using DOL.Database;
using DOL.GS.Scripts;

namespace DOL.GS;

/// <summary>Bot passengers on the existing Old Frontiers porter's cast cycle.</summary>
public static class AutonomousFrontierTransport
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<OFTeleporter, byte> Porters = new();
    public static void Register(OFTeleporter porter) => Porters[porter] = 0;
    public static void Unregister(OFTeleporter porter) => Porters.TryRemove(porter, out _);
    public static OFTeleporter NearestPorter(GameBot bot) => Porters.Keys
        .Where(p => p.ObjectState == GameObject.eObjectState.Active && p.CurrentRegion == bot.CurrentRegion && p.Realm == bot.Realm)
        .OrderBy(bot.GetDistanceTo).FirstOrDefault();

    /// <summary>
    /// The frontier porter standing on the passage pad of its realm's home keep (Castle Sauvage's Master Visur,
    /// Svasud Faste's stor gothi, Druim Ligen's Glasny): the circle a player stands in to take the passage, about
    /// 1,700 units from the pad the home necklace lands on. Null when no porter of that realm is in the region.
    /// Owner 2026-11-01: the level-50 bots enter the world on this pad and wait there.
    /// </summary>
    public static OFTeleporter HomePorter(eRealm realm, ushort region)
    {
        Passage home = Destination(realm, region);
        if (home == null || home.Medallion != "home_necklace")
            return null;
        Vector2 homePoint = new(home.Location.X, home.Location.Y);
        return Porters.Keys
            .Where(p => p != null && p.ObjectState == GameObject.eObjectState.Active &&
                        p.Realm == realm && p.CurrentRegionID == region &&
                        Vector2.DistanceSquared(new(p.X, p.Y), homePoint) <= 4_000f * 4_000f)
            .OrderBy(p => Vector2.DistanceSquared(new(p.X, p.Y), homePoint))
            .FirstOrDefault();
    }

    /// <summary>The pad of that porter, or false when the realm's porter is not in the world.</summary>
    public static bool TryGetHomePad(eRealm realm, ushort region, out Vector3 pad)
    {
        OFTeleporter porter = HomePorter(realm, region);
        // Owner 2026-11-02: a porter that is registered but not placed in the world yet reports 0,0,0, and the level-50
        // login landed there - "keep=1:0,0,0" 110 times in the 19:51 boot and 33 in the 21:21 one, every one followed by
        // "login position invalid" and a repair somewhere else. No frontier coordinate is this close to the origin, so an
        // unplaced porter counts as no pad and the caller falls back to the necklace's fixed landing.
        bool placed = porter != null && porter.X > HomePadMinimum && porter.Y > HomePadMinimum;
        pad = placed ? new(porter.X, porter.Y, porter.Z) : default;
        return placed;
    }

    /// <summary>Below this an X or Y is the origin, not a frontier keep: real values run from about 300,000 to 800,000.</summary>
    private const int HomePadMinimum = 100_000;

    public static GameBot[] BoardingParty(GameBot bot, Passage passage) =>
        HasCommittedSiegePassage(bot, passage) || passage.Medallion == "home_necklace" &&
        AutonomousObjectiveAssignments.Parse(bot.PersistentRecord?.ObjectiveKind) != eAutonomousObjectiveKind.RvR
            ? [bot] // Initial PvE meetups can have members returning from different frontiers.
            : bot.Group?.GetMembersInTheGroup().OfType<GameBot>().ToArray() ?? [bot];

    private sealed class BoardingBatch
    {
        public bool Active;
        public FrontierBoardingQueue<GameBot> Waiting = new();
        public System.Collections.Generic.HashSet<GameBot> Handled = new();
        public ECSGameTimer Timer;
        public long OpenUntil;
    }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<OFTeleporter, BoardingBatch> Batches = new();
    public const string RequestKey = "RvrFrontierPassage";
    public const int BoardingRadius = 500;
    private const int BoardingMargin = 50;
    public const int SlicePassengerLimit = 8;
    public const int SliceMilliseconds = 3;
    public const int SliceIntervalMilliseconds = 50;
    public const int BoardingWindowMilliseconds = 30_000;
    public const long WarbandWaitMilliseconds = 60_000;
    private const string BlockedSinceKey = "RvrFrontierBlockedSince";

    public static bool PartialDepartureDue(long blockedSince, long now) =>
        blockedSince > 0 && now - blockedSince >= WarbandWaitMilliseconds;
    public static bool SliceFull(int processed, double elapsedMilliseconds) =>
        processed >= SlicePassengerLimit || elapsedMilliseconds >= SliceMilliseconds;

    public static Vector3 WaitingOffset(long botId, int attempt = 0)
    {
        // Stable per character, not per warband: no shared endpoint at the NPC.
        ulong seed = unchecked((ulong)botId * 11400714819323198485UL);
        double angle = ((seed >> 16) % 65536) * (Math.PI * 2 / 65536) + attempt * 2.399963229728653;
        float radius = 220 + (float)(seed % 191);
        return new((float)Math.Cos(angle) * radius, (float)Math.Sin(angle) * radius, 0);
    }

    public static bool TryWaitingPoint(GameBot bot, OFTeleporter porter, out Vector3 point)
    {
        // Owner 2026-11-02: a bot that already holds a boarding request is a traveller, not a party waiting for its
        // leader, so it has to stand inside the boarding radius. It used to get the gate-front fan that our own
        // "wait in front of the gate" rule puts 963 units from the porter of the entry keeps: Wilorred stood at
        // 474,576/342,751, Master Visur at 475,766/343,201 - 1,272 apart against a boarding radius of 500 - and never
        // boarded, never moved, and never said why.
        bool boarding = bot.TempProperties.GetProperty<Request>(RequestKey)?.Porter == porter;
        return TryWaitingPoint(PathfindingProvider.Instance, porter.CurrentZone, new(porter.X, porter.Y, porter.Z),
            bot.Realm, bot.DatabaseID, out point, boarding);
    }

    /// <summary>Inside the boarding radius with a margin, so a step of drift cannot put a traveller out of reach.</summary>
    public static bool InsideBoardingReach(Vector3 point, Vector3 porter) =>
        Vector3.Distance(point, porter) <= BoardingRadius - BoardingMargin;

    public static bool TryWaitingPoint(IPathfindingMgr nav, Zone zone, Vector3 center, eRealm realm, long botId,
        out Vector3 point, bool boarding = false)
    {
        point = default;
        if (zone == null || !nav.IsAvailable || !nav.HasNavmesh(zone)) return false;
        // Owner 2026-11-02: at the entry keeps of Hadrian, Odin and Emain the waiting party stands in the open in front
        // of the gate, not in a ring around the porter and never behind the keep: the cluster is tied to that spot.
        // Owner 2026-11-02, second pass: that spot is for waiting only. Whoever has a passage request must stand within
        // BoardingRadius of the porter, so a traveller keeps the ring and the fan stays for the party.
        Point3D gate = default;
        bool gateFront = !boarding && AutonomousFrontierRelease.TryGateFront(realm, zone.ZoneRegion.ID, out gate);
        Vector3 origin = gateFront ? new Vector3(gate.X, gate.Y, gate.Z) : center;
        for (int attempt = 0; attempt < 8; attempt++)
        {
            Vector3 desired = origin + (gateFront
                ? AutonomousFrontierRelease.GateOffset(realm, zone.ZoneRegion.ID, botId, attempt)
                : WaitingOffset(botId, attempt));
            Vector3? floor = nav.GetClosestPoint(zone, desired, 32, 32, 96, nav.DefaultFilters);
            if (!floor.HasValue || Vector3.Distance(origin, floor.Value) > (gateFront ? 860 : 450) ||
                (boarding && !InsideBoardingReach(floor.Value, center)) ||
                (!gateFront && Vector2.Distance(new(center.X, center.Y), new(floor.Value.X, floor.Value.Y)) < 200) ||
                !AutonomousRealmBoundary.Allows(realm, zone.ZoneRegion.ID, zone.ID) ||
                !AutonomousZoneItinerary.HasCompleteCorridor(nav, zone, center, floor.Value)) continue;
            point = floor.Value;
            return true;
        }
        return false;
    }
    public sealed record Passage(ushort Region, string Medallion, GameLocation Location);
    public sealed record Request(OFTeleporter Porter, Passage Passage, string ForceId);

    private static AutonomousRvrEventLayer.Plan ActiveSiegePlan(GameBot bot)
    {
        if (bot?.IsAutonomousWorldBot != true || bot.IsPlayerLedGroup || bot.IsTemporaryGroupHelper ||
            !AutonomousObjectiveAssignments.Is(bot, eAutonomousObjectiveKind.RvR)) return null;
        string force = bot.TempProperties.GetProperty<string>("RvrEventForce") ?? $"rvr-{bot.DatabaseID}";
        return AutonomousRvrEventLayer.KeepPlan(force, bot.Realm, GameLoop.GameLoopTime);
    }

    public static bool PassageMatchesSiege(GameBot bot, Passage passage)
    {
        var plan=ActiveSiegePlan(bot);
        return plan==null || plan.RegionId==passage?.Region;
    }
    public static bool HasCommittedSiegePassage(GameBot bot, Passage passage) =>
        ActiveSiegePlan(bot) is { } plan && plan.RegionId==passage?.Region;
    public static bool HasDefenderPriority(GameBot bot, Passage passage) =>
        ActiveSiegePlan(bot) is { Intent: AutonomousRvrEventLayer.Intent.DefendEvent } plan && plan.RegionId==passage?.Region;

    // These are the existing OFTeleporter destinations, shared with its player
    // callback. The two invading realms arrive at different native portal keeps.
    public static Passage Destination(eRealm realm, ushort region) => (realm, region) switch
    {
        (eRealm.Albion, 100) => new(100,"odin_necklace",new("Odin Alb",100,596364,631509,5971)),
        (eRealm.Albion, 200) => new(200,"emain_necklace",new("Emain Alb",200,475835,343661,4080)),
        (eRealm.Midgard, 1) => new(1,"hadrian_necklace",new("Hadrian Mid",1,655200,293217,4879)),
        (eRealm.Midgard, 200) => new(200,"emain_necklace",new("Emain Mid",200,474107,295199,3871)),
        (eRealm.Hibernia, 1) => new(1,"hadrian_necklace",new("Hadrian Hib",1,605743,293676,4839)),
        (eRealm.Hibernia, 100) => new(100,"odin_necklace",new("Odin Hib",100,596055,581400,6031)),
        (eRealm.Albion, 1) => new(1,"home_necklace",new("Home Alb",1,584285,477200,2600)),
        (eRealm.Midgard, 100) => new(100,"home_necklace",new("Home Mid",100,766811,669605,5736)),
        (eRealm.Hibernia, 200) => new(200,"home_necklace",new("Home Hib",200,334386,420071,5184)),
        _ => null,
    };

    /// <summary>The frontier whose keep sells this realm's medallions: Castle Sauvage, Svasud Faste, Druim Ligen.</summary>
    public static ushort HomeFrontier(eRealm realm) => realm switch
    {
        eRealm.Albion => 1,
        eRealm.Midgard => 100,
        eRealm.Hibernia => 200,
        _ => 0,
    };

    /// <summary>
    /// Owner 2026-11-02: "i raduni in altre frontiere devono SEMPRE passare dai portali". A frontier vendor stocks
    /// the necklaces of its own realm's frontier plus the home one, so a bot standing in a foreign frontier can only
    /// buy the home medallion - Gwulla in Emain sells home_necklace and nothing else - and a third frontier is two
    /// passages, home first. The predicate is the medallion arithmetic, not a preference: it may only be true when
    /// the target is neither the frontier the bot is in nor its own home one.
    /// </summary>
    public static bool NeedsHomeHop(eRealm realm, ushort currentRegion, ushort targetRegion)
    {
        ushort home = HomeFrontier(realm);
        return home != 0 && currentRegion != targetRegion &&
               currentRegion is 1 or 100 or 200 && targetRegion is 1 or 100 or 200 &&
               currentRegion != home && targetRegion != home;
    }

    /// <summary>
    /// Owner 2026-11-03 (siege rework, phase 0): the leader writes the party's rendezvous for the passage it can board now.
    /// A bot without the ticket of a third frontier cannot board it (the vendor sells only the home one), so the target
    /// is not anchored at all: the home hop anchors once and the target follows from home. Writing the target first and the
    /// home hop right after rewrote the anchor twice per pulse (744 writes on 39 groups in one run).
    /// </summary>
    public static bool AnchorsTheTargetNow(eRealm realm, ushort currentRegion, ushort targetRegion, bool hasTicket) =>
        hasTicket || !NeedsHomeHop(realm, currentRegion, targetRegion);

    public static DbInventoryItem Ticket(GameBot bot, Passage passage) => bot.Inventory.AllItems
        .FirstOrDefault(item => item.SlotPosition >= (int)eInventorySlot.FirstBackpack &&
            item.SlotPosition <= (int)eInventorySlot.LastBackpack && item.Count > 0 && item.Id_nb == passage.Medallion);

    public static bool Ready(GameBot bot, OFTeleporter porter, Request request) =>
        bot is { IsAutonomousWorldBot: true, IsAlive: true } && bot.Realm == porter.Realm &&
        !bot.IsPlayerLedGroup && !bot.IsTemporaryGroupHelper && !bot.IsStunned && !bot.IsMezzed &&
        bot.ObjectState == GameObject.eObjectState.Active && bot.CurrentRegion == porter.CurrentRegion &&
        bot.IsWithinRadius(porter, BoardingRadius) && !bot.InCombat && !bot.IsAttacking &&
        (bot.Brain as BotBrain)?.HasAggro != true && !GameRelic.IsPlayerCarryingRelic(bot) &&
        CanBoardForObjective(bot.Realm, AutonomousObjectiveAssignments.KindFor(bot), request?.Passage) &&
        PassageMatchesSiege(bot,request?.Passage) &&
        request?.Porter == porter && request.ForceId == (bot.TempProperties.GetProperty<string>("RvrEventForce") ?? $"rvr-{bot.DatabaseID}") &&
        Ticket(bot,request.Passage) != null;

    public static bool CanBoardForObjective(eRealm realm, eAutonomousObjectiveKind objective, Passage passage)
    {
        if (passage == null) return false;
        Passage canonical = Destination(realm, passage.Region);
        if (canonical == null || canonical.Medallion != passage.Medallion) return false;
        return objective == eAutonomousObjectiveKind.RvR || passage.Medallion == "home_necklace";
    }

    public static void Depart(OFTeleporter porter)
    {
        var batch = Batches.GetOrCreateValue(porter);
        lock (batch)
        {
        batch.OpenUntil = GameLoop.GameLoopTime + BoardingWindowMilliseconds;
        if (batch.Waiting.OrdinaryCount == 0)
            foreach (var passenger in porter.GetNPCsInRadius(BoardingRadius).OfType<GameBot>()) batch.Waiting.Enqueue(passenger);
        if (batch.Active) return;
        // The ceremony authorizes this batch; spread actual transfers over
        // bounded slices rather than remove/add hundreds of actors at once.
        batch.Handled.Clear();
        batch.Active = true;
        batch.Timer = new ECSGameTimer(porter) { Callback = _ => ProcessBatch(porter, batch) };
        batch.Timer.Start(1);
        }
    }

    private static int ProcessBatch(OFTeleporter porter, BoardingBatch batch)
    {
        lock (batch) return ProcessBatchLocked(porter, batch);
    }

    private static int ProcessBatchLocked(OFTeleporter porter, BoardingBatch batch)
    {
        int processed = 0;
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
        if (porter.ObjectState != GameObject.eObjectState.Active) { batch.Waiting.Clear(); batch.OpenUntil = 0; }
        else if (batch.Waiting.Count == 0 && GameLoop.GameLoopTime < batch.OpenUntil)
        {
            batch.Handled.Clear();
            foreach (var passenger in porter.GetNPCsInRadius(BoardingRadius).OfType<GameBot>()) batch.Waiting.Enqueue(passenger);
        }
        int examined = 0;
        while (examined < 64 && System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds < SliceMilliseconds &&
            batch.Waiting.TryDequeue(out var bot, out bool priority))
        {
            examined++;
            if (batch.Handled.Contains(bot)) continue;
            var request = bot.TempProperties.GetProperty<Request>(RequestKey);
            if (priority && !HasDefenderPriority(bot, request?.Passage)) continue;
            if (!Ready(bot,porter,request)) continue;
            var group = BoardingParty(bot, request.Passage);
            bool MemberReady(GameBot member) => member.CurrentRegionID == request.Passage.Region ||
                member.TempProperties.GetProperty<Request>(RequestKey) is Request memberRequest &&
                memberRequest.Passage.Region == request.Passage.Region && Ready(member, porter, memberRequest);
            // Owner 2026-11-01: the party walks to the teleporter as one body and each member crosses on its own as soon
            // as it is ready - no batch gate, no waiting for the leader. The two deadlocks this replaces: members held at
            // the porter for a leader who was waiting for them at the rendezvous, and a partial departure that split the
            // party for good. Reassembly happens on the far side, at the arrival pad.
            group = group.Where(MemberReady).ToArray();
            if (group.Length == 0) continue;
            bot.TempProperties.RemoveProperty(BlockedSinceKey);
            int departed=0;
            for (int index = 0; index < group.Length; index++)
            {
                // A warband is checked together but even its transfers can be
                // split across ticks; one costly transfer cannot trigger seven more.
                if (SliceFull(processed, System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds))
                {
                    foreach (var pending in group.Skip(index))
                        if(priority)batch.Waiting.EnqueuePriority(pending);else batch.Waiting.Enqueue(pending);
                    break;
                }
                var member = group[index];
                batch.Handled.Add(member);
                if(member.CurrentRegionID==request.Passage.Region) continue;
                processed++;
                var ticket = Ticket(member,request.Passage);
                member.StopMovingOnPath(); member.StopMoving();
                if (!member.MoveTo(request.Passage.Location)) continue;
                departed++;
                member.Inventory.RemoveItem(ticket);
                member.TempProperties.RemoveProperty(RequestKey);
                member.TempProperties.RemoveProperty(BlockedSinceKey);
                member.ForcePathReplot();
                AutonomousBotEconomy.MarkInventoryChanged(member);
                AutonomousStuckWatchdog.MarkProgress(member,eAutonomousProgressKind.Movement);
                AutonomousBotStatusPersistence.Queue(member,true);
            }
            var log=DOL.Logging.LoggerManager.Create(typeof(AutonomousFrontierTransport));
            if(departed>0 && log.IsInfoEnabled) log.Info($"RVR_FRONTIER_DEPARTURE force={request.ForceId} count={departed} porter=\"{porter.Name}\" destination=\"{request.Passage.Location.Name}\" region={request.Passage.Region}");
            if (SliceFull(processed, System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds)) break;
        }
        }
        catch (Exception error)
        {
            DOL.Logging.LoggerManager.Create(typeof(AutonomousFrontierTransport)).Error("Frontier boarding slice failed; unprocessed passengers retained", error);
        }
        if (batch.Waiting.Count > 0) return SliceIntervalMilliseconds;
        if (GameLoop.GameLoopTime < batch.OpenUntil) return 2000;
        batch.Handled.Clear();
        lock (batch) { batch.Active = false; batch.Timer = null; }
        return 0;
    }

    public static bool WakeBoardingPorter(GameBot bot, Request request)
    {
        if (request?.Porter == null || !Ready(bot, request.Porter, request)) return false;
        if (HasDefenderPriority(bot, request.Passage))
        {
            var batch = Batches.GetOrCreateValue(request.Porter);
            lock (batch)
            {
                bool wasEmpty=batch.Waiting.Count==0;
                batch.Waiting.EnqueuePriority(bot);
                if (!batch.Active)
                {
                    batch.Handled.Clear();
                    batch.Active = true;
                    batch.Timer = new ECSGameTimer(request.Porter) { Callback = _ => ProcessBatch(request.Porter, batch) };
                    batch.Timer.Start(1);
                }
                else if(wasEmpty) batch.Timer.Start(1); // wake a two-second ordinary boarding scan
            }
            return true;
        }
        // Friendly service NPCs are not eligible for the hostile-mob wake scan.
        // Wake the actual pad only; retain its native brain, spell and schedule.
        request.Porter.OnAutonomousBotNearby();
        return true;
    }
}
