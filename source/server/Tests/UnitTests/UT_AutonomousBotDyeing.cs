using System;
using System.Linq;
using DOL.Database;
using NUnit.Framework;

namespace DOL.GS.Tests;

[TestFixture]
public sealed class UT_AutonomousBotDyeing
{
    private static DbItemTemplate Dye(string name, int model, long price, int color, int itemType = 40) =>
        new() { Id_nb = Guid.NewGuid().ToString(), Name = name, Model = model, Price = price, Color = color, Item_Type = itemType };

    private static AutonomousBotDyeing.Offer Offer(int model, int color) => new(model, color, 50);

    [TestCase(eInventorySlot.TorsoArmor, 32, 0, AutonomousBotDyeing.ClothDye)]
    [TestCase(eInventorySlot.HeadArmor, 33, 0, AutonomousBotDyeing.LeatherDye)]
    [TestCase(eInventorySlot.LegsArmor, 34, 0, AutonomousBotDyeing.MetalDye)]
    [TestCase(eInventorySlot.ArmsArmor, 35, 0, AutonomousBotDyeing.MetalDye)]
    [TestCase(eInventorySlot.HandsArmor, 36, 0, AutonomousBotDyeing.MetalDye)]
    [TestCase(eInventorySlot.FeetArmor, 37, 0, AutonomousBotDyeing.MetalDye)]
    [TestCase(eInventorySlot.TorsoArmor, 38, 0, AutonomousBotDyeing.MetalDye)]
    [TestCase(eInventorySlot.Cloak, 41, 26, AutonomousBotDyeing.ClothDye)]
    [TestCase(eInventorySlot.Cloak, 32, 26, 0)]
    [TestCase(eInventorySlot.TorsoArmor, 42, 0, 0)]
    [TestCase(eInventorySlot.RightHandWeapon, 2, 0, 0)]
    public void TheDyeModelFollowsTheMaterialOfThePiece(eInventorySlot slot, int objectType, int itemType, int expected) =>
        Assert.That(AutonomousBotDyeing.DyeModelFor(slot, objectType, itemType), Is.EqualTo(expected));

    [Test]
    public void OnlyRealCheapDyesAndEnamelsAreOffers()
    {
        Assert.That(AutonomousBotDyeing.TryGetOffer(Dye("light blue cloth dye", 229, 50, 3), out var cloth), Is.True);
        Assert.That(cloth, Is.EqualTo(new AutonomousBotDyeing.Offer(229, 3, 50)));
        Assert.That(AutonomousBotDyeing.TryGetOffer(Dye("black enamel", 495, 165000, 74), out var black), Is.True, "dear dyes are offers too");
        Assert.That(black.Price, Is.EqualTo(165000));
        Assert.That(AutonomousBotDyeing.TryGetOffer(Dye("stout dwarven ale", 495, 100, 0), out _), Is.False, "same model, not a colour");
        Assert.That(AutonomousBotDyeing.TryGetOffer(Dye("mild acid enamel remover", 495, 50, 0), out _), Is.False);
        Assert.That(AutonomousBotDyeing.TryGetOffer(Dye("blue weapon luster", 538, 50, 5), out _), Is.False, "weapons are not tinted");
        Assert.That(AutonomousBotDyeing.TryGetOffer(Dye("blue cloth dye", 229, 50, 5, itemType: 8), out _), Is.False);
        Assert.That(AutonomousBotDyeing.TryGetOffer(Dye("free cloth dye", 229, 0, 5), out _), Is.False);
    }

    [Test]
    public void TheBotColourIsStableAndDependsOnTheBot()
    {
        var stock = new[] { Offer(229, 1), Offer(229, 2), Offer(229, 3), Offer(229, 4) };
        int? first = AutonomousBotDyeing.ChooseColor(10, [229], stock);
        Assert.That(AutonomousBotDyeing.ChooseColor(10, [229], stock), Is.EqualTo(first), "the same bot keeps its colour");
        var colours = Enumerable.Range(0, 8).Select(id => AutonomousBotDyeing.ChooseColor(id, [229], stock)).Distinct().ToArray();
        Assert.That(colours, Has.Length.EqualTo(4), "bots spread over every colour in stock");
    }

