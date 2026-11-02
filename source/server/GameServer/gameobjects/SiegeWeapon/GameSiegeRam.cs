using DOL.GS.Keeps;
using DOL.GS.PacketHandler;

namespace DOL.GS
{
	public class GameSiegeRam : GameSiegeWeapon
	{
		public GameSiegeRam() : base()
		{
			MeleeDamageType = eDamageType.Crush;
			Name = "siege ram";

			//AmmoType = 0x3B00;
			//this.Effect = 0x8A1;
			AmmoType = 0x26;
			EnableToMove = false; // Disabled to prevent exploits (going through walls) and because rams are automatically placed in a correct position.
			this.Model = 0xA2A;//0xA28
			//TODO find all value for ram
			ActionDelay = new int[]
			{
				0,//none
				5000,//aiming
				10000,//arming
				0,//loading
				1100//fireing
			};//en ms
		}

		//Set the maxium rams allowed to attack a target at the same time.
		private const int MAX_RAMS_ATTACKING_TARGET = 2;

		// Owner 2026-11-01: diagnostics only. A medium ram reported 225 damage and a 14 s reload with five companions
		// standing at it, which is crew 1 - the companions were not counted. This line says how many bots the ram sees
		// and which test rejects them, so the fix is chosen from evidence instead of guessed.
		private static readonly DOL.Logging.Logger Log = DOL.Logging.LoggerManager.Create(typeof(GameSiegeRam));
		private int _loggedCrew = -1;
		private long _lastCrewLogTick;
		public override ushort Type()
		{
			return 0x9602;
		}

		public override int MAX_PASSENGERS
		{
			get { return MaxPassengersFor(Level); }
		}

		/// <summary>How many people (riders or crew) a ram of this level can carry.</summary>
		public static int MaxPassengersFor(int level)
		{
			switch (level)
			{
				case 0:
					return 2;
				case 1:
					return 6;
				case 2:
					return 8;
				case 3:
					return 12;
			}
			return level * 3;
		}

		/// <summary>Owner 2026-11-01: the people at a ram, capped by its size. Companions count too.</summary>
		public static int CrewTotal(int level, int people) =>
			System.Math.Max(0, System.Math.Min(MaxPassengersFor(level), people));

		public override int SLOT_OFFSET
		{
			get
			{
				return 1;
			}
		}

		public override void Aim()
		{
			if (Owner?.TargetObject == null) return;
			//Only allow rams to attack keep or relic doors 
			if (!(Owner.TargetObject is GameKeepDoor) && !(Owner.TargetObject is GameRelicDoor))
			{
				OwnerOut?.SendMessage("Rams can only attack doors!", eChatType.CT_System, eChatLoc.CL_SystemWindow);
				return;
			}
			//Range Check
			if (!this.IsWithinRadius(Owner.TargetObject, attackComponent.AttackRange))
			{
				if(Owner != null)
					OwnerOut?.SendMessage("You are too far away to attack " + Owner.TargetObject.Name, eChatType.CT_System, eChatLoc.CL_SystemWindow);
				return;
			}
			//Limit 2 Rams aimed at door at a time
			//Check # of rams on the target
			int ramsAimedAtTarget=0;
			foreach (GameNPC npc in Owner.TargetObject.GetNPCsInRadius(600))
			{
				if(npc is GameSiegeRam ram)
				{
					if (ram != this && ram.IsAlive && ram.Health > ram.DecayedHp && ram.TargetObject == Owner.TargetObject)
					{
						ramsAimedAtTarget++;
					}
				}
			}

			if (ramsAimedAtTarget >= MAX_RAMS_ATTACKING_TARGET)
			{
				if(Owner != null)
					OwnerOut?.SendMessage("Too many rams already attacking   " + TargetObject?.Name, eChatType.CT_System,eChatLoc.CL_SystemWindow);
				return;
			}

			base.Aim();
		}

		public override void Fire()
		{
			UpdateRamStatus();
			GameLiving target = (TargetObject as GameLiving);
			if(target != null && !target.IsAlive)
			{

				if(Owner != null)
					OwnerOut?.SendMessage(target.Name + " is already destroyed!" , eChatType.CT_System,eChatLoc.CL_SystemWindow);
				return;
			}

			base.Fire();
		}

