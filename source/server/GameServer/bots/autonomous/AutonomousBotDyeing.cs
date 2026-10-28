using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DOL.Database;
using DOL.GS.Quests.Hibernia;
using DOL.Logging;

namespace DOL.GS
{
    /// <summary>
    /// Gamebots from level 12 tint their armor and cloak at the dye merchants of their realm's capital, paying from their saved
    /// copper: one colour per bot, and a dye is only bought when its price is at most a seventieth of the bot's money (a set of
    /// seven costs at most a tenth of it), never less than one silver. A trip happens at a natural task break, at most every
    /// two hours, and may pass several merchants because each capital merchant sells one material and a few colours. The
    /// shield, weapons and everything else keep their colour. Companions and /spawn helpers are not touched.
    /// </summary>
    public static class AutonomousBotDyeing
    {
        private static readonly Logger Log = LoggerManager.Create(MethodBase.GetCurrentMethod().DeclaringType);

        public const int MinimumLevel = 12;
        /// <summary>A dye is bought up to this price whatever the bot's money: one silver.</summary>
        public const long MinimumPriceCeilingCopper = 100;
        /// <summary>The price ceiling is the bot's money divided by this: a set of seven pieces costs at most a tenth of it.</summary>
        public const long SetBudgetDivisor = 70;
        public static readonly TimeSpan TripCooldown = TimeSpan.FromHours(2);
        /// <summary>When no merchant can be reached from where the bot stands, the next look is this far off, not the full
        /// cooldown: the bot may stand near a capital at its next task break.</summary>
        public static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(15);
        /// <summary>A trip starts only from a classic region and at most this many minutes on foot from the capital (a straight
        /// line per crossing): the phase between two tasks lasts thirty minutes, and Caer Sidi, on the Shrouded Isles, is two
        /// borders and 25 to 40 minutes from Camelot.</summary>
        public const double MaximumTripMinutes = 20;
        private const long StockCacheMilliseconds = 10 * 60_000;

        // The dye item's model says what it colours (GamePlayerInventory.DyeItem): cloth and magical cloaks, leather, and the
        // "enamel" for studded, chain, scale, plate and reinforced armor.
        public const int ClothDye = 229, LeatherDye = 494, MetalDye = 495;

        private static readonly eInventorySlot[] Slots =
        [
            eInventorySlot.HeadArmor, eInventorySlot.TorsoArmor, eInventorySlot.ArmsArmor,
            eInventorySlot.HandsArmor, eInventorySlot.LegsArmor, eInventorySlot.FeetArmor, eInventorySlot.Cloak
        ];

        public readonly record struct Offer(int DyeModel, int Color, long Price);

        public sealed record MerchantStock(GameMerchant Merchant, Offer[] Offers);

        private sealed class Trip
        {
            public long NextTripTick;
            public readonly HashSet<int> Visited = new();
        }

        private static readonly ConcurrentDictionary<long, Trip> Trips = new();
        private static readonly ConcurrentDictionary<long, long> LastMissReport = new();
        private static readonly ConcurrentDictionary<eRealm, (long ExpiresTick, MerchantStock[] Stock)> StockCache = new();

        /// <summary>Which dye model colours an item worn in this slot (0: none). Mirrors GamePlayerInventory.DyeItem.</summary>
        public static int DyeModelFor(eInventorySlot slot, int objectType, int itemType)
        {
            if (slot == eInventorySlot.Cloak)
                return objectType == (int)eObjectType.Magical && itemType == 26 ? ClothDye : 0;
            return objectType switch
            {
                (int)eObjectType.Cloth => ClothDye,
                (int)eObjectType.Leather => LeatherDye,
                >= (int)eObjectType.Studded and <= (int)eObjectType.Scale => MetalDye,
                _ => 0
            };
        }

        /// <summary>The most a bot pays for one dye: a seventieth of its saved copper, one silver at least.</summary>
        public static long PriceCeiling(long moneyCopper) => Math.Max(MinimumPriceCeilingCopper, moneyCopper / SetBudgetDivisor);

