using System;
using System.Reflection;
using DOL.GS.PacketHandler;
using DOL.Database;
using DOL.Events;
using DOL.AI.Brain;



namespace DOL.GS.Scripts
{
    public class OFMerchant : GameMerchant
    {
        #region Constructor

        public OFMerchant()
            : base()
        {
            SetOwnBrain(new BlankBrain());
        }

        #endregion Constructor

        #region AddToWorld

        public override bool AddToWorld()
        {
            GuildName = "Medallion Merchant";
            Level = 75;
            Flags |= eFlags.PEACE;

            switch (Realm)
            {
                case eRealm.Albion:
                    Name = "Sall Fadri";
                    Model = 61;
                    TradeItems = new MerchantTradeItems("OFMerchant_Alb");
                    break;
                case eRealm.Midgard:
                    Name = "Gwulla";
                    Model = 215;
                    TradeItems = new MerchantTradeItems("OFMerchant_Mid");
                    break;
                case eRealm.Hibernia:
                    Name = "Araisa";
                    Model = 342;
                    TradeItems = new MerchantTradeItems("OFMerchant_Hib");
                    break;
                default:
                    break;
            }
         
            MaxSpeedBase = 0;


            return base.AddToWorld();
        }

        #endregion AddToWorld

    }
    
    public class OFMerchantHome : GameMerchant
    {
        #region Constructor

        public OFMerchantHome()
            : base()
        {
            SetOwnBrain(new BlankBrain());
        }

        #endregion Constructor

        /// <summary>
        /// Owner 2026-11-02: the stock used to be chosen inside AddToWorld from CurrentRegionID, which is still 0 on
        /// the spawn path that adds the NPC before its region is assigned - four of the nine medallion merchants fell
        /// into the home-only fallback, so a warband standing in a foreign frontier could not buy the necklace of the
        /// frontier it needed. Selecting on every region assignment makes the region the merchant really stands in the
        /// last word, whatever order the spawner used.
        /// </summary>
        public override Region CurrentRegion
        {
            get => base.CurrentRegion;
            set
            {
                base.CurrentRegion = value;
                SelectStock();
            }
        }

        private void SelectStock()
        {
            string list = FrontierSupplyMerchantPolicy.MedallionStock(Realm, CurrentRegionID);
            if (list != null) TradeItems = new MerchantTradeItems(list);
        }

        #region AddToWorld

        public override bool AddToWorld()
        {
            GuildName = "Medallion Merchant";
            Level = 75;
            Flags |= eFlags.PEACE;

            switch (Realm)
            {
                case eRealm.Albion:
                    Name = "Sall Fadri";
                    Model = 61;
                    break;
                case eRealm.Midgard:
                    Name = "Gwulla";
                    Model = 215;
                    break;
                case eRealm.Hibernia:
                    Name = "Araisa";
                    Model = 342;
                    break;
            }

            SelectStock();
            MaxSpeedBase = 0;
            return base.AddToWorld();
        }

        #endregion AddToWorld


        public override bool Interact(GamePlayer player)
        {
            player.Out.SendMerchantWindow(TradeItems, eMerchantWindowType.Normal);
            return true;
        }
    }  
 }

