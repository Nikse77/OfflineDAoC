namespace DOL.GS;

/// <summary>
/// Owner 2026-10-31, step 2 of the RvR plan for the level-50 bots: they form groups of eight (with the role rules of
/// AutonomousRvrRoster) whatever they are going to do. A group that cannot be completed starts anyway with six or seven once
/// somebody has waited for it long enough. The classes that can turn invisible go alone (to ambush) or fill a group last, as
/// attackers; the Minstrel has the group speed song, so it is a normal group member.
/// The wait that lowers the size (owner 2026-10-31): five minutes for a group of seven, ten for six, twenty for any size.
/// </summary>
public static class AutonomousRvrFormation
{
    public const int FullSize = 8;
    public const int SevenSize = 7;
    public const int SixSize = 6;
    public const int SmallestGroup = 2;

    /// <summary>Owner 2026-10-31: the wait after which a group of seven is accepted.</summary>
    public const long SevenWaitMilliseconds = 5 * 60_000;
    /// <summary>The wait after which a group of six is accepted.</summary>
    public const long SixWaitMilliseconds = 10 * 60_000;
    /// <summary>The wait after which a group of any size (two or more) is accepted.</summary>
    public const long AnySizeWaitMilliseconds = 20 * 60_000;

    /// <summary>
    /// How many members the group starts with, the leader included, given the bots that could be in it and how long the one that
    /// waited longest has been waiting: eight when there are eight; seven after five minutes; six after ten; any number from two
    /// after twenty; otherwise 0 (keep waiting).
    /// </summary>
    public static int TargetSize(int candidates, long longestWaitMilliseconds)
    {
        if (candidates >= FullSize)
            return FullSize;
        if (longestWaitMilliseconds >= AnySizeWaitMilliseconds)
            return candidates >= SmallestGroup ? candidates : 0;
        if (longestWaitMilliseconds >= SixWaitMilliseconds)
            return candidates >= SixSize ? candidates : 0;
        if (longestWaitMilliseconds >= SevenWaitMilliseconds)
            return candidates >= SevenSize ? candidates : 0;
        return 0;
    }

    /// <summary>
    /// A class that goes alone (or fills a group as an attacker): the ones that can turn invisible, except those with the group
    /// speed song, who give the group the priority (the Minstrel).
    /// </summary>
    public static bool IsSoloStealth(eCharacterClass characterClass) =>
        BotRvrAmbush.IsStealthClass(characterClass) && characterClass != eCharacterClass.Minstrel;

    /// <summary>
    /// A bot that ambushes: a stealth class on its own. In a group it is an ordinary attacker; the Minstrel, who has the group speed song,
    /// is never an ambusher (it waits for a group like the others).
    /// </summary>
    public static bool IsAmbusher(bool inGroup, eCharacterClass characterClass) => !inGroup && IsSoloStealth(characterClass);
}