        /// <summary>A real colouring item: "... dye" or "... enamel" (not the weapon enamel, the enamel remover or the ale that
        /// share the model).</summary>
        public static bool TryGetOffer(DbItemTemplate template, out Offer offer)
        {
            offer = default;
            if (template == null || template.Item_Type != 40 || template.Price < 1 ||
                template.Model is not (ClothDye or LeatherDye or MetalDye))
                return false;
            string name = template.Name ?? string.Empty;
            if (!name.EndsWith(" dye", StringComparison.OrdinalIgnoreCase) &&
                !name.EndsWith(" enamel", StringComparison.OrdinalIgnoreCase))
                return false;
            offer = new(template.Model, template.Color, template.Price);
            return true;
        }

        /// <summary>The bot's own order of the colours in a stock: ascending, started at the bot's position among them.</summary>
        public static int[] PersonalOrder(long botId, IEnumerable<Offer> stock)
        {
            int[] colors = stock.Select(offer => offer.Color).Distinct().OrderBy(color => color).ToArray();
            if (colors.Length == 0)
                return colors;
            int start = (int)(Math.Abs(botId) % colors.Length);
            return Enumerable.Range(0, colors.Length).Select(step => colors[(start + step) % colors.Length]).ToArray();
        }

        /// <summary>
        /// A first colour for the bot from this stock: in its personal order (that of the whole capital stock when given, so
        /// that the later moves up BetterColor makes are moves in the same order), the first colour in stock that covers every
        /// material the bot wears; failing that, the colour covering the most.
        /// </summary>
        public static int? ChooseColor(long botId, IReadOnlyCollection<int> neededModels, IEnumerable<Offer> stock,
            IReadOnlyCollection<int> order = null)
        {
            int[] models = neededModels.Distinct().ToArray();
            Offer[] offers = stock.ToArray();
            if (models.Length == 0)
                return null;
            int bestColor = 0, bestCoverage = 0;
            foreach (int color in order ?? PersonalOrder(botId, offers))
            {
                int coverage = models.Count(model => offers.Any(offer => offer.Color == color && offer.DyeModel == model));
                if (coverage == models.Length)
                    return color;
                if (coverage > bestCoverage)
                {
                    bestCoverage = coverage;
                    bestColor = color;
                }
            }
            return bestCoverage > 0 ? bestColor : null;
        }

        /// <summary>
        /// A richer bot may move up its personal order: a colour before its current one that covers every material and whose
        /// dyes all cost at most half the price ceiling (the margin keeps the bot from changing for a few coppers). It never
        /// moves down, so the colour changes at most once per step up the order and never back.
        /// </summary>
        public static int? BetterColor(long botId, IReadOnlyCollection<int> neededModels, IReadOnlyCollection<Offer> fullStock,
            int? current, long ceiling)
        {
            if (current == null)
                return null;
            int[] models = neededModels.Distinct().ToArray();
            int[] order = PersonalOrder(botId, fullStock);
            int currentIndex = Array.IndexOf(order, current.Value);
            for (int index = 0; index < currentIndex; index++)
            {
                int color = order[index];
                if (models.All(model => fullStock.Any(offer => offer.Color == color && offer.DyeModel == model &&
                                                               offer.Price <= ceiling / 2)))
                    return color;
            }
            return null;
        }

        /// <summary>
        /// The colour the bot already wears most, when the whole stock sells it for every material the bot has on: a bot keeps
        /// its colour as its money (and so its palette) changes and as pieces are replaced.
        /// </summary>
        public static int? StickyColor(IReadOnlyCollection<int> wornColors, IReadOnlyCollection<int> neededModels, IEnumerable<Offer> fullStock,
            IReadOnlyList<int> order = null)
        {
            Offer[] stock = fullStock.ToArray();
            int[] models = neededModels.Distinct().ToArray();
            return wornColors.Where(color => color != 0)
                .GroupBy(color => color)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => order == null ? group.Key : order.ToList().IndexOf(group.Key) is var index && index >= 0 ? index : int.MaxValue)
                .Select(group => group.Key)
                .Where(color => models.All(model => stock.Any(offer => offer.Color == color && offer.DyeModel == model)))
                .Select(color => (int?)color)
                .FirstOrDefault();
        }

