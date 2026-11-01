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
			if (crew != _loggedCrew)
			{
				_loggedCrew = crew;
				(int seen, int aliveSameRealm, int leaderOf, int ownedBy) = ProbeCompanions();
				Log.Warn($"SIEGE_RAM_CREW level={Level} name=\"{Name}\" riders={CurrentRiders.Length} crew={crew} " +
				         $"damage={damageAmount} reload_ms={GetReloadDelay} " +
				         $"owner=\"{((Owner as GamePlayer)?.Name ?? "null")}\" bots_at_ram={seen} " +
				         $"alive_same_realm={aliveSameRealm} leader_of={leaderOf} owned_by_owner={ownedBy}");
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
		/// Riders, plus - for a bot-operated ram - the operator's group members standing at the ram (bots cannot mount;
		/// owner 2026-10-07: players crewed rams and bot crews must break doors in a sensible time). Up to the ram's places.
		/// </summary>
		public int Crew
		{
			get
			{
				int crew = CurrentRiders.Length;
				// A gamebot ram is crewed by any friendly gamebot at it, not only the operator's own group (run 2026-10-07:
				// Midgard's ram at Caer Erasleigh ran on one bot while its army fought beside it).
				if (Owner is GameBot op && (op.Group != null || op.IsAutonomousWorldBot))
					foreach (GameNPC npc in GetNPCsInRadius(300))
						if (npc is GameBot member && member != op && member.IsAlive && member.Realm == Realm &&
							(op.Group?.IsInTheGroup(member) == true || op.IsAutonomousWorldBot && member.IsAutonomousWorldBot))
							crew++;
				// Owner 2026-11-01: a player's companions crew the ram by standing at it, the same way a bot ram is
				// crewed by the bots around it. They cannot be riders - only player characters can mount a siege
				// weapon - but while the player is on the ram they stay at it and count, and when the player steps
				// off there is no operator left, so the ram stops firing and they follow again.
				else if (Owner is GamePlayer leader)
					foreach (GameNPC near in GetNPCsInRadius(300))
						if (near is GameBot mate && mate.IsAlive && mate.Realm == Realm &&
							CompanionEngagementMode.LeaderOf(mate) == leader)
							crew++;
				return CrewTotal(Level, crew);
			}
		}

		/// <summary>
		/// Diagnostics only: how many bots stand at the ram, and how many pass each test a companion has to pass to be
		/// counted. It reads what Crew reads and changes nothing - the fix is chosen from these four numbers.
		/// </summary>
		private (int Seen, int AliveSameRealm, int LeaderOf, int OwnedByOwner) ProbeCompanions()
		{
			if (Owner is not GamePlayer leader)
				return (0, 0, 0, 0);
			int seen = 0, alive = 0, leaderOf = 0, owned = 0;
			foreach (GameNPC near in GetNPCsInRadius(300))
			{
				if (near is not GameBot mate)
					continue;
				seen++;
				if (!mate.IsAlive || mate.Realm != Realm)
					continue;
				alive++;
				if (CompanionEngagementMode.LeaderOf(mate) == leader)
					leaderOf++;
				if (ReferenceEquals(mate.Owner, leader))
					owned++;
			}
			return (seen, alive, leaderOf, owned);
		}

		public void UpdateRamStatus()
		{
			//speed of reload/arming changed by number of riders
			ActionDelay[2] = GetReloadDelay;
		}

		private int GetReloadDelay
		{
			get
			{
				//custom formula
				return 10000 + ((Level + 1) * 2000) - (int)(10000 * ((double)Crew / (double)MAX_PASSENGERS));
			}
		}

		public override int CalcDamageToTarget(GameLiving target)
		{
			//return BaseDamage + (int)(((double)BaseDamage / 2.0) * (double)((double)CurrentRiders.Length / (double)MAX_PASSENGERS));
			return BaseDamage + (BaseDamage/2 * Crew);
		}

		public override int BaseDamage
		{
			get
			{
				int damageAmount = 0;
				switch (Level)
				{
					case 0:
						//damageAmount = 200;
						damageAmount = 100;
						break;
					case 1:
						//damageAmount = 300;
						damageAmount = 125;
						break;
					case 2:
						//damageAmount = 450;
						damageAmount = 150;
						break;
					case 3:
						//damageAmount = 750;
						damageAmount = 200;
						break;
				}
				return damageAmount;
			}
		}

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
