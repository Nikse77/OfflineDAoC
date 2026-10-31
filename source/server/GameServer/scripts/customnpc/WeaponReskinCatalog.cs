using System.Collections.Generic;
using System.Linq;

namespace DOL.GS
{
    /// <summary>
    /// The looks a weapon can take at the weapon reskin master (owner 2026-10-31): every other model that an item of the
    /// same kind already uses in the item table. Pure logic, so it can be tested without a database.
    /// </summary>
    public static class WeaponReskinCatalog
    {
        /// <summary>Owner 2026-10-31: only the looks of items of this level or above are offered.</summary>
        public const int MinimumLevel = 50;

        public sealed record Look(int Model, string Name, int Level);

        public sealed record Candidate(int Model, string Name, int Level);

        /// <summary>
        /// One look per model, left out the model the weapon already has and every model that only items below
        /// <see cref="MinimumLevel"/> use. A model is named after its lowest-level item of that level or above (then the
        /// first by name); looks are ordered by that name.
        /// </summary>
        public static IReadOnlyList<Look> Looks(IEnumerable<Candidate> candidates, int currentModel) => candidates
            .Where(c => c.Level >= MinimumLevel && c.Model > 0 && c.Model != currentModel && !string.IsNullOrWhiteSpace(c.Name))
            .GroupBy(c => c.Model)
            .Select(group => group.OrderBy(c => c.Level).ThenBy(c => c.Name, System.StringComparer.OrdinalIgnoreCase).First())
            .Select(c => new Look(c.Model, c.Name.Trim(), c.Level))
            .OrderBy(look => look.Name, System.StringComparer.OrdinalIgnoreCase).ThenBy(look => look.Model)
            .ToList();

        /// <summary>
        /// A popup message may not pass 2,048 bytes in one packet (the server discards a bigger one: 3,058 bytes with all
        /// the looks of a shield on one page, 2026-10-31). The lines of a page therefore fill at most this many
        /// characters; header, buttons and the packet's own bytes stay in the remainder.
        /// </summary>
        public const int PageCharacters = 1_500;

        /// <summary>One line of a page: the look's number, which the player tells the master, and its name.</summary>
        public static string LineOf(int number, Look look) => $"[look {number}] {look.Name}\n";

        /// <summary>
        /// The looks split into pages, each as many lines as fit in <see cref="PageCharacters"/> (at least one), keeping
        /// their order. A look keeps its number (counted from 1 over all the pages) on whatever page it falls.
        /// </summary>
        public static IReadOnlyList<IReadOnlyList<(int Number, Look Look)>> Pages(IReadOnlyList<Look> looks, int characters = PageCharacters)
        {
            var pages = new List<IReadOnlyList<(int Number, Look Look)>>();
            var page = new List<(int Number, Look Look)>();
            int used = 0;
            for (int i = 0; i < looks.Count; i++)
            {
                int length = LineOf(i + 1, looks[i]).Length;
                if (page.Count > 0 && used + length > characters)
                {
                    pages.Add(page);
                    page = new List<(int Number, Look Look)>();
                    used = 0;
                }
                page.Add((i + 1, looks[i]));
                used += length;
            }
            if (page.Count > 0 || pages.Count == 0)
                pages.Add(page);
            return pages;
        }

        /// <summary>True for the items the master works on: every weapon type and the shield.</summary>
        public static bool IsReskinnable(int objectType) => GlobalConstants.IsWeapon(objectType);

        /// <summary>
        /// What "of the same kind" means. Weapons: same object type, slot and hand. Shields: same size (small, medium or
        /// large, kept in Type_Damage) whatever the hand field says, because the item table fills it in unevenly
        /// for shields (0 and 2 both mean an ordinary shield) and a shield's size is what matters.
        /// </summary>
        public static (int ObjectType, int ItemType, int Hand, int Size) KindOf(int objectType, int itemType, int hand, int typeDamage) =>
            objectType == (int)eObjectType.Shield
                ? (objectType, itemType, -1, typeDamage)
                : (objectType, itemType, hand, 0);
    }
}
