using System;
using System.Collections.Generic;
using System.Linq;

namespace DOL.GS
{
    /// <summary>
    /// Owner 2026-11-03 (siege rework, phase 1): the attacking groups of one assault, in the order they joined. The group
    /// that opened the assault is the organizer and leads; when it is gone the command passes to the first group that had
    /// aggregated (order of joining, not the strongest). Pure bookkeeping, no effect on how anybody moves yet.
    /// </summary>
    public sealed class AutonomousRvrCoalition
    {
        public const int MinimumGroups = 2;
        public const int MaximumGroups = 8;
        public const int MinimumBots = 16;
        /// <summary>Owner 2026-11-03: a group smaller than this does not count as a group of the coalition; a pair or trio cannot aggregate. A lone bot (no group) may still be recruited and merged into a party by the event layer, which then counts.</summary>
        public const int MinimumGroupMembers = 4;

        public enum Change { None, Joined, Left, Promoted }

        private readonly List<string> _groups = new();

        /// <summary>The group that leads the coalition: the organizer, or the first remaining group after it left.</summary>
        public string Leader => _groups.Count == 0 ? null : _groups[0];
        public IReadOnlyList<string> Groups => _groups;
        public int Count => _groups.Count;

        /// <summary>Appends a group once; the first group ever added is the organizer. False when full or already in.</summary>
        public bool Join(string groupId)
        {
            if (string.IsNullOrEmpty(groupId) || _groups.Contains(groupId, StringComparer.Ordinal) || _groups.Count >= MaximumGroups)
                return false;
            _groups.Add(groupId);
            return true;
        }

        /// <summary>Removes a group; true when it was the leader, so the next group in joining order now leads.</summary>
        public bool Leave(string groupId, out bool promoted)
        {
            promoted = false;
            int index = _groups.FindIndex(id => string.Equals(id, groupId, StringComparison.Ordinal));
            if (index < 0) return false;
            _groups.RemoveAt(index);
            promoted = index == 0 && _groups.Count > 0;
            return true;
        }

        /// <summary>
        /// Siege rework phase 3: the id of the bot that leads the march. It is the leader of the coalition's first group (the
        /// organizer) that has one among the candidates, else of the next group in joining order; null when no group has its
        /// leader among them, and the caller falls back to the main-body election.
        /// </summary>
        public static long? PickMarchLeader(IEnumerable<string> order, IEnumerable<(string Force, long BotId, bool IsGroupLeader)> candidates)
        {
            var leaders = candidates.Where(candidate => candidate.IsGroupLeader).ToArray();
            foreach (string force in order)
                foreach (var leader in leaders)
                    if (string.Equals(leader.Force, force, StringComparison.Ordinal))
                        return leader.BotId;
            return null;
        }

        /// <summary>
        /// Owner 2026-11-03: the army does not march before the organizer has arrived - its group leader stands at the muster.
        /// True when there is no organizer to wait for (no group of four yet) or its leader is among the mustered bots. The muster
        /// deadline still sends the army whatever happens.
        /// </summary>
        public static bool OrganizerArrived(string organizerGroup, IEnumerable<(string Force, bool IsGroupLeader)> mustered) =>
            string.IsNullOrEmpty(organizerGroup) ||
            mustered.Any(entry => entry.IsGroupLeader && string.Equals(entry.Force, organizerGroup, StringComparison.Ordinal));

        /// <summary>Whether the coalition is large enough to march (2 groups and 16 bots at least).</summary>
        public static bool IsViable(int groups, int bots) => groups >= MinimumGroups && bots >= MinimumBots;

        /// <summary>
        /// Brings the list in line with the groups now assigned to the attack (insertion order of <paramref name="current"/>
        /// breaks no ties: groups already known keep their place, new ones go to the end). Returns what changed.
        /// </summary>
        public List<(Change Kind, string Group)> Reconcile(IEnumerable<string> current)
        {
            var changes = new List<(Change, string)>();
            string[] order = current.Distinct(StringComparer.Ordinal).ToArray();
            var wanted = new HashSet<string>(order, StringComparer.Ordinal);
            foreach (string gone in _groups.Where(id => !wanted.Contains(id)).ToArray())
            {
                Leave(gone, out bool promoted);
                changes.Add((Change.Left, gone));
                if (promoted) changes.Add((Change.Promoted, Leader));
            }
            foreach (string id in order)
                if (Join(id)) changes.Add((Change.Joined, id));
            return changes;
        }
    }
}
