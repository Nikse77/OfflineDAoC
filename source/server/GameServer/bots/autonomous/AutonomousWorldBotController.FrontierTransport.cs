using System;
using System.Linq;
using System.Numerics;
using DOL.Database;
using DOL.GS.Scripts;

namespace DOL.GS;

public sealed partial class AutonomousWorldBotController
{
    private OFTeleporter _frontierPorter;
    private GameMerchant _medallionMerchant;
    private long _nextPorterSearch;
    private Vector3? _frontierWaitingPoint;
    private long _nextWaitingPointSearch;
    private long _nextTransportRefusalLog;

    /// <summary>
    /// Owner 2026-11-03 (diagnostics only): a frontier crossing of an RvR bot that cannot start says why, at most once a minute
    /// per bot. Run of 2026-11-03 06:15: a Midgard party that patrolled Odin was given an assault in Hadrian, moved its
    /// rendezvous across and then stood still for 18 minutes with no marker to say which test stopped it.
    /// </summary>
    private bool RefuseFrontierTransport(GameBot bot, CampDestination destination, string reason)
    {
        try
        {
            long now = GameLoop.GameLoopTime;
            if (now >= _nextTransportRefusalLog &&
                AutonomousObjectiveAssignments.Parse(bot.PersistentRecord?.ObjectiveKind) == eAutonomousObjectiveKind.RvR)
            {
                _nextTransportRefusalLog = now + 60_000;
                Log.Info($"RVR_FRONTIER_TRANSPORT_REFUSED bot={bot.Name} id={bot.DatabaseID} realm={bot.Realm} " +
                         $"group={bot.Group?.GetHashCode()} leader={bot.Group?.LivingLeader == bot} region={bot.CurrentRegionID} " +
                         $"destRegion={destination.RegionId} reason={reason} " +
                         $"porter=\"{_frontierPorter?.Name}\" merchant=\"{_medallionMerchant?.Name}\" " +
                         $"merchantDistance={(_medallionMerchant == null ? -1 : (int)bot.GetDistanceTo(_medallionMerchant))}");
            }
        }
        catch { }
        return false;
    }

    private bool TryReturnFromForeignFrontier(GameBot bot)
    {
        // PvE/services cannot walk through another realm's border gates to get
        // home. Use the same real porter/ticket boarding used by outbound RvR.
        ushort home = bot.Realm switch { eRealm.Albion => 1, eRealm.Midgard => 100, eRealm.Hibernia => 200, _ => 0 };
        if (home == 0 || bot.CurrentRegionID == home || bot.CurrentRegionID is not (1 or 100 or 200) ||
            AutonomousObjectiveAssignments.Parse(bot.PersistentRecord?.ObjectiveKind) == eAutonomousObjectiveKind.RvR ||
            GameRelic.IsPlayerCarryingRelic(bot)) return false;
        var passage = AutonomousFrontierTransport.Destination(bot.Realm, home);
        return passage != null && TryFrontierTransport(bot, new("return-home", passage.Location.Name, "frontier",
            home, passage.Location.X, passage.Location.Y, passage.Location.Z, bot.Level, false, true));
    }

    /// <summary>Beyond this distance from the medallion merchant a bot enters the frontier on foot through its own border keep.</summary>
    private const int FrontierMedallionReach = 20_000;

