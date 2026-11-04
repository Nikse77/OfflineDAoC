using System.Numerics;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests;

[TestFixture]
public class UT_AutonomousReachablePost
{
    [Test]
    public void APostTheMeshReachesIsKeptAndOneItDoesNotIsReplacedByTheEndOfThePath()
    {
        var wanted = new Vector3(584567, 478239, 2605);
        Assert.That(AutonomousReachablePost.Decide(wanted, null), Is.EqualTo(wanted), "no answer from the mesh");
        Assert.That(AutonomousReachablePost.Decide(wanted, new Vector3(584567, 478239, 2605)), Is.EqualTo(wanted));
        Assert.That(AutonomousReachablePost.Decide(wanted, new Vector3(584567 - 120, 478239, 2601)), Is.EqualTo(wanted), "exactly the tolerance still reaches");
        var end = new Vector3(584335, 478318, 2617); // Castle Sauvage slot 14: the road ends 246 units short
        Assert.That(AutonomousReachablePost.Decide(wanted, end), Is.EqualTo(end));
    }
}
