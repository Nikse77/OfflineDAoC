using System;
using System.Collections.Generic;
using System.Linq;

namespace DOL.GS
{
    /// <summary>
    /// Emblems for the guilds the autonomous gamebots found (owner 2026-11-04): random, valid, and never the same as another
    /// guild's. A standalone generator: it knows nothing of guilds, the caller says which emblem numbers are taken.
    /// The emblem number is the client's: <c>logo &lt;&lt; 9 | pattern &lt;&lt; 7 | primary &lt;&lt; 3 | secondary</c>
    /// (see source/new-client/godot/game/emblems.gd and pipeline/build_emblems.py): 4 patterns, 16 dark primary colours, 8 light
    /// secondary colours, and a logo from the client's insignia list. The logos shown by the realm's own emblem editor are the
    /// ones used here (Albion B_, Midgard N_, Hibernia H_ files); the three "guard" logos (121-123) are left out.
    /// 0 means no emblem, so a generated emblem is never 0.
    /// </summary>
    public static class AutonomousGuildEmblems
    {
        public const int Patterns = 4;
        public const int PrimaryColours = 16;
        public const int SecondaryColours = 8;

        private static readonly int[] AlbionLogos = Ranges((1, 40), (124, 148), (199, 217));
        private static readonly int[] MidgardLogos = Ranges((41, 80), (174, 198), (218, 236));
        private static readonly int[] HiberniaLogos = Ranges((81, 120), (149, 173), (237, 255));

        /// <summary>The 16 dark primary colours (patterns.csv set 1), red/green/blue.</summary>
        private static readonly (int R, int G, int B)[] PrimaryRgb =
        [
            (169, 0, 3), (89, 0, 0), (99, 4, 96), (68, 14, 98), (0, 0, 168), (12, 54, 111), (9, 78, 91), (56, 121, 137),
            (6, 102, 39), (7, 74, 51), (75, 89, 10), (143, 114, 0), (106, 78, 20), (102, 58, 6), (75, 75, 75), (116, 0, 68),
        ];

        /// <summary>The 8 light secondary colours (patterns.csv set 2), red/green/blue.</summary>
        private static readonly (int R, int G, int B)[] SecondaryRgb =
        [
            (205, 200, 158), (174, 161, 134), (154, 154, 154), (235, 232, 97), (228, 181, 84), (135, 196, 130), (143, 162, 204),
            (152, 136, 165),
        ];

        /// <summary>Owner 2026-11-04: the two colours of an emblem must differ in brightness by at least this much (0-255 scale).</summary>
        public const int MinimumContrast = 70;

        private static double Brightness((int R, int G, int B) colour) => 0.2126 * colour.R + 0.7152 * colour.G + 0.0722 * colour.B;

        /// <summary>The brightness difference between a primary and a secondary colour.</summary>
        public static double Contrast(int primary, int secondary) =>
            Math.Abs(Brightness(SecondaryRgb[secondary & 7]) - Brightness(PrimaryRgb[primary & 15]));

        /// <summary>The (primary, secondary) pairs that contrast enough.</summary>
        private static readonly (int Primary, int Secondary)[] ColourPairs =
            Enumerable.Range(0, PrimaryColours).SelectMany(primary => Enumerable.Range(0, SecondaryColours)
                .Where(secondary => Contrast(primary, secondary) >= MinimumContrast).Select(secondary => (primary, secondary))).ToArray();

        public static int Encode(int logo, int pattern, int primary, int secondary) =>
            (logo << 9) | ((pattern & 3) << 7) | ((primary & 15) << 3) | (secondary & 7);

        public static (int Logo, int Pattern, int Primary, int Secondary) Decode(int emblem) =>
            (emblem >> 9, (emblem >> 7) & 3, (emblem >> 3) & 15, emblem & 7);

        /// <summary>The logo numbers a guild of this realm may carry.</summary>
        public static IReadOnlyList<int> LogosOf(eRealm realm) => realm switch
        {
            eRealm.Midgard => MidgardLogos,
            eRealm.Hibernia => HiberniaLogos,
            _ => AlbionLogos,
        };

        /// <summary>True when the emblem is one this generator could make for this realm.</summary>
        public static bool IsValid(eRealm realm, int emblem)
        {
            if (emblem <= 0) return false;
            var (logo, pattern, primary, secondary) = Decode(emblem);
            return LogosOf(realm).Contains(logo) && Encode(logo, pattern, primary, secondary) == emblem &&
                   Contrast(primary, secondary) >= MinimumContrast;
        }

        /// <summary>How many different emblems a realm can have.</summary>
        public static int CountFor(eRealm realm) => LogosOf(realm).Count * Patterns * ColourPairs.Length;

        /// <summary>A random emblem for a new guild of this realm that <paramref name="isTaken"/> does not know; 0 when all are taken.</summary>
        public static int Generate(eRealm realm, Func<int, bool> isTaken, Random random = null)
        {
            random ??= Random.Shared;
            isTaken ??= _ => false;
            IReadOnlyList<int> logos = LogosOf(realm);
            for (int attempt = 0; attempt < 500; attempt++)
            {
                var (primary, secondary) = ColourPairs[random.Next(ColourPairs.Length)];
                int emblem = Encode(logos[random.Next(logos.Count)], random.Next(Patterns), primary, secondary);
                if (!isTaken(emblem))
                    return emblem;
            }
            // Nearly everything is taken: walk all combinations from a random start so the pick stays unbiased.
            int total = CountFor(realm);
            int start = random.Next(total);
            for (int step = 0; step < total; step++)
            {
                int index = (start + step) % total;
                var (primary, secondary) = ColourPairs[index % ColourPairs.Length];
                int emblem = Encode(logos[index / (Patterns * ColourPairs.Length)], index / ColourPairs.Length % Patterns, primary, secondary);
                if (!isTaken(emblem))
                    return emblem;
            }
            return 0;
        }

        private static int[] Ranges(params (int First, int Last)[] ranges) =>
            ranges.SelectMany(range => Enumerable.Range(range.First, range.Last - range.First + 1)).ToArray();
    }
}