    private bool TryFrontierTransport(GameBot bot, CampDestination destination)
    {
        if (bot.CurrentRegionID == destination.RegionId || bot.CurrentRegionID is not (1 or 100 or 200) ||
            destination.RegionId is not (1 or 100 or 200) || GameRelic.IsPlayerCarryingRelic(bot)) return false;
        var passage = AutonomousFrontierTransport.Destination(bot.Realm,destination.RegionId);
        if (passage == null) return RefuseFrontierTransport(bot, destination, "no-passage");
        // Owner 2026-11-02: the party walks to the teleporter as one body and crosses one member at a time; whoever steps
        // through first re-anchors the rendezvous on the arrival pad, so the rest converge there and the party reassembles
        // on the far side instead of waiting for the leader at the porter.
        // Owner 2026-11-02, second pass: only the leader may re-anchor it. Every member computes its own destination, so
        // letting any of them write the shared anchor made two private intents fight over the group's region - the 043
        // party sat in Odin while the anchor alternated between "Odin Hib" and "Home Hib" every few seconds, and each
        // rewrite put the phase back to "Leader staging", which is the one phase where a member may cross alone: eight
        // bots queueing at the porter one at a time and never assembling. A follower that crosses still uses the anchor;
        // it just does not choose it.
        if (bot.Group != null && bot.Group.LivingLeader == bot)
        {
            // Owner 2026-11-02: "dopo il salto a volte vedo che i gruppi si radunano dentro al mure del keep". A home
            // keep's passage pad sits inside its walls, so the shared anchor goes to the gate front instead - the same
            // ground the release uses - and the party gathers out in the open in front of the gate.
            Point3D anchor = AutonomousFrontierRelease.TryGateFront(bot.Realm, passage.Region, out Point3D gateFront)
                ? gateFront
                : new Point3D(passage.Location.X, passage.Location.Y, passage.Location.Z);
            AutonomousBotGroupCoordinator.MoveRvRRendezvous(bot.Group, passage.Region,
                new Vector3(anchor.X, anchor.Y, anchor.Z), passage.Location.Name);
        }
        bool returningToPve = passage.Medallion == "home_necklace" &&
            AutonomousObjectiveAssignments.Parse(bot.PersistentRecord?.ObjectiveKind) != eAutonomousObjectiveKind.RvR;
        var party=AutonomousFrontierTransport.BoardingParty(bot, passage);
        if(party.Any(GameRelic.IsPlayerCarryingRelic))
        {
            foreach(var member in party) member.TempProperties.RemoveProperty(AutonomousFrontierTransport.RequestKey);
            return false;
        }
        // A full backpack must not cancel every member's approach before the
        // owner can reach the real merchant and sell ordinary vendor trash.
        if (_frontierPorter?.ObjectState != GameObject.eObjectState.Active || _frontierPorter.CurrentRegion != bot.CurrentRegion)
        {
            if (GameLoop.GameLoopTime < _nextPorterSearch) return false;
            _nextPorterSearch = GameLoop.GameLoopTime + 30_000;
            _frontierPorter = AutonomousFrontierTransport.NearestPorter(bot);
            _medallionMerchant = null;
            _frontierWaitingPoint = null;
            _nextWaitingPointSearch = 0;
        }
        if (_frontierPorter == null) return RefuseFrontierTransport(bot, destination, "no-porter-in-region");
        // Owner 2026-11-03: the walk to the realm's border keep is made as a patrol is: the leader walks, the members follow
        // it in formation, the leader waits for whoever trails and for any fight, and the party boards together.
        if (WalkWithThePartyToThePorter(bot)) return true;
        // Declare the transport intent before approaching its merchant. A
        // returning PvE bot may need its own portal-keep door to BUY the ticket.
        // Boarding still requires the real purchased medallion and all safety checks.
        bot.TempProperties.SetProperty(AutonomousFrontierTransport.RequestKey,new AutonomousFrontierTransport.Request(
            _frontierPorter,passage,bot.TempProperties.GetProperty<string>("RvrEventForce") ?? $"rvr-{bot.DatabaseID}"));
        if (AutonomousFrontierTransport.Ticket(bot,passage) == null)
        {
            if (_medallionMerchant?.ObjectState != GameObject.eObjectState.Active ||
                !_medallionMerchant.TradeItems.GetAllItems().Values.OfType<DbItemTemplate>().Any(item=>item.Id_nb==passage.Medallion))
                _medallionMerchant = _frontierPorter.GetNPCsInRadius(3000).OfType<GameMerchant>()
                    .Where(npc => (npc.Realm==bot.Realm || npc.Realm==eRealm.None) && npc.TradeItems?.GetAllItems().Values.OfType<DbItemTemplate>().Any(item=>item.Id_nb==passage.Medallion)==true)
                    .OrderBy(_frontierPorter.GetDistanceTo).FirstOrDefault();
            if (_medallionMerchant == null) return TryHomeFrontierDetour(bot, destination, "no-medallion-merchant");
            // Each realm sells frontier medallions only at its first border keep (Druim Ligen, Castle Sauvage, Svasud
            // Faste). A bot staged at the other border keep walked into the frontier through that keep's gate, as players do,
            // instead of crossing the realm for a medallion (run 24: Hibernians at Druim Cain stood still for 15 minutes
            // "collecting a frontier medallion" 110,000 units from Araisa).
            // Owner 2026-11-03: since every change of frontier goes through the teleporter there is no walk-in left, and a
            // bot more than FrontierMedallionReach from the merchant simply stood at "Waiting at the frontier teleporter" for
            // good (30 refusals in 25 minutes, merchants 40,000 to 58,000 units away). The only way is on foot to the
            // realm's border keep, in an enemy frontier or in its own, so the limit now yields only to the home hop of a
            // third frontier (NeedsHomeHop); otherwise the bot walks to the merchant by the porter.
            if (bot.GetDistanceTo(_medallionMerchant) > FrontierMedallionReach &&
                AutonomousFrontierTransport.NeedsHomeHop(bot.Realm, bot.CurrentRegionID, destination.RegionId))
                return TryHomeFrontierDetour(bot, destination, "merchant-too-far");
            // A ride (town teleporter or horse route) when clearly faster than walking to the merchant.
            if (TryBeginFasterStableRoute(bot, new(_medallionMerchant.X, _medallionMerchant.Y, _medallionMerchant.Z), _medallionMerchant.Name))
                return true;
            if (!ApproachSupplyMerchant(bot, _medallionMerchant))
            {
                SetRvrStatus(bot,"Collecting frontier medallion",destination.MonsterName,$"Walking to {_medallionMerchant.Name} for {passage.Medallion}");
                return true;
            }
            var template = _medallionMerchant.TradeItems.GetAllItems().Values.OfType<DbItemTemplate>().First(item=>item.Id_nb==passage.Medallion);
            var slot = bot.Inventory.FindFirstEmptySlot(eInventorySlot.FirstBackpack,eInventorySlot.LastBackpack);
            if (slot == eInventorySlot.Invalid)
            {
                // A full backpack after RvR must not force a walk through enemy
                // gates. Sell one ordinary disposable item at this real merchant;
                // never delete an item or grant a free ticket/teleport.
                var trash = AutonomousBotEconomy.FindVendorTrashCandidate(bot);
                if (trash != null)
                    AutonomousBotEconomy.TrySellToVendor(bot, _medallionMerchant, trash, out _);
                SetRvrStatus(bot, "Making room for frontier medallion", destination.MonsterName,
                    "At the real merchant; preserving equipped items and protected upgrades");
                return true;
            }
            if (slot==eInventorySlot.Invalid || template.Price<0 || template.Price>0 && !AutonomousBotEconomy.TrySpend(bot.DatabaseID,template.Price))
                return false; // Existing legal dungeon itinerary remains available.
            var ticket = GameInventoryItem.Create(template);
            if (ticket == null || !bot.Inventory.AddItem(slot,ticket))
            {
                if(template.Price>0) AutonomousBotEconomy.AddMoney(bot.DatabaseID,template.Price);
                return false;
            }
            AutonomousBotEconomy.MarkInventoryChanged(bot);
            AutonomousBotStatusPersistence.Queue(bot,true);
        }
        // Ordinary warbands retain their shared departure. Committed siege
        // responders board independently; defenders receive a bounded priority slice.
        // Owner 2026-11-02: the waiting point is chosen once and kept while the porter lives, so a traveller parked by
        // an older rule would stand out of the pad's reach for the rest of the run. Drop it when it cannot board from
        // there; a party member without a request keeps its spot in front of the gate.
        bool boardingNow = bot.TempProperties.GetProperty<AutonomousFrontierTransport.Request>(
            AutonomousFrontierTransport.RequestKey)?.Porter == _frontierPorter;
        if (boardingNow && _frontierWaitingPoint.HasValue &&
            !AutonomousFrontierTransport.InsideBoardingReach(_frontierWaitingPoint.Value,
                new(_frontierPorter.X, _frontierPorter.Y, _frontierPorter.Z)))
        {
            _frontierWaitingPoint = null;
            _nextWaitingPointSearch = 0;
        }
        if (!_frontierWaitingPoint.HasValue && GameLoop.GameLoopTime >= _nextWaitingPointSearch)
        {
            _nextWaitingPointSearch = GameLoop.GameLoopTime + 10_000;
            if (AutonomousFrontierTransport.TryWaitingPoint(bot, _frontierPorter, out var waiting))
                _frontierWaitingPoint = waiting;
        }
        if (!_frontierWaitingPoint.HasValue)
        {
            SetRvrStatus(bot,"Finding teleporter waiting space",destination.MonsterName,"Waiting for a reachable spot clear of the teleporter");
            return true;
        }
        if (Vector3.Distance(new(bot.X,bot.Y,bot.Z),_frontierWaitingPoint.Value) > 40)
            IssuePath(bot,_frontierWaitingPoint.Value);
        else { bot.StopMovingOnPath(); bot.StopMoving(); }
        AutonomousFrontierTransport.WakeBoardingPorter(bot,
            bot.TempProperties.GetProperty<AutonomousFrontierTransport.Request>(AutonomousFrontierTransport.RequestKey));
        SetRvrStatus(bot,"Boarding frontier teleporter",destination.MonsterName,
            AutonomousFrontierTransport.HasDefenderPriority(bot, passage)
                ? $"At {_frontierPorter.Name}; priority defense departure to {passage.Location.Name}"
                : AutonomousFrontierTransport.HasCommittedSiegePassage(bot, passage)
                    ? $"At {_frontierPorter.Name}; boarding independently for the siege at the next departure to {passage.Location.Name}"
                    : $"At {_frontierPorter.Name}; waiting for the warband and the native departure to {passage.Location.Name}");
        return true;
    }

