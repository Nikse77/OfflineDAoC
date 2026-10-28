using System;
using System.Collections.Generic;
using System.Linq;

namespace DOL.GS
{
    /// <summary>
    /// Owner 2026-10-28: who joins an RvR warband or a battleground party. A party of eight wants one speed song, two who can
    /// heal, one who can mez or root and one tank; the rest are attackers. One bot can cover several of those (a Bard is speed
    /// and mez, a Shaman heals and roots). A Paladin does not count as a healer here. A smaller party drops its places in this
    /// order: attackers, tank, control, speed, healers. A party with a place left uncovered is still formed.
    /// </summary>
    public static class AutonomousRvrRoster
    {
        [Flags]
        public enum Role { None = 0, Speed = 1, Healer = 2, Control = 4, Tank = 8, Attacker = 16 }

        public static bool IsSpeedClass(eCharacterClass characterClass) =>
            characterClass is eCharacterClass.Bard or eCharacterClass.Minstrel or eCharacterClass.Skald;

        /// <summary>Classes that heal in an RvR party: not the Paladin, and the Friar is an attacker (owner 2026-10-28).</summary>
        public static bool IsHealerClass(eCharacterClass characterClass) =>
            characterClass is eCharacterClass.Cleric or eCharacterClass.Druid or eCharacterClass.Warden or
                eCharacterClass.Healer or eCharacterClass.Shaman;

        /// <summary>Who mezzes or roots in a party. Albion: the Sorcerer and the Theurgist only (owner 2026-10-28); the other realms
        /// from the game's spell lines, the Bonedancer being an attacker only. The launcher keeps the same lists when it rolls a
        /// new bot's class.</summary>
        public static bool IsControlClass(eCharacterClass characterClass) =>
            characterClass is eCharacterClass.Sorcerer or eCharacterClass.Theurgist or
                eCharacterClass.Skald or eCharacterClass.Healer or eCharacterClass.Spiritmaster or eCharacterClass.Shaman or
                eCharacterClass.Runemaster or
                eCharacterClass.Enchanter or eCharacterClass.Mentalist or eCharacterClass.Bard or eCharacterClass.Druid;

        /// <summary>Who tanks in a party: the Paladin alone in Albion (owner 2026-10-28), the Warrior and the Thane in Midgard, the
        /// Hero and the Champion in Hibernia (and the Sluaghbinder). Not the PvE tank list of BotPartyRoles.</summary>
        public static bool IsTankClass(eCharacterClass characterClass) =>
            characterClass is eCharacterClass.Paladin or eCharacterClass.Warrior or eCharacterClass.Thane or
                eCharacterClass.Hero or eCharacterClass.Champion or eCharacterClass.Sluaghbinder;

        public static Role RolesOf(eCharacterClass characterClass)
        {
            Role roles = Role.None;
            if (IsSpeedClass(characterClass)) roles |= Role.Speed;
            if (IsHealerClass(characterClass)) roles |= Role.Healer;
            if (IsControlClass(characterClass)) roles |= Role.Control;
            if (IsTankClass(characterClass)) roles |= Role.Tank;
            // Whoever has no other role attacks.
            if (roles == Role.None) roles |= Role.Attacker;
            return roles;
        }

        public static Role RolesOf(GameBot bot) =>
            bot?.CharacterClass == null ? Role.None : RolesOf((eCharacterClass)bot.CharacterClass.ID);

        // The places of a party, kept in this order when it is small: the last of the list is dropped first (attackers
        // are not listed: they are what is left).
        private static readonly Role[] Places = [Role.Healer, Role.Healer, Role.Speed, Role.Control, Role.Tank];

        /// <summary>What a party of this size asks for: eight and up all five places, a smaller one the first of them.</summary>
        public static (Role Role, int Count)[] Requirements(int size) =>
            Places.Take(Math.Clamp(size, 0, Places.Length)).GroupBy(role => role)
                .Select(group => (group.Key, group.Count())).ToArray();

        /// <summary>
        /// Picks <paramref name="count"/> members from the pool (already in the order the caller prefers: level, then
        /// distance) to go with the leader: first whoever covers a place the party still lacks (most places at once first), then
        /// attackers, then the rest. A place nobody in the pool can cover stays uncovered.
        /// </summary>
        public static int[] Select(Role leader, IReadOnlyList<Role> pool, int count)
        {
            count = Math.Min(count, pool.Count);
            var chosen = new List<int>();
            if (count <= 0) return [];
            var needs = Requirements(count + 1);

            int Have(Role role) => (leader.HasFlag(role) ? 1 : 0) + chosen.Count(index => pool[index].HasFlag(role));
            List<Role> Missing() => needs.Where(need => Have(need.Role) < need.Count).Select(need => need.Role).ToList();

            while (chosen.Count < count)
            {
                List<Role> missing = Missing();
                if (missing.Count == 0) break;
                int best = -1, bestScore = 0;
                for (int index = 0; index < pool.Count; index++)
                {
                    if (chosen.Contains(index)) continue;
                    int score = missing.Count(role => pool[index].HasFlag(role));
                    if (score > bestScore) { best = index; bestScore = score; }
                }
                if (best < 0) break;
                chosen.Add(best);
            }

            // The rest: attackers first, then whoever is nearest in the caller's order.
            foreach (int index in Enumerable.Range(0, pool.Count).Where(index => !chosen.Contains(index))
                         .OrderBy(index => pool[index].HasFlag(Role.Attacker) ? 0 : 1))
            {
                if (chosen.Count >= count) break;
                chosen.Add(index);
            }
            return chosen.ToArray();
        }

        /// <summary>For the log: how many of each role a party has.</summary>
        public static string Summary(IEnumerable<Role> members)
        {
            Role[] all = members.ToArray();
            return $"speed={all.Count(role => role.HasFlag(Role.Speed))} healers={all.Count(role => role.HasFlag(Role.Healer))} " +
                   $"control={all.Count(role => role.HasFlag(Role.Control))} tanks={all.Count(role => role.HasFlag(Role.Tank))} " +
                   $"attackers={all.Count(role => role.HasFlag(Role.Attacker))}";
        }
    }
}
