namespace DOL.GS
{
    /// <summary>
    /// The one-line text a party member carries while it travels in formation. Owner 2026-11-02: every member of a
    /// frontier patrol read "the party clears dungeon threats before advancing" while walking Emain in the open, and the
    /// phase column kept saying "Boarding frontier teleporter" for minutes after the crossing. The wording now follows
    /// what the party is really doing, and the phase follows the wording.
    /// </summary>
    public static class AutonomousGroupStatusText
    {
        public const string TravelingInFormation = "Traveling in group formation";

        public static string Formation(string leader, eAutonomousObjectiveKind kind, bool dungeonCamp)
        {
            if (kind == eAutonomousObjectiveKind.RvR)
                return $"Keeping formation with {leader}; the party patrols the frontier as one";
            if (dungeonCamp)
                return $"Keeping formation with {leader}; the party clears dungeon threats before advancing";
            return $"Keeping formation with {leader}; the party advances to its next objective";
        }
    }
}
