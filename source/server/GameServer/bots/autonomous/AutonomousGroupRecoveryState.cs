using System;
using System.Collections.Generic;
using System.Linq;

namespace DOL.GS
{
    /// <summary>At most eight members. No world scans, movement, timers or persistence.</summary>
    public sealed class AutonomousGroupRecoveryState
    {
        public readonly record struct Member(long Id, int Deaths, bool Alive, bool Returning = false,
            bool Riding = false, bool AtRendezvous = true, bool Busy = false, bool ResourcesReady = true);
        public const long ReadySettleMilliseconds = 5_000;
        private readonly Dictionary<long, int> _deaths = new();
        private long? _readySince;
        public bool IsRegrouping { get; private set; }

        /// <summary>
        /// Owner 2026-11-03 (siege rework phase 5): a group is wiped when no member is left on its feet - each is dead or released
        /// and waiting at its frontier keep. A wiped group leaves the siege and goes back to patrol.
        /// </summary>
        public static bool IsWiped(IReadOnlyCollection<Member> members) =>
            members.Count > 0 && members.All(member => !member.Alive || member.Returning);

        // Members that were released and waiting at their keep when a regroup completed without them. The leader fetches them
        // on its own; they must not open the same regroup again a second later (run of 2026-11-03 22:26: a party looped through
        // 111 regroups in fourteen minutes because one comrade waited at the portal, and the leader never got to go for it).
        private readonly HashSet<long> _waiting = new();

        /// <summary>Remembers the living members that wait at their keep as the regroup completes without them.</summary>
        public void AcceptWaiting(IEnumerable<Member> members)
        {
            foreach (Member member in members)
                if (member.Alive && member.Returning) _waiting.Add(member.Id);
        }

        public bool HasCasualty(IEnumerable<Member> members) => members.Any(member =>
            !member.Alive || member.Returning && !_waiting.Contains(member.Id) ||
            _deaths.TryGetValue(member.Id, out int previous) && member.Deaths > previous);

        private bool HasNewDeath(IEnumerable<Member> members) => members.Any(member =>
            _deaths.TryGetValue(member.Id, out int previous) && member.Deaths > previous);

        public bool Observe(Member[] members, bool taskStarted)
        {
            // A waiting member that is back on its feet, or gone, is a normal member again.
            _waiting.RemoveWhere(id => !members.Any(member => member.Id == id && member.Alive && member.Returning));
            bool casualty = HasCasualty(members);
            bool start = taskStarted && !IsRegrouping && casualty;
            // Only a new death restarts the settle clock. A member that is dead or still returning (an RvR casualty
            // waiting at its frontier keep for the leader) is not part of the group that TryComplete settles, so it
            // must not clear the clock on every pulse: the regroup of midgard-b041b5-029 stayed open for forty minutes.
            if (IsRegrouping && casualty && HasNewDeath(members)) _readySince = null;
            foreach (Member member in members) _deaths[member.Id] = member.Deaths;
            if (start) { IsRegrouping = true; _readySince = null; }
            return start;
        }

        /// <summary>A nearby resurrection needs resource recovery, not a trip
        /// through the town rendezvous. Absorb that death only while the whole
        /// living party is together; a released or distant member still starts
        /// the ordinary regroup episode.</summary>
        public bool TryResumeLocally(Member[] members, bool together)
        {
            if (IsRegrouping || !together || members == null || members.Length < 2 ||
                members.Any(member => !member.Alive || member.Returning || member.Riding))
                return false;
            foreach (Member member in members)
                _deaths[member.Id] = member.Deaths;
            return true;
        }

        public bool TryComplete(Member[] members, long now)
        {
            if (!IsRegrouping) return false;
            bool ready = members.Length >= 2 && members.All(member => member.Alive && !member.Returning &&
                !member.Riding && member.AtRendezvous && !member.Busy && member.ResourcesReady);
            if (!ready) { _readySince = null; return false; }
            _readySince ??= now;
            if (now - _readySince.Value < ReadySettleMilliseconds) return false;
            IsRegrouping = false;
            _readySince = null;
            return true;
        }

        public static bool ResourcesReady(int health, int power, int endurance, bool usesPower) =>
            health >= 90 && (!usesPower || power >= 80) && endurance >= 80;
    }
}
