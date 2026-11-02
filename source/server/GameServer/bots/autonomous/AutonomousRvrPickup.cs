namespace DOL.GS
{
    /// <summary>
    /// Owner 2026-11-02, on how a party reunites in the frontier: "per i membri di un gruppo il punto di raduno è sempre
    /// il leader"; "un membro che fa release aspetta dove sta che il leader sia a tiro (2000 unità?) e lo raggiunge per
    /// rientrare in formazione"; "il leader sceglie dove andare dopo che tutti i suoi membri sono in formazione (tranne
    /// quelli che hanno fatto release)"; "ovviamente se qualcuno ha fatto release il leader lo va a prendere ... con tutto
    /// il gruppo rimasto al seguito". The two distances that decide it live here, so both sides read the same number.
    /// </summary>
    public static class AutonomousRvrPickup
    {
        public const int PickupRadius = 2_000;

        /// <summary>The released member walks the last stretch once its leader is this close.</summary>
        public static bool MemberJoinsLeader(double distanceToLeader) => distanceToLeader <= PickupRadius;

        /// <summary>The leader keeps marching to the keep while it is still outside the pickup radius.</summary>
        public static bool LeaderKeepsFetching(double distanceToKeep) => distanceToKeep > PickupRadius;

        /// <summary>Owner 2026-11-02: 2,000 units is the leash for a fighter too - beyond it the member drops the
        /// chase and comes back into formation.</summary>
        public static bool MemberReturnsToFormation(double distanceToLeader) => distanceToLeader > PickupRadius;

        /// <summary>Owner 2026-11-02: the leader has the same leash. Chasing a target must never cost it the party:
        /// past the radius from its nearest member it drops the chase and walks back to them.</summary>
        public static bool LeaderMustReturn(double distanceToNearestMember) => distanceToNearestMember > PickupRadius;
    }
}
