using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace DOL.GS
{
    /// <summary>
    /// Per-thread counts and time of navmesh queries and route-planning steps within one AI turn,
    /// appended to the "Long NpcService" warning so a slow turn names what it spent its time on.
    /// Reset before each brain turn; costs two timestamps per counted call.
    /// </summary>
    public static class NavQueryProfile
    {
        public enum Kind { PathStraight, ClosestPoint, OtherQuery, ZoneStep, Corridor, Road, Count }

        [ThreadStatic] private static int[] _calls;
        [ThreadStatic] private static long[] _ticks;
        [ThreadStatic] private static List<(string Site, long Ticks)> _sites;
        [ThreadStatic] private static List<(string Name, long Ticks)> _sections;
        [ThreadStatic] private static string _scope;
        [ThreadStatic] private static Dictionary<string, (int Calls, long Ticks)> _scopes;

        public static void Reset()
        {
            _sites?.Clear();
            _sections?.Clear();
            _scopes?.Clear();
            _scope = null;
            if (_calls == null) { _calls = new int[(int)Kind.Count]; _ticks = new long[(int)Kind.Count]; return; }
            Array.Clear(_calls);
            Array.Clear(_ticks);
        }

        /// <summary>Slow zone-step planning is credited to the code that asked for it ("zoneStepBy=" in the
        /// summary): 13 callers plan zone steps and the slow turns only said "ZoneStep" (2026-10-16).</summary>
        public const long SiteThresholdMilliseconds = 20;

        public static void NoteSite(string file, string member, long started)
        {
            if (_calls == null) return;
            long ticks = Stopwatch.GetTimestamp() - started;
            if (ticks * 1000 < SiteThresholdMilliseconds * Stopwatch.Frequency) return;
            string site = System.IO.Path.GetFileNameWithoutExtension(file) + "." + member;
            _sites ??= new List<(string, long)>();
            for (int i = 0; i < _sites.Count; i++)
                if (_sites[i].Site == site) { _sites[i] = (site, _sites[i].Ticks + ticks); return; }
            _sites.Add((site, ticks));
        }

        /// <summary>
        /// Times a named stretch of a bot's turn ("sectionsBy=" in the summary, inclusive of anything nested inside it, shown when it took
        /// <see cref="SiteThresholdMilliseconds"/> or more). Diagnostics only (2026-10-30): 21% of the long turns spent nothing on
        /// pathfinding and the Realm Exchange turns reached 737 ms, and the summary could not say where.
        /// <code>using var _section = NavQueryProfile.Section("Name");</code>
        /// </summary>
        public static SectionTimer Section(string name) => new(name, Stopwatch.GetTimestamp());

        public readonly struct SectionTimer : IDisposable
        {
            private readonly string _name;
            private readonly long _started;

            public SectionTimer(string name, long started) { _name = name; _started = started; }
            public void Dispose() => NoteSection(_name, _started);
        }

        private static void NoteSection(string name, long started)
        {
            if (_calls == null) return;
            long ticks = Stopwatch.GetTimestamp() - started;
            if (ticks * 1000 < SiteThresholdMilliseconds * Stopwatch.Frequency) return;
            _sections ??= new List<(string, long)>();
            for (int i = 0; i < _sections.Count; i++)
                if (_sections[i].Name == name) { _sections[i] = (name, _sections[i].Ticks + ticks); return; }
            _sections.Add((name, ticks));
        }

        public static long Start() => Stopwatch.GetTimestamp();

        /// <summary>
        /// Names the code that is asking for corridor checks, so a slow turn says who spent its pathfinding time
        /// ("corridorBy=" in the summary; 2026-10-29: one bot made 1,409 checks, 4.4 s, in a turn named only "Searching the camp").
        /// The outermost scope wins: the resolver that loops over candidates, not the leaf that asks about one of them.
        /// Returns the scope to give back to <see cref="Leave"/>.
        /// </summary>
        public static string Enter(string site)
        {
            string previous = _scope;
            _scope ??= site;
            return previous;
        }

        public static void Leave(string previous) => _scope = previous;

        public static void Stop(Kind kind, long started)
        {
            if (_calls == null) return;
            long elapsed = Stopwatch.GetTimestamp() - started;
            _calls[(int)kind]++;
            _ticks[(int)kind] += elapsed;
            if (kind == Kind.Corridor)
            {
                _scopes ??= new Dictionary<string, (int, long)>();
                string site = _scope ?? "(unnamed)";
                _scopes[site] = _scopes.TryGetValue(site, out var total) ? (total.Calls + 1, total.Ticks + elapsed) : (1, elapsed);
            }
        }

        public static string Summary()
        {
            if (_calls == null) return string.Empty;
            var text = new StringBuilder();
            for (int i = 0; i < (int)Kind.Count; i++)
            {
                if (_calls[i] == 0) continue;
                text.Append(' ').Append((Kind)i).Append('=').Append(_calls[i]).Append('/')
                    .Append((_ticks[i] * 1000 / Stopwatch.Frequency).ToString()).Append("ms");
            }
            if (_scopes is { Count: > 0 })
            {
                text.Append(" corridorBy=");
                int shown = 0;
                foreach (var pair in _scopes.OrderByDescending(pair => pair.Value.Ticks).Take(3))
                    text.Append(shown++ > 0 ? "," : "").Append(pair.Key).Append(':').Append(pair.Value.Calls).Append('/')
                        .Append((pair.Value.Ticks * 1000 / Stopwatch.Frequency).ToString()).Append("ms");
            }
            if (_sites is { Count: > 0 })
            {
                _sites.Sort((a, b) => b.Ticks.CompareTo(a.Ticks));
                text.Append(" zoneStepBy=");
                for (int i = 0; i < Math.Min(3, _sites.Count); i++)
                    text.Append(i > 0 ? "," : "").Append(_sites[i].Site).Append(':')
                        .Append((_sites[i].Ticks * 1000 / Stopwatch.Frequency).ToString()).Append("ms");
            }
            if (_sections is { Count: > 0 })
            {
                _sections.Sort((a, b) => b.Ticks.CompareTo(a.Ticks));
                text.Append(" sectionsBy=");
                for (int i = 0; i < Math.Min(5, _sections.Count); i++)
                    text.Append(i > 0 ? "," : "").Append(_sections[i].Name).Append(':')
                        .Append((_sections[i].Ticks * 1000 / Stopwatch.Frequency).ToString()).Append("ms");
            }
            return text.Length == 0 ? string.Empty : " nav:" + text;
        }
    }
}