    [Test]
    public void TheColourCoversEveryMaterialTheBotWearsWhenSomeColourDoes()
    {
        // 1 only as cloth, 2 as cloth and metal, 3 only as metal: a bot in cloth and metal must get colour 2.
        var stock = new[] { Offer(229, 1), Offer(229, 2), Offer(495, 2), Offer(495, 3) };
        for (int id = 0; id < 6; id++)
            Assert.That(AutonomousBotDyeing.ChooseColor(id, [229, 495], stock), Is.EqualTo(2), $"bot {id}");
    }

    [Test]
    public void WithoutAColourForEveryMaterialTheBestCoverageWinsAndNothingInStockMeansNoColour()
    {
        var stock = new[] { Offer(229, 1), Offer(495, 2), Offer(495, 3) };
        Assert.That(AutonomousBotDyeing.ChooseColor(0, [229, 495], stock), Is.AnyOf(1, 2, 3));
        Assert.That(AutonomousBotDyeing.ChooseColor(0, [494], stock), Is.Null, "no leather dye at all");
        Assert.That(AutonomousBotDyeing.ChooseColor(0, [], stock), Is.Null);
        Assert.That(AutonomousBotDyeing.ChooseColor(0, [229], Array.Empty<AutonomousBotDyeing.Offer>()), Is.Null);
    }

    [TestCase(0L, 100L)]
    [TestCase(5_000L, 100L)]
    [TestCase(7_000L, 100L)]
    [TestCase(70_000L, 1_000L)]
    [TestCase(289_500L, 4_135L)]
    [TestCase(10_000_000L, 142_857L)]
    public void ADyeCostsAtMostASeventiethOfTheBotsMoneyAndNeverLessThanOneSilverIsAllowed(long money, long ceiling) =>
        Assert.That(AutonomousBotDyeing.PriceCeiling(money), Is.EqualTo(ceiling));

    [Test]
    public void ASetOfSevenAtTheCeilingCostsAtMostATenthOfTheMoney()
    {
        foreach (long money in new long[] { 70_000, 289_500, 10_000_000, 154_000_000 })
            Assert.That(AutonomousBotDyeing.PriceCeiling(money) * 7, Is.LessThanOrEqualTo(money / 10 + 7));
    }

    [Test]
    public void ABotKeepsTheColourItWearsMostWhenTheWholeStockSellsItForEveryMaterial()
    {
        var stock = new[] { Offer(229, 1), Offer(495, 1), Offer(229, 2), Offer(495, 2), Offer(229, 3) };
        Assert.That(AutonomousBotDyeing.StickyColor([2, 2, 1, 0], [229, 495], stock), Is.EqualTo(2), "the most worn colour");
        Assert.That(AutonomousBotDyeing.StickyColor([3, 3, 3], [229, 495], stock), Is.Null, "3 has no metal dye in stock");
        Assert.That(AutonomousBotDyeing.StickyColor([3, 3, 1], [229, 495], stock), Is.EqualTo(1), "the next colour that covers both");
        Assert.That(AutonomousBotDyeing.StickyColor([0, 0], [229], stock), Is.Null, "undyed pieces say nothing");
        Assert.That(AutonomousBotDyeing.StickyColor([], [229], stock), Is.Null);
    }

    [Test]
    public void ARicherBotMovesUpItsOwnOrderOfColoursAndNeverDown()
    {
        // Bot 0 takes the colours 1, 2, 3, 4 in that order. Dyes of colour 1 cost 1000, of colour 2 cost 100.
        var stock = new AutonomousBotDyeing.Offer[]
        {
            new(229, 1, 1000), new(229, 2, 100), new(229, 3, 50), new(229, 4, 50)
        };
        Assert.That(AutonomousBotDyeing.PersonalOrder(0, stock), Is.EqualTo(new[] { 1, 2, 3, 4 }));
        Assert.That(AutonomousBotDyeing.PersonalOrder(1, stock), Is.EqualTo(new[] { 2, 3, 4, 1 }));
        // Colour 3 now: a ceiling of 400 (half: 200) opens colour 2 but not colour 1.
        Assert.That(AutonomousBotDyeing.BetterColor(0, [229], stock, 3, 400), Is.EqualTo(2));
        // A ceiling of 2200 (half: 1100) opens colour 1, the first of its order.
        Assert.That(AutonomousBotDyeing.BetterColor(0, [229], stock, 3, 2200), Is.EqualTo(1));
        // The margin: colour 2 costs 100, so half the ceiling must reach 100.
        Assert.That(AutonomousBotDyeing.BetterColor(0, [229], stock, 3, 198), Is.Null, "the price must fit in half the ceiling");
        Assert.That(AutonomousBotDyeing.BetterColor(0, [229], stock, 3, 200), Is.EqualTo(2));
        // Never down: from the first colour, or with little money, nothing changes.
        Assert.That(AutonomousBotDyeing.BetterColor(0, [229], stock, 1, 1_000_000), Is.Null);
        Assert.That(AutonomousBotDyeing.BetterColor(0, [229], stock, 3, 100), Is.Null, "a poorer bot keeps its colour");
        Assert.That(AutonomousBotDyeing.BetterColor(0, [229], stock, null, 1_000_000), Is.Null, "the first colour comes from ChooseColor");
    }