		public override void DoDamage()
		{
			if (Owner is GameBot && !BotSiegeRuntime.CanDamage(this, TargetObject as GameLiving)) return;
			GameLiving target = (TargetObject as GameLiving);
			if (target == null)
			{
				if(Owner != null)
					OwnerOut?.SendMessage("Select a target first.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
				return;
			}

			//Only allow rams to attack keep or relic doors 
			if (!(target is GameKeepDoor) && !(target is GameRelicDoor))
			{
				if(Owner != null)
					OwnerOut?.SendMessage("Rams can only attack doors!", eChatType.CT_System, eChatLoc.CL_SystemWindow);
				return;
			}

			//todo good  distance check
			if (!this.IsWithinRadius(target, attackComponent.AttackRange))
			{
				if(Owner != null)
					OwnerOut?.SendMessage("You are too far away to attack " + target.Name, eChatType.CT_System, eChatLoc.CL_SystemWindow);
				return;
			}

			int damageAmount = CalcDamageToTarget(target);

			// Owner 2026-11-01: diagnostics only. One line whenever the crew changes, naming the rider count, the crew the
			// formula used, and how many bots the radius holds against each test - so an uncounted companion is explained.
			int crew = Crew;
			long crewLogTick = GameLoop.GameLoopTime;
			if (crew != _loggedCrew || crewLogTick - _lastCrewLogTick >= 20_000)
			{
				_loggedCrew = crew;
				_lastCrewLogTick = crewLogTick;
				var probe = ProbeCompanions();
				GamePlayer operatorPlayer = Owner as GamePlayer;
				// Owner 2026-11-01: diagnostics must never cost a shot, and the test fixtures run with no logger initialised.
				try
				{
					Log.Warn($"SIEGE_RAM_CREW level={Level} name=\"{Name}\" riders={CurrentRiders.Length} crew={crew} " +
					         $"crew_companions={probe.CrewCount} damage={damageAmount} reload_ms={GetReloadDelay} " +
					         $"owner=\"{operatorPlayer?.Name ?? "null"}\" ram_realm={(int)Realm} " +
					         $"leader_realm={(operatorPlayer != null ? (int)operatorPlayer.Realm : -1)} " +
					         $"ram_region={CurrentRegionID} " +
					         $"leader_region={(operatorPlayer != null ? operatorPlayer.CurrentRegionID : -1)} " +
					         $"bots_at_ram={probe.Seen} alive_same_realm={probe.AliveSameRealm} " +
					         $"leader_of={probe.LeaderOf} owned_by_owner={probe.OwnedByOwner} " +
					         $"group_bots={probe.GroupBots} group_alive={probe.GroupAlive} " +
					         $"group_same_realm={probe.GroupSameRealm} group_same_region={probe.GroupSameRegion} " +
					         $"group_in_600={probe.GroupInRadius}");
				}
				catch (System.Exception)
				{
					// A shot is worth more than a log line.
				}
			}

			AttackData ad = new AttackData();
			ad.Attacker = this;
			ad.Target = target;
			ad.AttackType = AttackData.eAttackType.Ranged;
			ad.AttackResult = eAttackResult.HitUnstyled;
			ad.Damage = damageAmount;
			ad.DamageType = MeleeDamageType;
			
			int before = target.Health;
			target.TakeDamage(this, eDamageType.Crush, damageAmount, 0);
			RecordBotHit(before, target);
			target.OnAttackedByEnemy(ad);

			if(Owner != null)
			{
				Owner.OnAttackEnemy(ad);
				OwnerOut?.SendMessage("The " + this.Name + " hits " + target.Name + " for " + damageAmount + " damage!", eChatType.CT_YouHit, eChatLoc.CL_SystemWindow);
				Message.SystemToArea(this, GetName(0, false) + " hits " + target.GetName(0, true), eChatType.CT_OthersCombat, Owner);
			}
			base.DoDamage();
		}

		public override bool RiderMount(GamePlayer rider, bool forced)
		{
			if (!base.RiderMount(rider, forced))
				return false;
			UpdateRamStatus();
			return true;
		}

		public override bool RiderDismount(bool forced, GamePlayer player)
		{
			if (!base.RiderDismount(forced, player))
				return false;
			if (player.SiegeWeapon == this)
				ReleaseControl();
			UpdateRamStatus();
			return true;
		}

		public override void ReleaseControl()
		{
			base.ReleaseControl();
			foreach (GamePlayer player in CurrentRiders)
				player.DismountSteed(true);
		}

		/// <summary>
		/// Owner 2026-11-02: every deployed ram fights as if fully crewed. The virtual crew count - riders plus the
		/// friendly bots standing within 300 units - never filled: a night of sieges ran at 0-3 of 12 places, a third of
		/// the damage and twice the reload. The count is gone; the ram's full places are the number, and the marker
		/// SIEGE_RAM_CREW now reports the damage that number produces.
		/// </summary>
		public int Crew => MAX_PASSENGERS;

		/// <summary>Sanity distance for a companion crewing a player's ram; generous because a party of companions spreads out.</summary>
		private const int CompanionCrewRadius = 600;

		/// <summary>
		/// Owner 2026-11-01: the player's companions crew the ram, counted from the <b>group</b> - /spawn puts every
		/// companion in the player's group - and not from a radius query. A radius read inside the damage path came back
		/// with one crew member while two companions stood inside 300 units and passed every test, so the spatial list
		/// cannot be the authority here; with seven companions most of them also stand further out than 300. Distance
		/// stays only as sanity, so one left behind does not count as if it were at the door. Up to the ram's places.
		/// </summary>
		private int CompanionsAtRam(GamePlayer leader)
		{
			if (leader == null || leader.Group == null)
				return 0;
			int companions = 0;
			// Owner 2026-11-01: measured from the leader, who is standing on the ram, and not from the ram's own Realm
			// and region. Those two properties of a deployed siege weapon are the only comparisons the group probe did
			// not test, and they are the ones that can come back empty on a ram placed from a kit.
			foreach (GameLiving member in leader.Group.GetMembersInTheGroup())
				if (member is GameBot mate && mate.IsAlive && mate.Realm == leader.Realm &&
					!mate.IsAutonomousWorldBot && mate.CurrentRegionID == leader.CurrentRegionID &&
					leader.IsWithinRadius(mate, CompanionCrewRadius))
					companions++;
			return companions;
		}

		/// <summary>
		/// Diagnostics only: how many bots stand at the ram, how many pass each test, and what the group says. The radius
		/// and the group are printed side by side because they disagreed once; the difference decides which is right.
		/// The group tests are counted one at a time, so a crew of zero names the condition that emptied it.
		/// </summary>
		private readonly record struct CrewProbe(int Seen, int AliveSameRealm, int LeaderOf, int OwnedByOwner,
			int GroupBots, int GroupAlive, int GroupSameRealm, int GroupSameRegion, int GroupInRadius, int CrewCount);

		private CrewProbe ProbeCompanions()
		{
			int seen = 0, alive = 0, leaderOf = 0, owned = 0;
			int groupBots = 0, groupAlive = 0, groupSameRealm = 0, groupSameRegion = 0, groupInRadius = 0;
			GamePlayer leader = Owner as GamePlayer;
			if (leader != null)
			{
				foreach (GameNPC near in GetNPCsInRadius(300))
				{
					if (near is not GameBot subject)
						continue;
					seen++;
					if (!subject.IsAlive || subject.Realm != Realm)
						continue;
					alive++;
					if (CompanionEngagementMode.LeaderOf(subject) == leader)
						leaderOf++;
					if (ReferenceEquals(subject.Owner, leader))
						owned++;
				}

				if (leader.Group != null)
				{
					foreach (GameLiving member in leader.Group.GetMembersInTheGroup())
					{
						if (member is not GameBot subject || subject.IsAutonomousWorldBot)
							continue;
						groupBots++;
						if (!subject.IsAlive)
							continue;
						groupAlive++;
						if (subject.Realm != leader.Realm)
							continue;
						groupSameRealm++;
						if (subject.CurrentRegionID != leader.CurrentRegionID)
							continue;
						groupSameRegion++;
						if (!leader.IsWithinRadius(subject, CompanionCrewRadius))
							continue;
						groupInRadius++;
					}
				}
			}

			return new CrewProbe(seen, alive, leaderOf, owned, groupBots, groupAlive, groupSameRealm,
				groupSameRegion, groupInRadius, leader != null ? CompanionsAtRam(leader) : 0);
		}

		public void UpdateRamStatus()
		{
			//speed of reload/arming changed by number of riders
			ActionDelay[2] = GetReloadDelay;
		}

		private int GetReloadDelay
		{
			get => ReloadDelay(Level, Crew);
		}

		// Owner 2026-11-02: every ram is crewed to its full places (see Crew), so the numbers below are the ram's intended
		// damage and cadence. They are static so a test can pin them without a loaded world.
		private static int BaseFor(int level) => level switch
		{
			0 => 100,
			1 => 125,
			2 => 150,
			3 => 200,
			_ => 0,
		};

		/// <summary>Damage of one hit: the ram's base plus half of it for every crew place.</summary>
		public static int DamageFor(int level, int crew) => BaseFor(level) + (BaseFor(level) / 2 * crew);

		/// <summary>Reload in milliseconds: slower for a heavier ram, faster with every crew place filled.</summary>
		public static int ReloadDelay(int level, int crew) =>
			10000 + ((level + 1) * 2000) - (int)(10000 * ((double)crew / (double)MaxPassengersFor(level)));

		public override int BaseDamage => BaseFor(Level);

		public override int CalcDamageToTarget(GameLiving target) => DamageFor(Level, Crew);

		public override int MeleeAttackRange
		{
			get
			{
				switch (Level)
				{
					case 0: return 300;
					case 1: return 400;
					case 2:
					case 3: return 500;
					default: return 500;
				}
			}
		}

		public override short MaxSpeed
		{
			get
			{
				double speed = 10.0 + 5.0 * Level + 50.0 * CurrentRiders.Length / MAX_PASSENGERS;

				foreach (GamePlayer player in CurrentRiders)
				{
					RealmAbilities.RAPropertyEnhancer ab = player.GetAbility<RealmAbilities.AtlasOF_LifterAbility>();

					if (ab != null)
						speed *= 1 + ab.Amount / 100;
				}

				return (short) speed;
			}
		}
	}
}