        /// <summary>Worn pieces that this bot may tint, with the dye model each needs.</summary>
        public static List<(DbInventoryItem Item, int DyeModel)> DyeablePieces(GameBot bot)
        {
            List<(DbInventoryItem, int)> pieces = new();
            if (bot?.Inventory == null)
                return pieces;
            foreach (eInventorySlot slot in Slots)
            {
                DbInventoryItem item = bot.Inventory.GetItem(slot);
                if (item == null || SluaghbinderEpicArmor.IsSetItem(item.Id_nb))
                    continue;
                int model = DyeModelFor(slot, item.Object_Type, item.Item_Type);
                if (model != 0)
                    pieces.Add((item, model));
            }
            return pieces;
        }

        private static bool IsDyeingBot(GameBot bot) =>
            bot?.IsAutonomousWorldBot == true && !bot.IsTemporaryGroupHelper && bot.Level >= MinimumLevel && bot.Inventory != null;

        private static ushort CapitalRegionId(eRealm realm) => realm switch
        {
            eRealm.Albion => 10,
            eRealm.Midgard => 101,
            eRealm.Hibernia => 201,
            _ => (ushort)0
        };

        public static bool IsRealmCapital(GameBot bot, GameMerchant merchant) =>
            bot != null && merchant?.CurrentRegion != null && merchant.CurrentRegion.IsCapitalCity &&
            merchant.CurrentRegionID == CapitalRegionId(bot.Realm) &&
            (merchant.Realm == eRealm.None || merchant.Realm == bot.Realm);

        /// <summary>The dye merchants of the realm's capital and what each sells (cached ten minutes).</summary>
        public static MerchantStock[] CapitalStock(eRealm realm)
        {
            long now = GameLoop.GameLoopTime;
            if (StockCache.TryGetValue(realm, out var cached) && now < cached.ExpiresTick)
                return cached.Stock;
            Region region = WorldMgr.GetRegion(CapitalRegionId(realm));
            MerchantStock[] stock = region == null ? Array.Empty<MerchantStock>() : region.Merchants
                .Where(merchant => merchant?.TradeItems != null && merchant.ObjectState is GameObject.eObjectState.Active &&
                                   (merchant.Realm == eRealm.None || merchant.Realm == realm))
                .Select(merchant => new MerchantStock(merchant, OffersOf(merchant)))
                .Where(entry => entry.Offers.Length > 0)
                .ToArray();
            StockCache[realm] = (now + StockCacheMilliseconds, stock);
            return stock;
        }

        private static Offer[] OffersOf(GameMerchant merchant)
        {
            List<Offer> offers = new();
            foreach (DbItemTemplate template in merchant.TradeItems.GetAllItems().Values.OfType<DbItemTemplate>())
                if (TryGetOffer(template, out Offer offer))
                    offers.Add(offer);
            return offers.ToArray();
        }

        /// <summary>The colour and the pieces still to tint (those with a dye of that colour the bot can pay for), or false when
        /// there is nothing to do.</summary>
        public static bool TryPlan(GameBot bot, out int color, out List<(DbInventoryItem Item, int DyeModel)> pieces)
        {
            color = 0;
            pieces = null;
            if (!IsDyeingBot(bot))
                return false;
            List<(DbInventoryItem Item, int DyeModel)> worn = DyeablePieces(bot);
            if (worn.Count == 0)
                return false;
            Offer[] full = CapitalStock(bot.Realm).SelectMany(entry => entry.Offers).ToArray();
            long ceiling = PriceCeiling(AutonomousBotEconomy.GetMoney(bot.DatabaseID));
            Offer[] affordable = full.Where(offer => offer.Price <= ceiling).ToArray();
            int[] models = worn.Select(piece => piece.DyeModel).ToArray();
            int[] order = PersonalOrder(bot.DatabaseID, full);
            int? sticky = StickyColor(worn.Select(piece => piece.Item.Color).ToArray(), models, full, order);
            int? chosen = BetterColor(bot.DatabaseID, models, full, sticky, ceiling) ?? sticky
                          ?? ChooseColor(bot.DatabaseID, models, affordable, order);
            if (chosen == null)
                return false;
            int wanted = chosen.Value;
            color = wanted;
            pieces = worn.Where(piece => piece.Item.Color != wanted &&
                                         affordable.Any(offer => offer.Color == wanted && offer.DyeModel == piece.DyeModel)).ToList();
            return pieces.Count > 0;
        }