    /// <summary>
    /// Owner 2026-11-02: the medallion of a third frontier is not for sale where the bot stands, so the passage it
    /// can board now is the one leading home; the next turn boards the target from there. Two teleporters, never a
    /// walk and never the stalled "No legal region connection" rendezvous that left a party standing in Emain with
    /// an objective in Hadrian's Wall.
    /// </summary>
    private bool _frontierWalkWaiting;

    /// <summary>Within this distance of the porter the walk is over and the party boards (see BoardingParty).</summary>
    private const int PartyWalkEndRadius = 2_000;

    /// <summary>
    /// Owner 2026-11-03: "vorrei che il capo andasse al raduno e che i membri lo seguissero come fanno quando pattugliano ...
    /// devono aspettarsi, restare uniti, combattere se necessario". While the leader of an RvR party is still far from the
    /// porter, a member follows it in formation instead of walking to the merchant on its own, and the leader stops when a
    /// member trails (600 to stop, 300 to go on, the compact radii of the patrol) or when any member fights. Once the leader
    /// is within PartyWalkEndRadius of the porter, or in another region, everybody boards as before.
    /// Returns true when this bot's turn was spent walking with, or waiting for, the party.
    /// </summary>
    private bool WalkWithThePartyToThePorter(GameBot bot)
    {
        if (_groupDirective?.IsDynamic != true || _groupDirective.ObjectiveKind != eAutonomousObjectiveKind.RvR ||
            bot.Group == null || _frontierPorter == null)
            return false;
        if (bot.Group.LivingLeader is not GameBot leader || !leader.IsAlive ||
            leader.CurrentRegionID != bot.CurrentRegionID || leader.GetDistanceTo(_frontierPorter) <= PartyWalkEndRadius)
        {
            _frontierWalkWaiting = false;
            return false;
        }
        if (leader != bot)
            return FollowDynamicGroupLeader(bot, _groupDirective);

        long now = GameLoop.GameLoopTime;
        GameBot[] members = bot.Group.GetMembersInTheGroup().OfType<GameBot>().ToArray();
        float farthest = 0;
        foreach (GameBot member in members)
        {
            if (member == bot || !member.IsAlive || member.CurrentRegionID != bot.CurrentRegionID) continue;
            float distance = member.GetDistanceTo(bot);
            if (AutonomousGroupMemberStall.Release(member, bot, distance, AutonomousGroupPace.CompactTrailRadius, now)) continue;
            farthest = Math.Max(farthest, distance);
        }
        bool fighting = AutonomousGroupCombat.AnyHoldsGroup(members, bot, now);
        bool move = AutonomousGroupPace.Decide(farthest, _frontierWalkWaiting, compact: true, partyFighting: fighting) ==
                    AutonomousGroupPace.Decision.FullSpeed;
        _frontierWalkWaiting = !move;
        if (move) return false;
        bot.StopMovingOnPath();
        bot.StopMoving();
        SetRvrStatus(bot, "Waiting for group members", "Walking to the border keep",
            fighting ? "A member of the party is fighting; holding the walk to the frontier teleporter"
                     : "Holding the walk to the frontier teleporter until every living bot is back in formation");
        return true;
    }

    private bool TryHomeFrontierDetour(GameBot bot, CampDestination destination, string cause)
    {
        if (!AutonomousFrontierTransport.NeedsHomeHop(bot.Realm, bot.CurrentRegionID, destination.RegionId))
            return RefuseFrontierTransport(bot, destination, cause + "+no-home-hop");
        AutonomousFrontierTransport.Passage home =
            AutonomousFrontierTransport.Destination(bot.Realm, AutonomousFrontierTransport.HomeFrontier(bot.Realm));
        if (home == null) return RefuseFrontierTransport(bot, destination, cause + "+no-home-passage");
        return TryFrontierTransport(bot, new("frontier-passage", home.Location.Name, "frontier",
            home.Region, home.Location.X, home.Location.Y, home.Location.Z, bot.Level, false, true));
    }
}
