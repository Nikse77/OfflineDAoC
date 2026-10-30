using System;
using System.Text;
using DOL.Events;
using DOL.GS;

namespace DOL.AI
{
    /// <summary>
    /// This class is the base of all artificial intelligence in game objects
    /// </summary>
    public abstract class ABrain : IServiceObject
    {
        public FSM FSM { get; set; }
        public ServiceObjectId ServiceObjectId { get; } = new(ServiceObjectType.Brain);
        public virtual GameNPC Body { get; set; }
        public virtual int ThinkInterval { get; set; } = 2500;
        public bool IsActive => Body != null && Body.IsAlive && Body.ObjectState is GameObject.eObjectState.Active &&
                                (Body is GameBot { IsAutonomousWorldBot: true } || AutonomousSummonActivity.IsVisibleOrAutonomousSummon(Body));
        public long NextThinkTick { get; set; }
        protected virtual int ThinkOffsetOnStart => Util.Random(750, 3000);

        /// <summary>
        /// Returns the string representation of the ABrain
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return new StringBuilder()
                .Append("body name='").Append(Body==null?"(null)":Body.Name)
                .Append("' (id=").Append(Body==null?"(null)":Body.ObjectID.ToString())
                .Append("), active=").Append(IsActive)
                .Append(", ThinkInterval=").Append(ThinkInterval)
                .ToString();
        }

        /// <summary>
        /// Starts the brain thinking
        /// </summary>
        /// <returns>true if started</returns>
        public virtual bool Start()
        {
            if (ServiceObjectStore.Add(this))
            {
                // Offset the first think tick by a random amount so that not too many are grouped in one server tick.
                // We also delay the first think tick a bit because clients tend to send positive LoS checks when they shouldn't.
                NextThinkTick = GameLoop.GameLoopTime + ThinkOffsetOnStart;
                return true;
            }

            return false;
        }

        /// <summary>Whether stopping a brain sends its body back to its spawn point. A dead gamebot is left where it fell.</summary>
        public static bool ShouldReturnToSpawn(bool intentionalWorldMove, bool deadGameBot, bool nearSpawn, bool active) =>
            !intentionalWorldMove && !deadGameBot && !nearSpawn && !active;

        /// <summary>
        /// Stops the brain thinking
        /// </summary>
        /// <returns>true if stopped</returns>
        public virtual bool Stop()
        {
            if (ServiceObjectId.IsPendingRemoval)
                return false; // Prevents overrides from doing any redundant work. Maybe counter intuitive.

            // Without `IsActive` check, charming a NPC that's returning to spawn would teleport it.
            // GameObject.MoveTo temporarily makes an autonomous GameBot inactive
            // while changing regions. Returning it to spawn from inside that
            // transfer creates a nested MoveTo and must not happen.
            // A dead gamebot stays where it died: its brain stops on death, and sending the corpse "home" moved it to its
            // spawn point within milliseconds, so nobody could raise it where it fell (found by
            // chris4aker/OfflineDAoC-LivingServer, 73348b1). The release moves it later.
            if (ShouldReturnToSpawn(Body is GameBot { IsIntentionalWorldMove: true }, Body is GameBot { IsAlive: false }, Body.IsNearSpawn, IsActive))
                Body.MoveTo(Body.CurrentRegionID, Body.SpawnPoint.X, Body.SpawnPoint.Y, Body.SpawnPoint.Z, Body.SpawnHeading);

            Body.ClearObjectsInRadiusCache();
            FSM?.SetCurrentState(eFSMStateType.WAKING_UP);
            return ServiceObjectStore.Remove(this);
        }

        /// <summary>
        /// Receives all messages of the body
        /// </summary>
        /// <param name="e">The event received</param>
        /// <param name="sender">The event sender</param>
        /// <param name="args">The event arguments</param>
        public virtual void Notify(DOLEvent e, object sender, EventArgs args) { }

        /// <summary>
        /// This method is called whenever the brain does some thinking
        /// </summary>
        public abstract void Think();

        public abstract void KillFSM();
    }
}
