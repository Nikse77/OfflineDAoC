using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Threading;
using DOL.Database;

namespace DOL.GS
{
	public enum eMerchantWindowSlot : int
	{
		FirstEmptyInPage = -2,
		Invalid = -1,

		FirstInPage = 0,
		LastInPage = MerchantTradeItems.MAX_ITEM_IN_TRADEWINDOWS - 1,
	}

	/// <summary>
	/// This class represents a full merchant item list
	/// and contains functions that can be used to
	/// add and remove items
	/// </summary>
	public class MerchantTradeItems
	{
		/// <summary>
		/// Defines a logger for this class.
		/// </summary>
		private static readonly Logging.Logger log = Logging.LoggerManager.Create(MethodBase.GetCurrentMethod().DeclaringType);

		/// <summary>
		/// The maximum number of items on one page
		/// </summary>
		public const byte MAX_ITEM_IN_TRADEWINDOWS = 30;

		/// <summary>
		/// The maximum number of pages supported by clients
		/// </summary>
		public const int MAX_PAGES_IN_TRADEWINDOWS = 5;

		#region Constructor/Declaration

		// for client one page is 30 items, just need to use scrollbar to see them all
		// item30 will be on page 0
		// item31 will be on page 1

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="itemsListId"></param>
		public MerchantTradeItems(string itemsListId)
		{
			m_itemsListID = itemsListId;
		}

		/// <summary>
		/// Item list id
		/// </summary>
		protected string m_itemsListID;

		/// <summary>
		/// Item list id
		/// </summary>
		public string ItemsListID
		{
			get { return m_itemsListID; }
		}

		/// <summary>
		/// Holds item template instances defined with script
		/// </summary>
		protected HybridDictionary m_usedItemsTemplates = new HybridDictionary();
		private readonly Lock _lock = new();
		private Hashtable _databaseItemsCache;
		private long _databaseItemsCacheExpiresTick;
		private const int DatabaseItemsCacheLifetimeMilliseconds = 20_000;

		#endregion

		#region Add Trade Item

		/// <summary>
		/// Adds an item to the merchant item list
		/// </summary>
		/// <param name="page">Zero-based page number</param>
		/// <param name="slot">Zero-based slot number</param>
		/// <param name="item">The item template to add</param>
		public virtual bool AddTradeItem(int page, eMerchantWindowSlot slot, DbItemTemplate item)
		{
			lock (_lock)
			{
				if (item == null)
				{
					return false;
				}

				eMerchantWindowSlot pageSlot = GetValidSlot(page, slot);

				if (pageSlot == eMerchantWindowSlot.Invalid)
				{
					log.ErrorFormat("Invalid slot {0} specified for page {1} of TradeItemList {2}", slot, page, ItemsListID);
					return false;
				}

				m_usedItemsTemplates[(page*MAX_ITEM_IN_TRADEWINDOWS)+(int)pageSlot] = item;
			}

			return true;
		}

		/// <summary>
		/// Removes an item from trade window
		/// </summary>
		/// <param name="page">Zero-based page number</param>
		/// <param name="slot">Zero-based slot number</param>
		/// <returns>true if removed</returns>
		public virtual bool RemoveTradeItem(int page, eMerchantWindowSlot slot)
		{
			lock (_lock)
			{
				slot = GetValidSlot(page, slot);
				if (slot == eMerchantWindowSlot.Invalid) return false;
				if (!m_usedItemsTemplates.Contains((page*MAX_ITEM_IN_TRADEWINDOWS)+(int)slot)) return false;
				m_usedItemsTemplates.Remove((page*MAX_ITEM_IN_TRADEWINDOWS)+(int)slot);
				return true;
			}
		}

		#endregion

		#region Get Inventory Informations

		/// <summary>
		/// Get the list of all items in the specified page
		/// </summary>
		// The world database holds merchant rows stored twice (the same item, the same slot) and slots shared by two different
		// items (2026-10-29: 118 items of 11 lists were never shown). Duplicates are ignored; a second item takes a free slot.
		private static readonly HashSet<string> s_loggedSlotConflicts = new();
		private static readonly HashSet<string> s_loggedMissingTemplates = new();

		private static bool PageShows(Hashtable layout, int page, DbItemTemplate item)
		{
			for (int slot = 0; slot < MAX_ITEM_IN_TRADEWINDOWS; slot++)
				if (layout[page * MAX_ITEM_IN_TRADEWINDOWS + slot] is DbItemTemplate shown &&
				    shown.Id_nb.Equals(item.Id_nb, StringComparison.OrdinalIgnoreCase))
					return true;
			return false;
		}

