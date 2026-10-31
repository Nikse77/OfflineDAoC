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
        public const int PageSize = 8;

        public sealed record Look(int Model, string Name, int Level);

        public sealed record Candidate(int Model, string Name, int Level);

        /// <summary>
        /// One look per model, left out the model the weapon already has. A model is named after its lowest-level item
        /// (then the first by name); looks are ordered by that name.
        /// </summary>
        public static IReadOnlyList<Look> Looks(IEnumerable<Candidate> candidates, int currentModel) => candidates
            .Where(c => c.Model > 0 && c.Model != currentModel && !string.IsNullOrWhiteSpace(c.Name))
            .GroupBy(c => c.Model)
            .Select(group => group.OrderBy(c => c.Level).ThenBy(c => c.Name, System.StringComparer.OrdinalIgnoreCase).First())
            .Select(c => new Look(c.Model, c.Name.Trim(), c.Level))
            .OrderBy(look => look.Name, System.StringComparer.OrdinalIgnoreCase).ThenBy(look => look.Model)
            .ToList();

        public static int PageCount(int looks) => looks <= 0 ? 1 : (looks + PageSize - 1) / PageSize;

        /// <summary>The page (0-based, clamped) of a list of looks, each with its number counted from 1.</summary>
        public static IEnumerable<(int Number, Look Look)> Page(IReadOnlyList<Look> looks, int page)
        {
            page = System.Math.Clamp(page, 0, PageCount(looks.Count) - 1);
            return looks.Skip(page * PageSize).Take(PageSize).Select((look, i) => (page * PageSize + i + 1, look));
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
