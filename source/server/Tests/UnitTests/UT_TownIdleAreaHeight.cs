using DOL.GS;
using NUnit.Framework;

namespace DOL.GS.Tests;

[TestFixture]
public sealed class UT_TownIdleAreaHeight
{
    // Caer Witrin's area record: a circle of radius 1,000 centred at 437031,650943, height 2448 (region 1). The idle spots the bots
    // were sent to stand 940 above it (Z 3385), so the three-dimensional distance is just over the radius.
    private static readonly Area.Circle CaerWitrin = new("Caer Witrin", 437031, 650943, 2448, 1000);

    [TestCase(436543, 650917, 3382)]
    [TestCase(436679, 651028, 3385)]
    [TestCase(436626, 651085, 3388)]
    public void TheIdleSpotsAreInsideInTwoDimensionsButNotInThree(int x, int y, int z)
    {
        Assert.That(CaerWitrin.IsContaining(new Point3D(x, y, z), false), Is.True, "the idle code's own test (two dimensions)");
        Assert.That(CaerWitrin.IsContaining(new Point3D(x, y, z), true), Is.False, "the height is part of the area test: the watchdog's old test");
    }

    [Test]
    public void ASpotOutsideTheTownIsOutsideInBoth()
    {
        Assert.That(CaerWitrin.IsContaining(new Point3D(439000, 650943, 2448), false), Is.False);
    }
}
