using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace DOL.GS
{
    /// <summary>At most eight entries, updated by the existing group pulse; no timers or I/O.</summary>
    public sealed class AutonomousRendezvousAttendance
    {
        public const long TimeoutMilliseconds = 15 * 60_000L;
        /// <summary>Owner 2026-10-31: the time an RvR group has to gather.</summary>
        public const long RvrTimeoutMilliseconds = 20 * 60_000L;
        private readonly long _timeout;

        public AutonomousRendezvousAttendance(long timeoutMilliseconds = TimeoutMilliseconds) => _timeout = timeoutMilliseconds;
        public const int ArrivalRadius = 38;
        public const int OutdoorArrivalRadius = 80;
        public const int EndpointTolerance = 8;
        public static int ApproachRadius(bool tight) => Radius(tight) / 2;
        public static int Radius(bool tight) => tight ? ArrivalRadius : OutdoorArrivalRadius;
        public static bool IsAtSlot(Vector3 position, Vector3 slot, bool tight = true)
        {
            int radius = Radius(tight);
            return Vector3.DistanceSquared(position, slot) <= radius * radius;
        }

        /// <summary>
        /// Owner 2026-11-03: an RvR party member follows its leader like a companion and stays where the follow order puts it,
        /// not on its own slot. A slot sits on a ring of 88 units around the rendezvous and the arrival radius is 80, so a
        /// member within this distance of a leader that stands at its slot has arrived.
        /// </summary>
        public const int FollowArrivalRadius = 200;

        public static bool FollowsLeader(double distanceToLeader, bool leaderAtSlot) =>
            leaderAtSlot && distanceToLeader <= FollowArrivalRadius;
        /// <summary>Deaths on the way that make a member a no-show at once (in-game check 2026-10-17: a
        /// level 1-3 group died about 70 times near its meetup point during the full fifteen-minute wait).</summary>
        public const int MeetupDeathLimit = 3;
        private readonly Dictionary<long, int> _deathsAtStart = new();
        private readonly HashSet<long> _deathLimited = new();
        private readonly Dictionary<long, long> _due = new();
        private readonly HashSet<long> _arrived = new();
        private readonly Dictionary<long, DateTime> _dueUtc = new();
        public long Revision { get; private set; }

        public void Add(long memberId, long now, DateTime? utcNow = null)
        {
            if (_due.TryAdd(memberId, now + _timeout))
            {
                _dueUtc[memberId] = (utcNow ?? DateTime.UtcNow).AddMilliseconds(_timeout);
                Revision++;
            }
        }

        // Stable UTC value for the launcher's local countdown. No per-second writes.
        public DateTime? DeadlineUtc(long memberId) => !_arrived.Contains(memberId) &&
            _dueUtc.TryGetValue(memberId, out DateTime due) ? due : null;

        /// <summary>A proven disconnected route should get a short chance to
        /// recover, not consume the entire ordinary fifteen-minute meetup.
        /// Never extend a deadline that is already closer.</summary>
        public void LimitDeadline(long memberId, long now, long graceMilliseconds, DateTime? utcNow = null)
        {
            Add(memberId, now, utcNow);
            long limited = Math.Min(_due[memberId], now + Math.Max(0, graceMilliseconds));
            if (limited >= _due[memberId]) return;
            _due[memberId] = limited;
            _dueUtc[memberId] = (utcNow ?? DateTime.UtcNow).AddMilliseconds(Math.Max(0, limited - now));
            Revision++;
        }

        public bool Observe(long memberId, long now, bool atRendezvous)
        {
            Add(memberId, now);
            if (atRendezvous && _arrived.Add(memberId))
                Revision++;
            else if (!atRendezvous && _arrived.Remove(memberId))
                Revision++;
            return !_arrived.Contains(memberId) && now >= _due[memberId];
        }

        /// <summary>
        /// True once, when a member has died <see cref="MeetupDeathLimit"/> times since its wait began
        /// (deathCount is its lifetime death count). Its deadline is then now: dying on the way again
        /// and again is a no-show, not fifteen more minutes of corpse runs.
        /// </summary>
        public bool ReachedDeathLimit(long memberId, int deathCount, long now, DateTime? utcNow = null)
        {
            if (!_deathsAtStart.TryGetValue(memberId, out int start))
            {
                _deathsAtStart[memberId] = deathCount;
                return false;
            }
            if (deathCount - start < MeetupDeathLimit || !_deathLimited.Add(memberId))
                return false;
            LimitDeadline(memberId, now, 0, utcNow);
            return true;
        }

        public int DeathsSinceStart(long memberId, int deathCount) =>
            _deathsAtStart.TryGetValue(memberId, out int start) ? Math.Max(0, deathCount - start) : 0;

        public long WaitedMilliseconds(long memberId, long now) =>
            _due.TryGetValue(memberId, out long due) ? now - (due - _timeout) : 0;

        /// <summary>
        /// Starts one new, shared attendance window after the coordinator has
        /// changed the rendezvous or rebuilt formation slots. Keeping the old
        /// expired deadlines would immediately expel members that were already
        /// present but were assigned a different slot by that rebuild.
        /// </summary>
        public void Rebase(IEnumerable<long> memberIds, long now, DateTime? utcNow = null)
        {
            _due.Clear();
            _arrived.Clear();
            _dueUtc.Clear();
            _deathsAtStart.Clear();
            _deathLimited.Clear();
            Revision++;
            foreach (long memberId in memberIds)
                Add(memberId, now, utcNow);
        }

        /// <summary>A smaller party gets new formation slots, but repeated
        /// no-shows must not buy a fresh fifteen-minute wait for everyone else.
        /// Give a member with an imminent old deadline two minutes to reach
        /// its reassigned slot; preserve all later original deadlines.</summary>
        public void RebaseAfterRosterReduction(IEnumerable<long> memberIds, long now, DateTime? utcNow = null)
        {
            var previous = new Dictionary<long, long>(_due);
            var deathsAtStart = new Dictionary<long, int>(_deathsAtStart);   // the same wait goes on: keep counting
            DateTime utc = utcNow ?? DateTime.UtcNow;
            Rebase(memberIds, now, utc);
            foreach (long memberId in _due.Keys)
                if (deathsAtStart.TryGetValue(memberId, out int start))
                    _deathsAtStart[memberId] = start;
            foreach (long memberId in _due.Keys.ToArray())
            {
                if (!previous.TryGetValue(memberId, out long formerDeadline)) continue;
                long deadline = Math.Min(_due[memberId], Math.Max(formerDeadline, now + 2 * 60_000L));
                _due[memberId] = deadline;
                _dueUtc[memberId] = utc.AddMilliseconds(deadline - now);
            }
        }

        public void Reset()
        {
            _due.Clear();
            _arrived.Clear();
            _dueUtc.Clear();
            _deathsAtStart.Clear();
            _deathLimited.Clear();
            Revision++;
        }
    }
}
