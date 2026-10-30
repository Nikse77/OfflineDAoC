# How a dead gamebot is shown to the clients

Written 2026-10-30, after a day of attempts. It records what the original client does with a dead
gamebot, what went wrong on the way, what was tried and failed, and what the server does now.
"Gamebot" here means every `GameBot`: companions (`/spawn`), bot helpers and the autonomous bots of
the world and of the battlegrounds.

## Result

A gamebot dies with the death animation and its body stays lying where it fell, for everybody who
sees it, as a dead monster does. The server keeps the bot, so a healer or a player's resurrection
spell still raises it where it fell, and the raised bot is drawn alive again.

Checked in game on 2026-10-30 (owner): in a battleground it looks right; a companion killed on
purpose, then raised at once by a cleric, falls with the animation, leaves one body on the ground
and comes back active beside the cleric. The body then disappears after a few seconds (see
"Limits").

## What the original server does

- A normal monster that dies is deleted at once (`GameNPC.ProcessDeath`: `Delete()` then
  `StartRespawn()`). Deleting it sends the clients a removal, and `SendObjectRemove` writes the
  removal type from the health (`npc.Health > 0 ? 1 : 0`): type 0 means "a dead body". The client
  answers a dead-body removal with the death animation, leaves the body lying for a few seconds and
  fades it. That body is client-side only: it cannot be targeted.
- A gamebot overrides `RetainCorpseForResurrection => true`, so it is **not** deleted: it stays in
  the world, with 0 health, to be raised. It therefore never gets the dead-body removal.
- The original server moves it away by accident: its brain stops on death and `ABrain.Stop` sends
  it to its spawn point with `MoveTo`. `MoveTo` removes the object (type 0, since the health is 0:
  the animation plays and a body lies where it fell) and puts it back at the spawn point, out of
  range. Nobody can raise it where it fell, and its resurrection is a long walk away.
- Players have a dead flag: `SendPlayerCreate` writes `IsAlive`, and the player position packet has
  `StateFlags.DEAD`. An NPC has nothing like it: `SendNPCCreate` carries no health or dead state, and
  the health byte of `SendObjectUpdate` does not change the pose.

`upstream/main` (shadowofze) and the Living Server fork (chris4aker) have this same code. The fork
fixed the corpse being moved (its `73348b1`, the same one-line change as ours) and nothing else:
there, and in our first build with it, a dead gamebot stands where it fell and can be targeted.

## The problems, in the order they appeared

1. **Companions were not raised.** The dead companion was sent to its spawn point within
   milliseconds, so the healer found nothing to raise. Fix (`f9cff53`, tested): `ABrain.Stop` does
   not send a dead `GameBot` home (`ABrain.ShouldReturnToSpawn`). The release moves it later.
   - Side effect: with the corpse in place, the `MoveTo` that played the death animation is gone.
     Every dead gamebot, companion or not, now **stood** where it fell and could be targeted. This is
     also what the owner saw in the battlegrounds and with enemies he killed.

2. **Attempts to make it lie down that did not work** (each one tried in game):
   - Create the object again (`6f5a875`): nothing changed. The client already knew the object, and
     a create carries no health.
   - A position update with health 0 (`ef11d86`): nothing changed. The log line added to check that
     the branch ran (`BOT_DEAD_BODY_UPDATE`) showed it did, and that `ABrain.Stop` ran for the same
     body every seven seconds. The health byte of an update is ignored for the pose.
   - A combat animation that leaves the health at 0 (`2f1bce7`, like a bleed killing a monster):
     nothing changed.
   - These three were removed or replaced. They should not be tried again.

3. **The removal as a dead body works** (`5ec14b5`): sending the viewers `SendObjectRemove` of the
   bot, with its health at 0, plays the death animation and leaves a body on the ground. But:
   - A body created again 2.5 seconds later (`5ec14b5`, then removed in `ecdd513`) was a second,
     standing and targetable figure beside the lying one.
   - Even without it, the figure came back: `ClientService.UpdateNpcs` sends an update to every NPC in
     range every `world_npc_update_interval` (5 seconds), and the client draws an object it receives
     an update for, standing. The original server never met this, because the corpse was at its spawn
     point, out of range.

4. **Dead gamebots are taken out of the world updates** (`d368888`), and then **late viewers** were
   missing the body: a player who comes into range after the death, or enters the zone, saw nothing.
   Fix (`155ad04`): `PlayerObjectCache.DeadBodiesShown` holds, per player, the dead gamebots that
   player was shown. Clients that know the bot at its death get the dead-body removal at once
   (`ClientService.ShowDeadBody`, from `GameBot.ProcessDeath`). A client that does not know it gets it
   created and removed as a dead body from `UpdateNpcs`. The set is emptied of anything no longer dead
   and in range, so a body that comes into range again is shown again.

5. **A second figure on companions** (`cdf47de`): other senders still reached the client after the
   removal. Companions move until the end, so a movement broadcast (`NpcMovementComponent`) followed
   the death; a name, flag or guild change recreates an object (`GameNPC` setters); targeting the body
   refreshes it (`PlayerTargetHandler`); and a client that receives an update for an object it no
   longer has asks for its creation (`CreateObjectRequestHandler`). `ClientService` now sends nothing
   for a dead gamebot at its public entry points (`IsDeadGamebot`): only the dead-body removal reaches
   the clients.

The raise and the release always create the bot again, because both revive it before moving it:
`ResurrectLiving` sets the health and then calls `MoveTo`, and `GameBot.MoveRevived` does the same for
the release. Do not change that order: a bot moved while dead would be drawn as a body at its new place.

## Limits (known, not defects of the server)

- The lying body is a client-side ghost. It is not targetable and the client fades it after a few
  seconds, as it does for a dead monster. After that a dead gamebot waiting to be raised is not seen at
  all, although the server still has it: bot healers raise it, a player healer cannot target it.
  A longer body would need the server to send the dead-body sequence again every few seconds, and each
  time the bot would show standing for an instant before it falls. Not done.
- A player who comes into range sees the body only for the same few seconds.
- Not checked: the new client (Godot) and the 1124 packet library path; companions in a group that
  leaves the zone while dead; a dead gamebot on a stable route.

## Other findings of the same day

- `SERVER_MEMORY` now lists private memory, threads, handles, native nav queries made, and the number
  of effects and brains held by the service (`377c83a`). Effects in the service grew from about 4,000
  to 130,000 in half an hour after only the first effect-leak fix (`40c5c3e`); the two fixes of the fork
  that follow (`7bcac65`, `b172944`) end effects whose NPC owner or pulse caster is deleted, and take
  an effect whose ending was lost out of the service. Check `effects` in a long run.
- The song icons of the original client: the effects bar uses the database icon number, the song bar
  the icons.csv row (`7bfbe21`, `Spell.EffectIcon`).

## Where to look

| What | Where |
|---|---|
| A dead gamebot stays where it fell | `ai/ABrain.cs` (`ShouldReturnToSpawn`, `Stop`) |
| The death shown to the clients | `bots/GameBot.cs` (`ProcessDeath`), `ECS-Services/ClientService.cs` (`ShowDeadBody`, `UpdateNpcs`, `IsDeadGamebot`) |
| Which bodies a player was shown | `gameobjects/GamePlayer.cs` (`PlayerObjectCache.DeadBodiesShown`) |
| Why the dead-body removal plays the animation | `packets/Server/PacketLib168.cs` (`SendObjectRemove`) |
