using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using DOL.Database;
using DOL.GS.Keeps;
using DOL.Logging;

namespace DOL.GS
{
	/// <summary>
	/// DoorMgr is manager of all door regular door and keep door
	/// </summary>
	public sealed class DoorMgr
	{
		private static readonly Logger log = LoggerManager.Create(MethodBase.GetCurrentMethod().DeclaringType);

		private static readonly Lock Lock = new();

		private static Dictionary<int, GameDoorBase> m_doors = new();

		public const string WANT_TO_ADD_DOORS = "WantToAddDoors";

		/// <summary>
		/// this function load all door from DB
		/// </summary>
		public static bool Init()
		{
			var dbdoors = GameServer.Database.SelectAllObjects<DbDoor>();
			foreach (DbDoor door in dbdoors)
			{
				if (!LoadDoor(door))
				{
					log.Error("Unable to load door id " + door.ObjectId + ", correct your database");
					// continue loading, no need to stop server for one bad door!
				}
			}
			return true;
		}

		public static int SaveKeepDoors()
		{
			int count = 0;

			try
			{
				lock (Lock)
				{
					foreach (GameDoorBase door in m_doors.Values)
					{
						if (door.DbDoor != null &&
							door is GameKeepDoor keepDoor &&
							keepDoor.IsAttackableDoor)
						{
							keepDoor.SaveIntoDatabase();
							count++;
						}
					}
				}
			}
			catch (Exception e)
			{
				if (log.IsErrorEnabled)
					log.Error("Error saving keep doors.", e);
			}

			return count;
		}

		public static bool LoadDoor(DbDoor door)
		{
			GameDoorBase mydoor = null;
			ushort zone = (ushort)(door.InternalID / 1000000);

			Zone currentZone = WorldMgr.GetZone(zone);
			if (currentZone == null) return false;
			
			//check if the door is a keep door
			foreach (AbstractArea area in currentZone.GetAreasOfSpot(door.X, door.Y, door.Z))
			{
				if (area is KeepArea)
				{
					mydoor = new GameKeepDoor();
					mydoor.LoadFromDatabase(door);
					break;
				}
			}

			//if the door is not a keep door, create a standard door
			if (mydoor == null)
			{
				mydoor = new GameDoor();
				mydoor.LoadFromDatabase(door);
			}

			//add to the list of doors
			if (mydoor != null)
			{
				RegisterDoor(mydoor);
			}

			return true;
		}

		public static void RegisterDoor(GameDoorBase door)
		{
			lock (Lock)
			{
				if (m_doors.TryGetValue(door.DoorId, out GameDoorBase existingDoor))
				{
					if (door == existingDoor)
						return;
				}
				else
				{
					// Track doors that can't be opened via interaction, so they can be treated as obstacles.
					if (!door.CanBeOpenedViaInteraction && !PathfindingProvider.Instance.RegisterDoor(door))
					{
						// 2026-11-04: most of these doors stand within a few hundred units of a door that registered first and
						// already took the shared door polygons (their Door flag is gone), so the mesh is fine there; the
						// message is a warning for them and stays an error only for a door that has no registered neighbour.
						GameDoorBase neighbour = FindRegisteredNeighbour(door);
						string detail = $"(Id: {door.DoorId}) " +
							$"(Name: {door.Name}) " +
							$"(Loc: {door.X},{door.Y},{door.Z}) " +
							$"(ZoneId: {door.CurrentZone?.ID}) " +
							$"(RegionId: {door.CurrentRegionID})";
						if (neighbour != null)
						{
							if (log.IsWarnEnabled)
								log.Warn($"Door not registered in the pathfinding provider, probably sharing its navmesh polygons with door {neighbour.DoorId} {detail}");
						}
						else if (log.IsErrorEnabled)
							log.Error($"Failed to register door in pathfinding provider, possible navmesh/object location mismatch {detail}");
					}
				}

				m_doors[door.DoorId] = door;
			}
		}

		/// <summary>The closest already registered door of the same region within 250 units (and 400 in height), if any.</summary>
		private static GameDoorBase FindRegisteredNeighbour(GameDoorBase door)
		{
			GameDoorBase closest = null;
			double closestDistance = 250 * 250;
			foreach (GameDoorBase other in m_doors.Values)
			{
				if (other == door || other.CurrentRegionID != door.CurrentRegionID || Math.Abs(other.Z - door.Z) > 400)
					continue;
				double dx = other.X - door.X, dy = other.Y - door.Y, distance = dx * dx + dy * dy;
				if (distance <= closestDistance)
				{
					closest = other;
					closestDistance = distance;
				}
			}
			return closest;
		}

		public static void UnregisterDoor(int doorID)
		{
			m_doors.Remove(doorID);
		}

		/// <summary>
		/// This function get the door object by door index
		/// </summary>
		/// <returns>return the door with the index</returns>
		public static GameDoorBase GetDoorByID(int id)
		{
			return m_doors.TryGetValue(id, out GameDoorBase value) ? value : null;
		}
	}
}
