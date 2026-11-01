using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace DOL.GS
{
    /// <summary>
    /// Owner 2026-11-01: a party that sets out for its objective moves as one body. The leader waits for the members that
    /// left with it (through <see cref="AutonomousGroupPace"/>), and a member that never left - still at the muster keep,
    /// or in another region - stops holding the party back and is taken out of the group instead.
    ///
    /// The roster is taken once, the first time the party is really on its way, and it is what the waiting logic reads:
    /// without it one member left behind in another region reported an infinite distance and the leader waited for it
    /// forever while the rest of the party stood still.
    /// </summary>
    public static class AutonomousGroupMarchRoster
    {
        /// <summary>A member this close to the leader when the roster is taken was mustered with it and counts as departed.</summary>
        public const int MusterRadius = 600;

        /// <summary>A member that has not joined the march within this time is taken out of the group.</summary>
        public const long GraceMilliseconds = 60_000;

        private sealed class State
        {
            public readonly HashSet<int> Departed = new();
            public long StartedTick;
            public bool Pruned;
        }

        private static readonly ConditionalWeakTable<Group, State> Rosters = new();

        /// <summary>
        /// Takes the roster the first time the party is really on its way - the leader is moving and at least one member
        /// travels with it - then removes the members that never joined. Called from the leader's own turn, so it never
        /// runs during the muster or in a player event.
        /// </summary>
        public static void Note(GameBot leader, bool marching, long now)
        {
            if (leader?.Group == null)
                return;

            if (!marching)
            {
                Rosters.Remove(leader.Group);
                return;
            }

            State state = Rosters.GetOrCreateValue(leader.Group);
            if (state.StartedTick == 0)
            {
                // The party leaves when the leader walks; a leader still gathering has not departed yet.
                if (!leader.IsMoving)
                    return;

                foreach (GameLiving member in leader.Group.GetMembersInTheGroup())
                    if (member is GameBot mate && mate != leader && mate.IsAlive && mate.IsAutonomousWorldBot &&
                        !mate.IsTemporaryGroupHelper && mate.CurrentRegionID == leader.CurrentRegionID &&
                        mate.GetDistanceTo(leader) <= MusterRadius)
                        state.Departed.Add(mate.ObjectID);

                if (state.Departed.Count == 0)
                {
                    Rosters.Remove(leader.Group);
                    return;
                }

                state.Departed.Add(leader.ObjectID);
                state.StartedTick = now;
                return;
            }

            if (state.Pruned || now - state.StartedTick < GraceMilliseconds)
                return;

            state.Pruned = true;
            List<GameBot> left = new();
            foreach (GameLiving member in leader.Group.GetMembersInTheGroup())
                if (member is GameBot mate && mate != leader && mate.IsAutonomousWorldBot &&
                    !mate.IsTemporaryGroupHelper && !state.Departed.Contains(mate.ObjectID))
                    left.Add(mate);

            foreach (GameBot mate in left)
                leader.Group.RemoveMember(mate, retainSingleRemainingMember: true);
        }

        /// <summary>
        /// Whether a member is one the party is waiting for. True while no roster exists, so a party that has not set out
        /// yet keeps its old behaviour.
        /// </summary>
        public static bool IsDeparted(Group group, GameBot member)
        {
            if (group == null || member == null || !Rosters.TryGetValue(group, out State state) || state.StartedTick == 0)
                return true;
            return state.Departed.Contains(member.ObjectID);
        }

        /// <summary>A follower turn refreshes this mark; when it stops arriving the party is no longer marching.</summary>
        public const long FormationMarkMilliseconds = 5_000;

        private sealed class MarchMark
        {
            public long Tick;
        }

        private static readonly ConditionalWeakTable<GameBot, MarchMark> Marching = new();

        /// <summary>Called by the group travel branch for every member that moves in formation toward the objective.</summary>
        public static void MarkMarching(GameBot bot, long now)
        {
            if (bot == null)
                return;
            Marching.GetOrCreateValue(bot).Tick = now;
        }

        /// <summary>
        /// True while the bot walks in a party formation. Used to keep the march from stopping for upkeep: the buffers
        /// fell behind because a cast costs the whole party a halt, and the party walked on without them.
        /// </summary>
        public static bool IsMarching(GameBot bot, long now) =>
            bot != null && Marching.TryGetValue(bot, out MarchMark mark) && now - mark.Tick < FormationMarkMilliseconds;
    }
}