        private static Trip TripOf(GameBot bot)
        {
            return Trips.GetOrAdd(bot.DatabaseID, id =>
                // Staggered so that a server start does not send every bot to the capital at once.
                new Trip { NextTripTick = GameLoop.GameLoopTime + (id % 7200) * 1000 });
        }

        /// <summary>Whether a dye trip is wanted now: a dyeing bot with pieces to tint, money, and no trip in the last two hours.</summary>
        public static bool NeedsTrip(GameBot bot)
        {
            if (!IsDyeingBot(bot) || GameLoop.GameLoopTime < TripOf(bot).NextTripTick)
                return false;
            if (!TryPlan(bot, out int color, out var pieces))
                return false;
            return NextMerchant(bot, color, pieces) != null || FinishTrip(bot, "no merchant in reach", RetryDelay);
        }

        /// <summary>Whether the bot has already been served (or tried) at this merchant on its current trip: the world controller
        /// then looks for the next one instead of keeping the bot where it stands.</summary>
        public static bool HasPassed(GameBot bot, GameMerchant merchant) =>
            merchant != null && bot?.DatabaseID > 0 && TripOf(bot).Visited.Contains(merchant.ObjectID);

        /// <summary>One line per bot every five minutes saying why a bot at a capital dye merchant bought nothing (2026-10-28: a
        /// Friar stood fifteen minutes at Dyemaster Emma and nothing said why).</summary>
        private static void ReportMiss(GameBot bot, GameMerchant merchant, string reason)
        {
            long now = GameLoop.GameLoopTime;
            if (LastMissReport.TryGetValue(bot.DatabaseID, out long last) && now - last < 5 * 60_000) return;
            LastMissReport[bot.DatabaseID] = now;
            if (Log.IsInfoEnabled)
                Log.Info($"AUTONOMOUS_DYE_MISS bot=\"{bot.Name}\" id={bot.DatabaseID} level={bot.Level} class=\"{bot.ClassName}\" " +
                         $"merchant=\"{merchant?.Name}\" reason=\"{reason}\" money={AutonomousBotEconomy.GetMoney(bot.DatabaseID)} " +
                         $"ceiling={PriceCeiling(AutonomousBotEconomy.GetMoney(bot.DatabaseID))} visited={TripOf(bot).Visited.Count}");
        }

        /// <summary>The nearest capital merchant (not yet passed on this trip) that sells the bot's colour for a piece it still
        /// has to tint, or null.</summary>
        public static GameMerchant FindTripMerchant(GameBot bot)
        {
            if (!IsDyeingBot(bot) || !TryPlan(bot, out int color, out var pieces))
                return null;
            return NextMerchant(bot, color, pieces);
        }

        private static GameMerchant NextMerchant(GameBot bot, int color, List<(DbInventoryItem Item, int DyeModel)> pieces)
        {
            Trip trip = TripOf(bot);
            long ceiling = PriceCeiling(AutonomousBotEconomy.GetMoney(bot.DatabaseID));
            return CapitalStock(bot.Realm)
                .Where(entry => !trip.Visited.Contains(entry.Merchant.ObjectID) &&
                                pieces.Any(piece => entry.Offers.Any(offer => offer.Color == color && offer.DyeModel == piece.DyeModel &&
                                                                            offer.Price <= ceiling)))
                .Select(entry => entry.Merchant)
                .OrderBy(merchant => merchant.CurrentRegionID == bot.CurrentRegionID ? bot.GetDistanceTo(merchant) : 0)
                .FirstOrDefault(merchant => AutonomousWorldBotController.EstimateClassicTripMinutes(
                    bot, merchant.CurrentRegionID, merchant.X, merchant.Y) <= MaximumTripMinutes);
        }