    [Test]
    public void TheFirstColourFollowsTheOrderOfTheWholeStockEvenWhenOnlyPartOfItIsAffordable()
    {
        var full = new AutonomousBotDyeing.Offer[] { new(229, 1, 1000), new(229, 2, 100), new(229, 3, 50), new(229, 4, 50) };
        var affordable = full.Where(offer => offer.Price <= 100).ToArray();
        int[] order = AutonomousBotDyeing.PersonalOrder(0, full);
        // Bot 0 prefers 1, 2, 3, 4; colour 1 is too dear, so the first affordable one in the same order is 2.
        Assert.That(AutonomousBotDyeing.ChooseColor(0, [229], affordable, order), Is.EqualTo(2));
        // Without the whole-stock order the 3-colour affordable palette would start elsewhere.
        Assert.That(AutonomousBotDyeing.ChooseColor(0, [229], affordable), Is.EqualTo(2).Or.EqualTo(3).Or.EqualTo(4));
    }

    [Test]
    public void ABetterColourMustCoverEveryMaterialTheBotWears()
    {
        var stock = new AutonomousBotDyeing.Offer[] { new(229, 1, 10), new(229, 2, 10), new(495, 2, 10), new(495, 3, 10) };
        // Order for bot 0: 1, 2, 3. Colour 1 has no metal dye, so a bot on 3 in cloth and metal can only reach colour 2.
        Assert.That(AutonomousBotDyeing.BetterColor(0, [229, 495], stock, 3, 1000), Is.EqualTo(2));
    }

    [Test]
    public void ATieBetweenTwoWornColoursGoesToTheBetterOneInTheBotsOrder()
    {
        var stock = new[] { Offer(229, 1), Offer(229, 2), Offer(229, 3) };
        // Three pieces in colour 2 and three in colour 3: the order puts 3 first, so the move up is finished, not undone.
        Assert.That(AutonomousBotDyeing.StickyColor([2, 2, 2, 3, 3, 3], [229], stock, new[] { 3, 2, 1 }), Is.EqualTo(3));
        Assert.That(AutonomousBotDyeing.StickyColor([2, 2, 2, 3, 3, 3], [229], stock, new[] { 2, 3, 1 }), Is.EqualTo(2));
        // Without an order the lower colour wins, as before.
        Assert.That(AutonomousBotDyeing.StickyColor([2, 2, 2, 3, 3, 3], [229], stock), Is.EqualTo(2));
    }

    [Test]
    public void ABotThatCannotReachAMerchantLooksAgainSoonNotInTwoHours()
    {
        Assert.That(AutonomousBotDyeing.RetryDelay, Is.LessThan(AutonomousBotDyeing.TripCooldown));
        Assert.That(AutonomousBotDyeing.RetryDelay, Is.EqualTo(TimeSpan.FromMinutes(15)));
    }

    [Test]
    public void TheTripsAreSpacedByTwoHoursAndOnlyFromLevelTwelve()
    {
        Assert.That(AutonomousBotDyeing.TripCooldown, Is.EqualTo(TimeSpan.FromHours(2)));
        Assert.That(AutonomousBotDyeing.MinimumLevel, Is.EqualTo(12));
        Assert.That(AutonomousBotDyeing.MinimumPriceCeilingCopper, Is.EqualTo(100), "one silver");
        Assert.That(AutonomousBotDyeing.SetBudgetDivisor, Is.EqualTo(70), "a tenth of the money for seven pieces");
        Assert.That(AutonomousBotDyeing.MaximumTripMinutes, Is.EqualTo(20), "the phase between tasks lasts thirty minutes");
    }
}
