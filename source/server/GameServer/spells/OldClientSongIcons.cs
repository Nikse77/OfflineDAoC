using System.Collections.Generic;

namespace DOL.GS
{
    /// <summary>
    /// Owner 2026-10-30: in the original client the speed, health regeneration and power regeneration songs of the
    /// Minstrel, Bard and Skald showed unrelated pictures (a fist, a brain, red bursts, an empty frame). The client reads
    /// the icon number it is sent as a row of its icons.csv, and these songs carry spells.csv row numbers instead
    /// (3608.. for speed, 1111.. for life, 5171.. for power). The icons.csv rows that draw the right pictures
    /// (wing 132, star 42, ring 228, with the rank border) are 320.., 1370.. and 1460..; they are sent for these songs
    /// only. The new client resolves the number through spells.csv, so it needs its own mapping for them.
    /// </summary>
    public static class OldClientSongIcons
    {
        private static readonly Dictionary<ushort, ushort> Speed = new()
        {
            [3608] = 320, [3609] = 321, [3610] = 322, [3611] = 323, [3612] = 324,
        };

        private static readonly Dictionary<ushort, ushort> HealthRegeneration = new()
        {
            [1111] = 1370, [1112] = 1370, [1113] = 1370, [1114] = 1371, [1115] = 1371, [1116] = 1372, [1117] = 1373, [1118] = 1374,
            [3613] = 1370, [3614] = 1370, [3615] = 1371, [3616] = 1372, [3617] = 1373, [3618] = 1374,
        };

        private static readonly Dictionary<ushort, ushort> PowerRegeneration = new()
        {
            [5171] = 1460, [5172] = 1461, [5173] = 1462, [5174] = 1463, [5175] = 1464,
        };

        /// <summary>The icon number to send for a spell of this type (the database's own number when it needs no change).</summary>
        public static ushort For(ushort icon, string spellType)
        {
            Dictionary<ushort, ushort> map = spellType switch
            {
                "SpeedEnhancement" => Speed,
                "HealthRegenBuff" => HealthRegeneration,
                "PowerRegenBuff" => PowerRegeneration,
                _ => null,
            };
            return map != null && map.TryGetValue(icon, out ushort fixedIcon) ? fixedIcon : icon;
        }
    }
}
