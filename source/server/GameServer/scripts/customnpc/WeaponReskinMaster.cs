using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DOL.Database;
using DOL.GS.PacketHandler;

namespace DOL.GS
{
    /// <summary>
    /// Owner 2026-10-31: changes the look of any weapon, and any shield, to the look of another one of the same
    /// kind, chosen among the models the item table already uses. The player hands over a weapon from the backpack, is
    /// shown the looks a page at a time, previews one on a reflection of themselves (as the Reskin NPC does) and
    /// confirms. The weapon keeps its stats and condition; it is replaced by a copy with the new model. Free, like the
    /// existing Reskin NPC.
    /// </summary>
    public class WeaponReskinMaster : GameNPC
    {
        private const string TempItem = "WeaponReskinItem";
        private const string TempPage = "WeaponReskinPage";
        private const string TempModel = "WeaponReskinModel";

        // Looks per kind of weapon, read once from the item table.
        private static readonly ConcurrentDictionary<(int ObjectType, int ItemType, int Hand, int Size), IReadOnlyList<WeaponReskinCatalog.Candidate>> Kinds = new();

        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player))
                return false;

            TurnTo(player, 500);
            SendReply(player, "I can give a weapon or a shield the look of another of its kind... free of charge.\n" +
                              "Hand me the weapon or shield from your backpack and I will show you the looks I know.");
            return true;
        }

        public override bool ReceiveItem(GameLiving source, DbInventoryItem item)
        {
            if (source is not GamePlayer player || item == null)
                return false;

            if (GetDistanceTo(player) > WorldMgr.INTERACT_DISTANCE)
            {
                player.Out.SendMessage("You are too far away to give anything to " + GetName(0, false) + ".", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (!WeaponReskinCatalog.IsReskinnable(item.Object_Type))
            {
                SendReply(player, "I only work on weapons and shields.");
                return false;
            }

            if (item.OwnerID != player.InternalID || !IsInBackpack(item))
            {
                SendReply(player, "Take it off first and put it in your backpack, then hand it to me.");
                return false;
            }

            player.TempProperties.SetProperty(TempItem, item);
            player.TempProperties.SetProperty(TempPage, 0);
            player.TempProperties.RemoveProperty(TempModel);
            ShowPage(player, item, 0);
            return false; // the weapon stays in the backpack until the change is confirmed
        }

        public override bool WhisperReceive(GameLiving source, string str)
        {
            if (!base.WhisperReceive(source, str) || source is not GamePlayer player)
                return false;

            TurnTo(player.X, player.Y);
            DbInventoryItem item = player.TempProperties.GetProperty<DbInventoryItem>(TempItem);
            if (item == null)
            {
                SendReply(player, "Hand me a weapon first.");
                return false;
            }

            string text = (str ?? string.Empty).Trim().ToLowerInvariant();
            int page = player.TempProperties.GetProperty<int>(TempPage);
            if (text == "next page" || text == "previous page")
            {
                page += text == "next page" ? 1 : -1;
                ShowPage(player, item, page);
                return true;
            }

            if (text == "cancel")
            {
                Forget(player);
                SendReply(player, "As you wish. Hand me a weapon whenever you like.");
                return true;
            }

            if (text == "confirm look")
            {
                int model = player.TempProperties.GetProperty<int>(TempModel);
                if (model <= 0)
                {
                    SendReply(player, "Choose a look first.");
                    return false;
                }
                return Apply(player, item, model);
            }

            if (text.StartsWith("look ") && int.TryParse(text.AsSpan(5), out int number))
            {
                IReadOnlyList<WeaponReskinCatalog.Look> looks = LooksFor(item);
                if (number < 1 || number > looks.Count)
                {
                    SendReply(player, "I do not know that look.");
                    return false;
                }

                WeaponReskinCatalog.Look look = looks[number - 1];
                player.TempProperties.SetProperty(TempModel, look.Model);
                DbInventoryItem preview = (DbInventoryItem)item.Clone();
                preview.Model = look.Model;
                Preview(player, preview);
                SendReply(player, $"{look.Name}, as you see on your reflection.\nIf you like it, tell me to [confirm look]; or choose another, or [cancel].");
                return true;
            }

            return false;
        }

        private static bool IsInBackpack(DbInventoryItem item) =>
            item.SlotPosition >= (int)eInventorySlot.FirstBackpack && item.SlotPosition <= (int)eInventorySlot.LastBackpack;

        private static IReadOnlyList<WeaponReskinCatalog.Look> LooksFor(DbInventoryItem item)
        {
            var kind = WeaponReskinCatalog.KindOf(item.Object_Type, item.Item_Type, item.Hand, item.Type_Damage);
            IReadOnlyList<WeaponReskinCatalog.Candidate> candidates = Kinds.GetOrAdd(kind, key =>
                GameServer.Database.SelectObjects<DbItemTemplate>(key.Hand < 0
                        ? DB.Column("Object_Type").IsEqualTo(key.ObjectType)
                            .And(DB.Column("Item_Type").IsEqualTo(key.ItemType))
                            .And(DB.Column("Type_Damage").IsEqualTo(key.Size))
                        : DB.Column("Object_Type").IsEqualTo(key.ObjectType)
                            .And(DB.Column("Item_Type").IsEqualTo(key.ItemType))
                            .And(DB.Column("Hand").IsEqualTo(key.Hand)))
                    .Select(template => new WeaponReskinCatalog.Candidate(template.Model, template.Name, template.Level))
                    .ToList());
            return WeaponReskinCatalog.Looks(candidates, item.Model);
        }

        private void ShowPage(GamePlayer player, DbInventoryItem item, int page)
        {
            IReadOnlyList<WeaponReskinCatalog.Look> looks = LooksFor(item);
            if (looks.Count == 0)
            {
                SendReply(player, "I know no other look for this kind of weapon or shield.");
                return;
            }

            int pages = WeaponReskinCatalog.PageCount(looks.Count);
            page = Math.Clamp(page, 0, pages - 1);
            player.TempProperties.SetProperty(TempPage, page);

            var text = new StringBuilder();
            text.Append($"Looks for your {item.Name} (page {page + 1} of {pages}). Tell me [look N] to see one:\n\n");
            foreach ((int number, WeaponReskinCatalog.Look look) in WeaponReskinCatalog.Page(looks, page))
                text.Append($"[look {number}] {look.Name} (level {look.Level})\n");
            text.Append('\n');
            if (page > 0)
                text.Append("[previous page]  ");
            if (page < pages - 1)
                text.Append("[next page]  ");
            text.Append("[cancel]");
            SendReply(player, text.ToString());
        }

        private void Preview(GamePlayer player, DbInventoryItem item)
        {
            var display = new DisplayModel(player, item)
            {
                X = X + 50,
                Y = Y,
                Z = Z,
                CurrentRegion = CurrentRegion,
            };
            display.AddToWorld();
            display.BroadcastLivingEquipmentUpdate();
            ClientService.UpdateNpcForPlayer(player, display);
        }

        private bool Apply(GamePlayer player, DbInventoryItem item, int model)
        {
            if (item.OwnerID != player.InternalID || !IsInBackpack(item) || player.Inventory.GetItem((eInventorySlot)item.SlotPosition) != item)
            {
                Forget(player);
                SendReply(player, "That weapon is no longer in your backpack. Please start over.");
                return false;
            }

            int slot = item.SlotPosition;
            var unique = new DbItemUnique(item.Template) { Model = model };
            DbInventoryItem changed = GameInventoryItem.Create(unique as DbItemTemplate);
            changed.Condition = item.Condition;
            changed.Durability = item.Durability;
            changed.Color = item.Color;
            changed.Emblem = item.Emblem;

            player.Inventory.RemoveItem(item);
            if (!player.Inventory.AddItem((eInventorySlot)slot, changed))
            {
                // Put the original back; nothing is lost.
                player.Inventory.AddItem((eInventorySlot)slot, item);
                Forget(player);
                SendReply(player, "I could not put the weapon back in your backpack. Please make some room and start over.");
                return false;
            }

            player.Out.SendInventoryItemsUpdate(new[] { changed });
            player.SaveIntoDatabase();
            Forget(player);
            SendReply(player, "It is done. Your weapon has its new look.");
            return true;
        }

        private static void Forget(GamePlayer player)
        {
            player.TempProperties.RemoveProperty(TempItem);
            player.TempProperties.RemoveProperty(TempPage);
            player.TempProperties.RemoveProperty(TempModel);
        }

        private static void SendReply(GamePlayer player, string message) =>
            player.Out.SendMessage(message, eChatType.CT_System, eChatLoc.CL_PopupWindow);
    }
}
