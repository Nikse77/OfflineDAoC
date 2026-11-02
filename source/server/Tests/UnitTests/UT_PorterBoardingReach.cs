using System.Numerics;
using NUnit.Framework;

namespace DOL.GS.Tests;

[TestFixture]
public class UT_PorterBoardingReach
{
    // Master Visur in Emain and Wilorred, as the run left them: the party spot in front of the gate is 963 units from
    // the porter, so a traveller parked there stood outside the 500-unit boarding radius and never boarded.
    private static readonly Vector3 EmainPorter = new(475_766, 343_201, 4_103);
    private static readonly Vector3 GateFanSpot = new(474_576, 342_751, 4_100);

    [Test]
    public void TheGateFanIsOutOfReachOfThePorter()
    {
        Assert.That(Vector3.Distance(GateFanSpot, EmainPorter), Is.GreaterThan(AutonomousFrontierTransport.BoardingRadius));
        Assert.That(AutonomousFrontierTransport.InsideBoardingReach(GateFanSpot, EmainPorter), Is.False);
    }

    [Test]
    public void TheWaitingRingAroundThePorterIsInReach()
    {
        for (long botId = 1; botId <= 200; botId++)
        {
            Vector3 offset = AutonomousFrontierTransport.WaitingOffset(botId);
            Vector3 spot = EmainPorter + offset;
            Assert.That(AutonomousFrontierTransport.InsideBoardingReach(spot, EmainPorter), Is.True,
                $"bot {botId} at {Vector3.Distance(spot, EmainPorter):0}");
        }
    }

    [Test]
    public void ReachKeepsAMarginUnderTheBoardingRadius()
    {
        Assert.That(AutonomousFrontierTransport.InsideBoardingReach(EmainPorter, EmainPorter), Is.True);
        Assert.That(AutonomousFrontierTransport.InsideBoardingReach(EmainPorter + new Vector3(440, 0, 0), EmainPorter), Is.True);
        Assert.That(AutonomousFrontierTransport.InsideBoardingReach(EmainPorter + new Vector3(460, 0, 0), EmainPorter), Is.False);
        Assert.That(AutonomousFrontierTransport.InsideBoardingReach(EmainPorter + new Vector3(0, -700, 0), EmainPorter), Is.False);
    }
}
