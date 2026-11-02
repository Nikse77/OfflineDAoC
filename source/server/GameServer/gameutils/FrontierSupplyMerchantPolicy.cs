using System;
using System.Collections.Generic;
using DOL.Database;

namespace DOL.GS
{
    public static class FrontierSupplyMerchantPolicy
    {
        private static readonly HashSet<string> SiegeKits = new(StringComparer.OrdinalIgnoreCase)
        {
            "deploy_heavy_siege_ram",
            "deploy_siege_ram", "deploy_siege_ram2", "deploy_siege_ram3",
            "deploy_siege_trebuchet", "deploy_siege_trebuchet2", "deploy_siege_trebuchet3",
            "deploy_siege_ballista", "deploy_siege_ballista2", "deploy_siege_ballista3",
            "deploy_siege_catapult", "deploy_siege_catapult2", "deploy_siege_catapult3"
        };

        public static string Label(string list) => list switch
        {
            "SiegeMerchantAlb" or "SiegeMerchantMid" or "SiegeMerchantHib" => "Siege Equipment",
            "OFMerchant_Alb" or "OFMerchant_Mid" or "OFMerchant_Hib" or
            "OFMerchant_Alb_Home" or "OFMerchant_Mid_Home" or
            "OFMerchant_Alb_HomeHib" or "OFMerchant_Alb_HomeMid" or
            "OFMerchant_Mid_HomeHib" or "OFMerchant_Mid_HomeAlb" or
            "OFMerchant_Hib_Home" or "OFMerchant_Hib_HomeAlb" or "OFMerchant_Hib_HomeMid" => "Teleport Medallions",
            _ => null
        };

        /// <summary>
        /// Owner 2026-11-02: the list a frontier medallion merchant carries, chosen by the frontier it stands in -
        /// at its own keep both enemy frontiers, in a foreign frontier the home necklace plus the other enemy
        /// frontier's. A warband that changes frontier always finds a medallion where it is, so the crossing stays a
        /// single passage. The last branch is the fallback for a merchant that has not been placed in a region yet.
        /// </summary>
        public static string MedallionStock(eRealm realm, ushort region) => (realm, region) switch
        {
            (eRealm.Albion, 100) => "OFMerchant_Alb_HomeMid",
            (eRealm.Albion, 200) => "OFMerchant_Alb_HomeHib",
            (eRealm.Albion, _) => "OFMerchant_Alb_Home",
            (eRealm.Midgard, 1) => "OFMerchant_Mid_HomeAlb",
            (eRealm.Midgard, 200) => "OFMerchant_Mid_HomeHib",
            (eRealm.Midgard, _) => "OFMerchant_Mid_Home",
            (eRealm.Hibernia, 1) => "OFMerchant_Hib_HomeAlb",
            (eRealm.Hibernia, 100) => "OFMerchant_Hib_HomeMid",
            (eRealm.Hibernia, _) => "OFMerchant_Hib_Home",
            _ => null,
        };

        public static void ApplyLabel(GameMerchant merchant)
        {
            string label = Label(merchant?.TradeItems?.ItemsListID);
            if (label == null) return;
            merchant.GuildName = label;
            merchant.Flags &= ~GameNPC.eFlags.DONTSHOWNAME;
        }

        // Only ordinary, purchasable siege kits bypass the no-drop sale gate.
        // Preserve no-drop flags so automatic inventory cleanup keeps needed rams.
        public static bool CanSell(DbInventoryItem item) => item != null &&
            (item.IsDropable || item.IsTradable && item.Price > 0 && SiegeKits.Contains(item.Id_nb ?? ""));
    }
}