		private void LogSlotConflictOnce(int page, int slot, int movedTo)
		{
			lock (s_loggedSlotConflicts)
			{
				if (!s_loggedSlotConflicts.Add($"{m_itemsListID}:{page}:{slot}"))
					return;
			}
			log.WarnFormat("merchant item without a free place of its own: listID={0} page={1} slot={2}; it {3}",
				m_itemsListID, page, slot, movedTo >= 0
					? $"is shown on page {movedTo / MAX_ITEM_IN_TRADEWINDOWS}, slot {movedTo % MAX_ITEM_IN_TRADEWINDOWS}"
					: "does not fit on any page");
		}

		/// <summary>
		/// The list's database rows as absolute slot (page * 30 + slot) to item. The world database holds rows stored twice and slots
		/// shared by two different items (2026-10-29: 118 items of 11 lists were never shown). An item already on its page is shown once;
		/// an item whose slot is taken, or outside the page, goes to the first free slot of its page or of a following page.
		/// </summary>
		private Hashtable DatabaseLayout()
		{
			lock (_lock)
			{
				long now = GameLoop.GameLoopTime;
				if (_databaseItemsCache == null || now >= _databaseItemsCacheExpiresTick)
				{
					_databaseItemsCache = LayoutDatabaseItems();
					_databaseItemsCacheExpiresTick = now + DatabaseItemsCacheLifetimeMilliseconds;
				}
				return _databaseItemsCache;
			}
		}

		private Hashtable LayoutDatabaseItems()
		{
			var layout = new Hashtable();
			if (string.IsNullOrEmpty(m_itemsListID))
				return layout;

			var leftovers = new List<(DbMerchantItem Row, DbItemTemplate Item)>();
			foreach (DbMerchantItem row in DOLDB<DbMerchantItem>.SelectObjects(DB.Column("ItemListID").IsEqualTo(m_itemsListID))
					.OrderBy(r => r.PageNumber).ThenBy(r => r.SlotPosition))
			{
				DbItemTemplate item = GameServer.Database.FindObjectByKey<DbItemTemplate>(row.ItemTemplateID);
				if (item == null)
				{
					bool first;
					lock (s_loggedMissingTemplates)
						first = s_loggedMissingTemplates.Add($"{m_itemsListID}:{row.ItemTemplateID}");
					if (first)
						log.WarnFormat("Item template with ID = '{0}' not found for merchant item list '{1}'; it is not offered", row.ItemTemplateID, m_itemsListID);
					continue;
				}
				if (row.PageNumber < 0 || row.PageNumber >= MAX_PAGES_IN_TRADEWINDOWS || PageShows(layout, row.PageNumber, item))
					continue;
				if (row.SlotPosition >= 0 && row.SlotPosition < MAX_ITEM_IN_TRADEWINDOWS &&
				    layout[row.PageNumber * MAX_ITEM_IN_TRADEWINDOWS + row.SlotPosition] == null)
					layout.Add(row.PageNumber * MAX_ITEM_IN_TRADEWINDOWS + row.SlotPosition, item);
				else
					leftovers.Add((row, item));
			}

			// Second pass, so that no leftover takes a slot a later row asks for.
			foreach ((DbMerchantItem row, DbItemTemplate item) in leftovers)
			{
				if (PageShows(layout, row.PageNumber, item))
					continue;
				int placed = -1;
				for (int page = row.PageNumber; page < MAX_PAGES_IN_TRADEWINDOWS && placed < 0; page++)
				{
					if (page != row.PageNumber && PageShows(layout, page, item))
						continue;
					for (int slot = 0; slot < MAX_ITEM_IN_TRADEWINDOWS && placed < 0; slot++)
						if (layout[page * MAX_ITEM_IN_TRADEWINDOWS + slot] == null)
							placed = page * MAX_ITEM_IN_TRADEWINDOWS + slot;
				}
				LogSlotConflictOnce(row.PageNumber, row.SlotPosition, placed);
				if (placed >= 0)
					layout.Add(placed, item);
			}
			return layout;
		}

