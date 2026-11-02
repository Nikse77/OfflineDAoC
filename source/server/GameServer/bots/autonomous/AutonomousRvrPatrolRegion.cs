using System;

namespace DOL.GS
{
    /// <summary>
    /// Owner 2026-11-02: "quando il gruppo inizia il pattugliamento tutto il pattugliamento deve svolgersi nella regione
    /// assegnata". A patrol is assigned to one frontier - Emain, Odin or Hadrian's Wall - and finishes inside it. The
    /// region is chosen when the party picks a new objective and then holds for the whole task; nothing expires on a
    /// clock. Only a new task (a siege, a defense, a fresh patrol) may name another frontier, and that crossing is by
    /// the teleporter.
    /// </summary>
    public static class AutonomousRvrPatrolRegion
    {
        /// <summary>The phase the party carries while it is picking a new objective.</summary>
        public const string ChoosingTaskPhase = "Choosing group target";

        /// <summary>The three frontiers a patrol can be assigned to.</summary>
        public static bool IsPatrolRegion(ushort region) => region is 1 or 100 or 200;

        /// <summary>
        /// True when the assignment pins the party to one frontier: a patrol region is assigned, the party can reach
        /// it, and it is not choosing a new objective. Mid-task the pin holds - the planner that used to look at the
        /// other two frontiers after twenty minutes picked a keep it could not reach from where the party stood,
        /// abandoned it and picked again, and every attempt moved the party rendezvous to another frontier.
        /// </summary>
        public static bool PinsToAssignedRegion(ushort assigned, bool reachable, bool choosingTask) =>
            IsPatrolRegion(assigned) && reachable && !choosingTask;
    }
}
