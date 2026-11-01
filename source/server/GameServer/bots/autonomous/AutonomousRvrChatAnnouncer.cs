using System.Collections.Generic;

namespace DOL.GS;

/// <summary>
/// Owner 2026-11-01: the faction chat belongs to the RvR group leaders now. Each force committed to a live event
/// announces, once per change of plan and in its leader's own voice, what it is about to do; the ambient bot chatter
/// that used to fill the channel is gone (<see cref="AutonomousBotChatCoordinator"/>) and the realm herald still
/// carries world news (<c>RealmEventNotices</c>).
///
/// Nothing here invents an objective: the text is a rendering of <see cref="AutonomousRvrEventLayer.Plan"/>, the same
/// plan the bots actually follow, so a line cannot promise a keep the force will not march on.
/// </summary>
public static class AutonomousRvrChatAnnouncer
{
    /// <summary>At most one line per realm this often: a dozen groups changing plan at once must not flood the channel.</summary>
    private const int RealmIntervalMilliseconds = 30_000;

    /// <summary>The same force does not repeat itself more often than this, even if its plan flips back and forth.</summary>
    private const int ForceIntervalMilliseconds = 4 * 60_000;

    private static readonly object Sync = new();
    private static readonly Dictionary<eRealm, long> LastRealmAnnouncement = new();
    private static readonly Dictionary<string, (string Key, long Tick)> LastForceAnnouncement = new();

    /// <summary>Called once per population turn, after the group coordinator has published its directives.</summary>
    public static void Tick()
    {
        long now = GameLoop.GameLoopTime;
        List<(string ForceId, eRealm Realm)> forces = AutonomousRvrEventLayer.ActiveForces(now);
        if (forces.Count == 0)
            return;

        HashSet<string> announced = new(System.StringComparer.Ordinal);
        foreach ((string forceId, eRealm realm) in forces)
        {
            // A force can hold both an attacker and a defender slot in two events; the first line stands.
            if (string.IsNullOrEmpty(forceId) || !announced.Add(forceId))
                continue;

            AutonomousRvrEventLayer.Plan plan = AutonomousRvrEventLayer.KeepPlan(forceId, realm, now);
            if (plan == null)
                continue;

            string text = Compose(plan);
            if (text == null)
                continue;

            GameBot leader = AutonomousBotGroupCoordinator.LeaderOfGroup(forceId);
            if (leader?.IsAlive != true || leader.ObjectState != GameObject.eObjectState.Active)
                continue;

            string key = $"{plan.Intent}|{plan.TargetId}";
            // Owner 2026-11-01: a patrol is announced when it starts, not while it runs; an ongoing siege may repeat.
            bool transitionOnly = plan.Intent == AutonomousRvrEventLayer.Intent.Roam;
            lock (Sync)
            {
                LastForceAnnouncement.TryGetValue(forceId, out (string Key, long Tick) previous);
                bool samePlan = previous.Key == key;
                if (samePlan && (transitionOnly || now - previous.Tick < ForceIntervalMilliseconds))
                    continue;
                if (LastRealmAnnouncement.TryGetValue(realm, out long realmTick) &&
                    now - realmTick < RealmIntervalMilliseconds)
                    continue;
                LastForceAnnouncement[forceId] = (key, now);
                LastRealmAnnouncement[realm] = now;
            }

            AutonomousBotChatCoordinator.BroadcastFaction(leader.Name, realm, text);
        }
    }

    /// <summary>
    /// One line per intent a follower can act on. A patrol says only that it is starting (the transition is announced,
    /// the thirty minutes that follow are not): "we are patrolling" repeated every few minutes is noise, but a patrol
    /// setting out is exactly the moment a reader could join it.
    /// </summary>
    private static string Compose(AutonomousRvrEventLayer.Plan plan)
    {
        string place = AutonomousRvrEventLayer.Place(plan.Name);
        if (string.IsNullOrWhiteSpace(place))
            return null;

        return plan.Intent switch
        {
            AutonomousRvrEventLayer.Intent.AssaultKeep =>
                $"We are forming up for an assault on {place} - join us at the rally.",
            AutonomousRvrEventLayer.Intent.AssaultRelicKeep =>
                $"We are going for the relic at {place} - the force is gathering.",
            AutonomousRvrEventLayer.Intent.DefendEvent =>
                $"We are moving to defend {place}.",
            AutonomousRvrEventLayer.Intent.HuntEnemy =>
                $"We are hunting the enemy force near {place}.",
            AutonomousRvrEventLayer.Intent.Roam =>
                $"We are setting out on patrol around {place} - join us if you are heading that way.",
            _ => null,
        };
    }
}
