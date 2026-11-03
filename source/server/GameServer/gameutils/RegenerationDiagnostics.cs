using System.Collections.Generic;

namespace DOL.GS;

/// <summary>
/// Owner 2026-11-01: diagnostics only, no behaviour change. Foog (level 50) recovers about 6 power per tick with the
/// sorcerer's buff on him, while the calculator says 2.5 + 0.2 * 50 = 12.5, doubled by the world's
/// mana_regen_amount_modifier to 25. One line every ten seconds per living names every component of the value, so the
/// next tick in game says whether the modifier is applied, what the buff really adds, and which debuff takes it away.
/// </summary>
public static class RegenerationDiagnostics
{
    private const int IntervalMilliseconds = 10_000;
    private static readonly Dictionary<int, long> LastReport = new();
    private static readonly DOL.Logging.Logger Log = DOL.Logging.LoggerManager.Create(typeof(RegenerationDiagnostics));

    private static readonly Dictionary<int, (long Tick, int Endurance)> LastEndurance = new();

    /// <summary>
    /// Owner 2026-11-03: Foog sits out of combat and his endurance bar stays at zero, with a Paladin's endurance chant on
    /// him. One line every ten seconds per player names every component of the regeneration and the net change of the
    /// bar since the last line. If the lines stop while the bar is below its maximum, the regeneration timer is dead.
    /// </summary>
    public static void ReportEndurance(GamePlayer player, int regen, int baseTick)
    {
        long now = GameLoop.GameLoopTime;
        int delta = 0;
        lock (LastEndurance)
        {
            if (LastEndurance.TryGetValue(player.ObjectID, out var last) && now - last.Tick < IntervalMilliseconds)
                return;
            if (LastEndurance.TryGetValue(player.ObjectID, out last)) delta = player.Endurance - last.Endurance;
            LastEndurance[player.ObjectID] = (now, player.Endurance);
        }

        eProperty property = eProperty.EnduranceRegenerationAmount;
        try
        {
            Log.Info($"ENDURANCE_REGEN_TICK who=\"{player.Name}\" level={player.Level} regen={regen} " +
                     $"endurance={player.Endurance}/{player.MaxEndurance} change_since_last_line={delta} base_tick={baseTick} " +
                     $"buff={player.BaseBuffBonusCategory[property]} ability={player.AbilityBonus[property]} " +
                     $"item={player.ItemBonus[property]} spec_debuff={player.SpecBuffBonusCategory[property]} " +
                     $"modifier={ServerProperties.Properties.ENDURANCE_REGEN_AMOUNT_MODIFIER} sitting={player.IsSitting} " +
                     $"in_combat={player.InCombat} moving={player.IsMoving} sprinting={player.IsSprinting} " +
                     $"fatigue_consumption={player.GetModified(eProperty.FatigueConsumption)}");
        }
        catch (System.Exception)
        {
            // A tick is worth more than a log line.
        }
    }

    /// <summary>Called for every power tick of a player or a companion; autonomous population is left out.</summary>
    public static void ReportPower(GameLiving living, int amount)
    {
        if (living is GamePlayer)
        {
            // players are the case being reported
        }
        else if (living is GameBot bot && !bot.IsAutonomousWorldBot)
        {
            // companions count too: the owner asked about both
        }
        else
        {
            return;
        }

        long now = GameLoop.GameLoopTime;
        lock (LastReport)
        {
            if (LastReport.TryGetValue(living.ObjectID, out long last) && now - last < IntervalMilliseconds)
                return;
            LastReport[living.ObjectID] = now;
        }

        eProperty property = eProperty.PowerRegenerationAmount;
        // Owner 2026-11-01: diagnostics sit on the regeneration tick, so a logging failure must never stop a tick - the
        // test fixtures call this path with no logger initialised and it threw there.
        try
        {
            Log.Info($"POWER_REGEN_TICK who=\"{living.Name}\" kind={(living is GameBot ? "companion" : "player")} " +
                     $"level={living.Level} amount={amount} mana={living.Mana}/{living.MaxMana} " +
                     $"base={2.5 + living.Level * 0.2:0.00} buff={living.BaseBuffBonusCategory[property]} " +
                     $"ability={living.AbilityBonus[property]} item={living.ItemBonus[property]} " +
                     $"spec_debuff={living.SpecBuffBonusCategory[property]} " +
                     $"modifier={ServerProperties.Properties.MANA_REGEN_AMOUNT_MODIFIER} " +
                     $"halved_below_50={ServerProperties.Properties.MANA_REGEN_AMOUNT_HALVED_BELOW_50_PERCENT} " +
                     $"in_combat={living.InCombat}");
        }
        catch (System.Exception)
        {
            // A tick is worth more than a log line.
        }
    }
}
