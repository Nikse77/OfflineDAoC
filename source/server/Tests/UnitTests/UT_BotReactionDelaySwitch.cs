using DOL.GS;
using NUnit.Framework;

// The unit-test bots must act at once; the delay itself is tested in UT_BotReactionDelay.
[SetUpFixture]
public class UT_BotReactionDelaySwitch
{
    [OneTimeSetUp]
    public void SwitchOff() => BotReactionDelay.Enabled = false;
}
