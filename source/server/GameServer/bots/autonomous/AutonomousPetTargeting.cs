using System;
using System.Collections.Generic;
using System.Linq;
using DOL.AI.Brain;

namespace DOL.GS
{
    /// <summary>
    /// Owner 2026-10-28: in the battlegrounds the opposing bots changed target to hit the pets. A player (a real one or a
    /// bot) is always preferred over its pet; a pet is a target only when no player is there to take.
    /// </summary>
    public static class AutonomousPetTargeting
    {
        /// <summary>A pet controlled by a player or a bot (what the battleground scans call a pet).</summary>
        public static bool IsPlayerPet(GameLiving living) =>
            living is GameNPC { Brain: IControlledBrain brain } && brain.GetLivingOwner() is IGamePlayer;

        /// <summary>The candidates without the pets, or all of them when only pets are left.</summary>
        public static IReadOnlyList<T> WithoutPetsWhenPlayersPresent<T>(IReadOnlyList<T> candidates, Func<T, bool> isPet)
        {
            if (candidates == null || candidates.Count == 0) return candidates;
            T[] players = candidates.Where(candidate => !isPet(candidate)).ToArray();
            return players.Length > 0 ? players : candidates;
        }
    }
}
