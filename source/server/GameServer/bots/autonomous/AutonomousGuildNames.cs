using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DOL.GS
{
    /// <summary>
    /// Names for the guilds the autonomous gamebots found (owner 2026-11-04): English, per realm, mostly a title with a place or
    /// an animal ("Order of the Silver Hawk", "Clan Ironfang", "Wardens of Avalon"). A standalone generator: it knows nothing of
    /// guilds, the caller says which names are taken. Names use letters and spaces only (the rule of /guild create) and stay
    /// within <see cref="MaxLength"/> characters.
    /// </summary>
    public static class AutonomousGuildNames
    {
        public const int MaxLength = 30;

        private static readonly Regex Allowed = new(@"^[a-zA-Z ]+$", RegexOptions.Compiled);

        private static readonly string[] Colours =
        [
            "Silver", "Golden", "Crimson", "Iron", "Black", "White", "Grey", "Emerald", "Azure", "Scarlet", "Ashen", "Amber",
            "Frost", "Storm", "Shadow", "Bright", "Wild", "Stone", "Ivory", "Burning",
        ];

        private static readonly string[] AlbionAnimals =
            ["Hawk", "Lion", "Stag", "Raven", "Wolf", "Boar", "Falcon", "Swan", "Hound", "Eagle", "Bear", "Dragon"];

        private static readonly string[] MidgardAnimals =
            ["Wolf", "Bear", "Raven", "Boar", "Eagle", "Serpent", "Ram", "Stag", "Hound", "Falcon", "Dragon", "Troll"];

        private static readonly string[] HiberniaAnimals =
            ["Stag", "Hound", "Swan", "Raven", "Hare", "Salmon", "Boar", "Owl", "Hawk", "Wolf", "Fox", "Crane"];

        private static readonly string[] AlbionTitles =
        [
            "Order of the", "Knights of the", "Guardians of the", "Brotherhood of the", "Company of the", "Wardens of the",
            "Sons of the", "House of the", "Riders of the", "Lords of the",
        ];

        private static readonly string[] MidgardTitles =
        [
            "Warband of the", "Sons of the", "Shield of the", "Hall of the", "Guard of the", "Brothers of the", "Raiders of the",
            "Order of the", "Keepers of the", "Sworn of the",
        ];

        private static readonly string[] HiberniaTitles =
        [
            "Circle of the", "Children of the", "Guardians of the", "Keepers of the", "Grove of the", "Company of the",
            "Order of the", "Dancers of the", "Wardens of the", "Sons of the",
        ];

        private static readonly string[] AlbionPlaces =
        [
            "Avalon", "Camelot", "Camlann", "Cornwall", "Salisbury", "Dover", "Humberton", "Cotswold", "Snowdonia", "Ludlow",
            "Westgate", "Caerleon", "Ashford", "Lyonesse", "Mordred", "Trent",
        ];

        private static readonly string[] MidgardPlaces =
        [
            "Jordheim", "Vasudheim", "Uppland", "Mularn", "Gotar", "Hagall", "Svasud", "Nailiten", "Odins Gate", "Ravenswood",
            "Fensalir", "Ironpeak", "Valkyn", "Skarn", "Ulfheim", "Aegirhamn",
        ];

        private static readonly string[] HiberniaPlaces =
        [
            "Tara", "Tir na Nog", "Druim Ligen", "Connacht", "Mag Mell", "Cruachan", "Shannon", "Fintain", "Cuil Cairn",
            "Ardee", "Tuatha", "Emain", "Innis", "Aran", "Donegal", "Moytura",
        ];

        // "Clan Ironfang": a clan name made of two halves.
        private static readonly string[] CompoundFirst =
        [
            "Iron", "Stone", "Frost", "Storm", "Blood", "Wolf", "Raven", "Bear", "Thunder", "Ash", "Black", "Steel", "Oak", "Dusk",
        ];

        private static readonly string[] CompoundSecond =
        [
            "fang", "hammer", "shield", "blade", "claw", "fist", "helm", "guard", "born", "bane", "wind", "spear", "axe", "mane",
        ];

        private static readonly string[] CompoundHeads = ["Clan", "Hold", "House"];

        private static readonly string[] Numerals = ["II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII"];

        /// <summary>A name for a new guild of this realm that <paramref name="isTaken"/> does not know.</summary>
        public static string Generate(eRealm realm, Func<string, bool> isTaken, Random random = null)
        {
            random ??= Random.Shared;
            isTaken ??= _ => false;
            for (int attempt = 0; attempt < 400; attempt++)
            {
                string name = Candidate(realm, random);
                if (IsAcceptable(name) && !isTaken(name))
                    return name;
            }
            // Every combination tried was taken: a numeral keeps the name unique ("Order of the Silver Hawk II").
            string baseName = Candidate(realm, random);
            for (int round = 0; round < 50; round++)
            {
                foreach (string numeral in Numerals)
                {
                    string name = $"{baseName} {numeral}";
                    if (IsAcceptable(name) && !isTaken(name))
                        return name;
                }
                baseName = Candidate(realm, random);
            }
            return null;
        }

        /// <summary>True when the name uses letters and spaces only and fits <see cref="MaxLength"/> characters.</summary>
        public static bool IsAcceptable(string name) =>
            !string.IsNullOrWhiteSpace(name) && name.Length <= MaxLength && name == name.Trim() && Allowed.IsMatch(name) &&
            !name.Contains("  ", StringComparison.Ordinal) && !name.Equals("None", StringComparison.OrdinalIgnoreCase);

        private static string Candidate(eRealm realm, Random random)
        {
            string[] animals = realm switch { eRealm.Midgard => MidgardAnimals, eRealm.Hibernia => HiberniaAnimals, _ => AlbionAnimals };
            string[] titles = realm switch { eRealm.Midgard => MidgardTitles, eRealm.Hibernia => HiberniaTitles, _ => AlbionTitles };
            string[] places = realm switch { eRealm.Midgard => MidgardPlaces, eRealm.Hibernia => HiberniaPlaces, _ => AlbionPlaces };

            switch (random.Next(10))
            {
                case 0:
                case 1:
                case 2:
                case 3:
                    // "Order of the Silver Hawk"
                    return $"{Pick(titles, random)} {Pick(Colours, random)} {Pick(animals, random)}";
                case 4:
                case 5:
                    // "Clan Ironfang" (Midgard leans on clans, the others use them less)
                    string head = realm == eRealm.Midgard ? "Clan" : Pick(CompoundHeads, random);
                    return $"{head} {Pick(CompoundFirst, random)}{Pick(CompoundSecond, random)}";
                case 6:
                case 7:
                    // "Wardens of Avalon": the title without its "the"
                    return $"{Pick(titles, random).Replace(" the", string.Empty, StringComparison.Ordinal)} {Pick(places, random)}";
                case 8:
                    // "The Silver Hawk Company"
                    return $"The {Pick(Colours, random)} {Pick(animals, random)} {Pick(["Company", "Order", "Guard", "Brotherhood"], random)}";
                default:
                    // a place name alone: "Avalon Legion"
                    return $"{Pick(places, random)} {Pick(["Legion", "Guard", "Company", "Watch", "Host"], random)}";
            }
        }

        private static string Pick(IReadOnlyList<string> values, Random random) => values[random.Next(values.Count)];
    }
}
