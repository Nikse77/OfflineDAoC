using NUnit.Framework;
using OfflineDaoc.Launcher;

[TestFixture]
public class BotRosterBalanceTests
{
    private static readonly int[] Albion = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 19];

    [Test]
    public void ARosterWithNoHealersGetsAHealerFirst()
    {
        // 16 bots, none of them a healer: the largest gap is the healers (2/8 of 17 is four and a quarter).
        var counts = new Dictionary<int, int> { [3] = 4, [7] = 4, [9] = 4, [11] = 4 };
        int chosen = BotCharacterGenerator.ChooseBalancedClass(Albion, counts, new Random(1));
        Assert.That(BotCharacterGenerator.RolesOf(chosen).HasFlag(BotCharacterGenerator.Role.Healer), Is.True);
    }

    [Test]
    public void AHundredBotsFromNothingKeepTheProportionsOfAnEight()
    {
        var counts = new Dictionary<int, int>();
        var random = new Random(7);
        for (int index = 0; index < 100; index++)
        {
            int id = BotCharacterGenerator.ChooseBalancedClass(Albion, counts, random);
            counts[id] = (counts.TryGetValue(id, out int existing) ? existing : 0) + 1;
        }
        int With(BotCharacterGenerator.Role role) =>
            counts.Where(pair => BotCharacterGenerator.RolesOf(pair.Key).HasFlag(role)).Sum(pair => pair.Value);
        int attackers = With(BotCharacterGenerator.Role.Attacker);
        Assert.That(With(BotCharacterGenerator.Role.Healer), Is.GreaterThanOrEqualTo(25));
        Assert.That(With(BotCharacterGenerator.Role.Speed), Is.GreaterThanOrEqualTo(12));
        Assert.That(With(BotCharacterGenerator.Role.Tank), Is.GreaterThanOrEqualTo(12));
        Assert.That(With(BotCharacterGenerator.Role.Control), Is.GreaterThanOrEqualTo(12));
        Assert.That(attackers, Is.GreaterThanOrEqualTo(30));
    }

    [Test]
    public void TheClassesRolledAreVaried()
    {
        // The roles are filled from the whole pool, not always by the same class.
        var counts = new Dictionary<int, int>();
        var random = new Random(3);
        for (int index = 0; index < 200; index++)
        {
            int id = BotCharacterGenerator.ChooseBalancedClass(Albion, counts, random);
            counts[id] = (counts.TryGetValue(id, out int existing) ? existing : 0) + 1;
        }
        Assert.That(counts.Count, Is.GreaterThanOrEqualTo(8));
    }

    [Test]
    public void APaladinIsATankNotAHealer()
    {
        Assert.That(BotCharacterGenerator.RolesOf(1).HasFlag(BotCharacterGenerator.Role.Tank), Is.True);
        Assert.That(BotCharacterGenerator.RolesOf(1).HasFlag(BotCharacterGenerator.Role.Healer), Is.False);
        Assert.That(BotCharacterGenerator.RolesOf(48).HasFlag(BotCharacterGenerator.Role.Speed | BotCharacterGenerator.Role.Control), Is.True);
    }
}
