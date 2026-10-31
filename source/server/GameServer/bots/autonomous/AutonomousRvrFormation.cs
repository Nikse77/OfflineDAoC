namespace DOL.GS;

/// <summary>
/// Owner 2026-10-31, step 2 of the RvR plan for the level-50 bots: they form groups of eight (with the role rules of
/// AutonomousRvrRoster) whatever they are going to do. A group that cannot be completed starts anyway with six or seven once
/// somebody has waited for it long enough. The classes that can turn invisible go alone (to ambush) or fill a group last, as
/// attackers; the Minstrel has the group speed song, so it is a normal group member.
/// </summary>
public static class AutonomousRvrFormation
{
    public const int FullSize = 8;
    public const int SmallestSize = 6;

    /// <summary>Owner 2026-10-31: five minutes (a trial value) before a group of six or seven may start.</summary>
    public const long SmallGroupWaitMilliseconds = 5 * 60_000;

    /// <summary>
    /// How many members the group starts with, the leader included, given the bots that could be in it and how long the one that
    /// waited longest has been waiting: eight when there are eight, six to seven once the wait is over, otherwise 0 (keep waiting).
    /// </summary>
    public static int TargetSize(int candidates, long longestWaitMilliseconds)
    {
        if (candidates >= FullSize)
            return FullSize;
        return candidates >= SmallestSize && longestWaitMilliseconds >= SmallGroupWaitMilliseconds ? candidates : 0;
    }

    /// <summary>
    /// A class that goes alone (or fills a group as an attacker): the ones that can turn invisible, except those with the group
    /// speed song, who give the group the priority (the Minstrel).
    /// </summary>
    public static bool IsSoloStealth(eCharacterClass characterClass) =>
        BotRvrAmbush.IsStealthClass(characterClass) && characterClass != eCharacterClass.Minstrel;
}
