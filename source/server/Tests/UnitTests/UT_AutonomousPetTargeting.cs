using System.Linq;
using DOL.AI.Brain;
using NUnit.Framework;

namespace DOL.GS.Tests;

[TestFixture]
public sealed class UT_AutonomousPetTargeting
{
    private static bool IsPet(string name) => name.EndsWith("-pet");

    [Test]
    public void PetsAreDroppedWhenAPlayerIsInView()
    {
        var kept = AutonomousPetTargeting.WithoutPetsWhenPlayersPresent(new[] { "a-pet", "Bob", "b-pet", "Carl" }, IsPet);
        Assert.That(kept, Is.EqualTo(new[] { "Bob", "Carl" }));
    }

    [Test]
    public void PetsAreKeptWhenNoPlayerIsLeft()
    {
        var kept = AutonomousPetTargeting.WithoutPetsWhenPlayersPresent(new[] { "a-pet", "b-pet" }, IsPet);
        Assert.That(kept, Is.EqualTo(new[] { "a-pet", "b-pet" }));
    }

    [Test]
    public void AnEmptyListStaysEmpty() =>
        Assert.That(AutonomousPetTargeting.WithoutPetsWhenPlayersPresent(System.Array.Empty<string>(), IsPet), Is.Empty);
}