        /// <summary>Ends the trip: the next one waits the cooldown (two hours unless told otherwise) from now, and the merchants
        /// passed are forgotten. Always returns false.</summary>
        public static bool FinishTrip(GameBot bot, string reason, TimeSpan? delay = null)
        {
            if (bot?.DatabaseID > 0)
            {
                Trip trip = TripOf(bot);
                trip.NextTripTick = GameLoop.GameLoopTime + (long)(delay ?? TripCooldown).TotalMilliseconds;
                trip.Visited.Clear();
            }
            return false;
        }

        /// <summary>
        /// At a capital dye merchant: pays the cheapest dye of the bot's colour it can afford for each piece the merchant sells
        /// one for, and tints the piece. Returns true when at least one piece was tinted. The trip ends when no other merchant
        /// has anything left for the bot.
        /// </summary>
        public static bool TryDyeAtMerchant(GameBot bot, GameMerchant merchant, out int dyed, out long spent)
        {
            dyed = 0;
            spent = 0;
            if (!IsDyeingBot(bot) || !IsRealmCapital(bot, merchant) || merchant.TradeItems == null ||
                GameLoop.GameLoopTime < TripOf(bot).NextTripTick)
                return false;
            // The step runs at any capital merchant the bot uses for another service: a merchant that sells no dye at all
            // (Hibernia Magicals, 2026-10-28) is neither a miss nor a stop of the trip.
            if (!CapitalStock(bot.Realm).Any(entry => entry.Merchant == merchant))
                return false;
            if (!merchant.IsWithinRadius(bot, DOL.GS.ServerProperties.Properties.WORLD_PICKUP_DISTANCE) ||
                !TryPlan(bot, out int color, out var pieces))
                return false;

            // A dye stop for this bot is a merchant that sells its colour for a piece it can pay for: the step also runs at any
            // capital merchant used for another service, and there it neither reports a miss nor counts as passed on the trip.
            Offer[] offers = OffersOf(merchant);
            long firstCeiling = PriceCeiling(AutonomousBotEconomy.GetMoney(bot.DatabaseID));
            if (!pieces.Any(piece => offers.Any(offer => offer.Color == color && offer.DyeModel == piece.DyeModel &&
                                                         offer.Price <= firstCeiling)))
                return false;

            Trip trip = TripOf(bot);
            trip.Visited.Add(merchant.ObjectID);
            foreach ((DbInventoryItem item, int model) in pieces)
            {
                // The ceiling is read again for every piece: the bot's money falls with each purchase.
                long ceiling = PriceCeiling(AutonomousBotEconomy.GetMoney(bot.DatabaseID));
                Offer[] options = offers.Where(offer => offer.Color == color && offer.DyeModel == model && offer.Price <= ceiling)
                    .OrderBy(offer => offer.Price).ToArray();
                if (options.Length == 0)
                    continue;
                if (!AutonomousBotEconomy.TrySpend(bot.DatabaseID, options[0].Price))
                {
                    if (dyed == 0)
                        ReportMiss(bot, merchant, $"the payment of {options[0].Price} copper failed");
                    FinishTrip(bot, "no money");
                    break;
                }
                item.Color = color;
                dyed++;
                spent += options[0].Price;
            }

            if (dyed == 0)
                ReportMiss(bot, merchant, $"bought nothing at a merchant that sells colour {color} for a piece it can pay for " +
                                          $"(pieces {pieces.Count}, models {string.Join('/', pieces.Select(piece => piece.DyeModel).Distinct())}, " +
                                          $"offers {offers.Length})");
            if (dyed > 0)
            {
                bot.BroadcastLivingEquipmentUpdate();
                AutonomousBotEconomy.MarkInventoryChanged(bot);
                bot.MarkAutonomousStateDirty();
                AutonomousBotStatusPersistence.Queue(bot, true);
                if (Log.IsInfoEnabled)
                    Log.Info($"AUTONOMOUS_DYE bot=\"{bot.Name}\" id={bot.DatabaseID} level={bot.Level} class=\"{bot.ClassName}\" " +
                             $"merchant=\"{merchant.Name}\" color={color} pieces={dyed} spentCopper={spent}");
            }

            if (!TryPlan(bot, out int nextColor, out var remaining) || NextMerchant(bot, nextColor, remaining) == null)
                FinishTrip(bot, "done");
            return dyed > 0;
        }
    }
}