		public virtual IDictionary GetItemsInPage(int page)
		{
			try
			{
				HybridDictionary itemsInPage = new HybridDictionary(MAX_ITEM_IN_TRADEWINDOWS);
				Hashtable layout = DatabaseLayout();
				for (int slot = 0; slot < MAX_ITEM_IN_TRADEWINDOWS; slot++)
					if (layout[page * MAX_ITEM_IN_TRADEWINDOWS + slot] is DbItemTemplate shown)
						itemsInPage.Add(slot, shown);
				lock (_lock)
				{
					foreach (DictionaryEntry de in m_usedItemsTemplates)
					{
						if ((int)de.Key >= (MAX_ITEM_IN_TRADEWINDOWS*page) && (int)de.Key < (MAX_ITEM_IN_TRADEWINDOWS*page+MAX_ITEM_IN_TRADEWINDOWS))
							itemsInPage[(int)de.Key%MAX_ITEM_IN_TRADEWINDOWS] = (DbItemTemplate)de.Value;
					}
				}
				return itemsInPage;
			}
			catch (Exception e)
			{
				if (log.IsErrorEnabled)
					log.Error("Loading merchant items list (" + m_itemsListID + ") page (" + page + "): ", e);
				return new HybridDictionary();
			}
		}

		/// <summary>
		/// Get the item in the specified page and slot
		/// </summary>
		/// <param name="page">The item page</param>
		/// <param name="slot">The item slot</param>
		/// <returns>Item template or null</returns>
		public virtual DbItemTemplate GetItem(int page, eMerchantWindowSlot slot)
		{
			try
			{
				slot = GetValidSlot(page, slot);
				if (slot == eMerchantWindowSlot.Invalid) return null;

				DbItemTemplate item;
				lock (_lock)
				{
					item = m_usedItemsTemplates[(int)slot+(page*MAX_ITEM_IN_TRADEWINDOWS)] as DbItemTemplate;
					if (item != null) return item;
				}

				// The same layout as the window shows, so that an item moved to a free slot is also the one bought from it.
				if (m_itemsListID != null && m_itemsListID.Length > 0 && slot >= eMerchantWindowSlot.FirstInPage)
					item = DatabaseLayout()[page * MAX_ITEM_IN_TRADEWINDOWS + (int)slot] as DbItemTemplate;
				return item;
			}
			catch (Exception e)
			{
				if (log.IsErrorEnabled)
					log.Error("Loading merchant items list (" + m_itemsListID + ") page (" + page + ") slot (" + slot + "): ", e);
				return null;
			}
		}

		/// <summary>
		/// Gets a copy of all intems in trade window
		/// </summary>
		/// <returns>A list where key is the slot position and value is the ItemTemplate</returns>
		public virtual IDictionary GetAllItems()
		{
			try
			{
				Hashtable allItems = new Hashtable();
				lock (_lock)
				{
					foreach (DictionaryEntry de in DatabaseLayout())
						allItems[de.Key] = de.Value;
					foreach (DictionaryEntry de in m_usedItemsTemplates)
					{
						allItems[(int)de.Key] = (DbItemTemplate)de.Value;
					}
				}
				return allItems;
			}
			catch (Exception e)
			{
				if (log.IsErrorEnabled)
					log.Error("Loading merchant items list (" + m_itemsListID + "):", e);
				return new HybridDictionary();
			}
		}

		/// <summary>
		/// Check if the slot is valid
		/// </summary>
		/// <param name="page">Zero-based page number</param>
		/// <param name="slot">SlotPosition to check</param>
		/// <returns>the slot if it's valid or eMerchantWindowSlot.Invalid if not</returns>
		public virtual eMerchantWindowSlot GetValidSlot(int page, eMerchantWindowSlot slot)
		{
			if (page < 0 || page >= MAX_PAGES_IN_TRADEWINDOWS) return eMerchantWindowSlot.Invalid;

			if (slot == eMerchantWindowSlot.FirstEmptyInPage)
			{
				IDictionary itemsInPage = GetItemsInPage(page);
				for (int i = (int)eMerchantWindowSlot.FirstInPage; i < (int)eMerchantWindowSlot.LastInPage; i++)
				{
					if (!itemsInPage.Contains(i))
						return ((eMerchantWindowSlot)i);
				}
				return eMerchantWindowSlot.Invalid;
			}

			if (slot < eMerchantWindowSlot.FirstInPage || slot > eMerchantWindowSlot.LastInPage)
				return eMerchantWindowSlot.Invalid;

			return slot;
		}

		#endregion
	}
}
