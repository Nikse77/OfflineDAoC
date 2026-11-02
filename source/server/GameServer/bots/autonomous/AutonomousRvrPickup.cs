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
        /// <summary>
        /// Owner 2026-11-02: "quello che aspetta rientra in formazione quando il capo è entro i 2000". Two thousand units
        /// is the meeting distance: at that range the waiting member leaves the keep and walks back into formation, and
        /// the party gathered at a released leader may start moving again.
        /// </summary>
        public const int PickupRadius = 2_000;

        /// <summary>
        /// Owner 2026-11-02: "il capo deve andare dal membro che aspetta" - the leader does not stop at the meeting
        /// distance, it walks on to the comrade itself and only gives up the fetch this close.
        /// </summary>
        public const int FetchRadius = 600;

        /// <summary>
        /// Owner 2026-11-02: "un membro che fa release aspetta dove sta che il leader sia a tiro (2000 unità?)" - the
        /// combat leash in both directions, a different rule from the meeting distance above.
        /// </summary>
        public const int LeashRadius = 2_000;

        /// <summary>The released member walks the last stretch once its leader is this close.</summary>
        public static bool MemberJoinsLeader(double distanceToLeader) => distanceToLeader <= PickupRadius;

        /// <summary>The leader keeps marching to the comrade until it is this close, so it really goes to it.</summary>
        public static bool LeaderKeepsFetching(double distanceToKeep) => distanceToKeep > FetchRadius;

        /// <summary>Owner 2026-11-02: 2,000 units is the leash for a fighter too - beyond it the member drops the
        /// chase and comes back into formation.</summary>
        public static bool MemberReturnsToFormation(double distanceToLeader) => distanceToLeader > LeashRadius;

        /// <summary>Owner 2026-11-02: the leader has the same leash. Chasing a target must never cost it the party:
        /// past the radius from its nearest member it drops the chase and walks back to them.</summary>
        public static bool LeaderMustReturn(double distanceToNearestMember) => distanceToNearestMember > LeashRadius;

        /// <summary>
        /// Owner 2026-11-02, on a warband that lost the whole fight: the leader that released at the frontier keep is
        /// the last of the group to fall, so it waits at the portal until its living comrades have gathered around it
        /// instead of marching off while they trail after it one at a time. The wait is bounded: one comrade that
        /// cannot reach the gate must not pin the warband there for ever.
        /// </summary>
        public const long KeepGatherMilliseconds = 180_000;

        public static bool PartyMayLeaveKeep(double distanceToFarthestLivingComrade, long waitedMilliseconds) =>
            distanceToFarthestLivingComrade <= PickupRadius || waitedMilliseconds >= KeepGatherMilliseconds;
    }
}
