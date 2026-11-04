using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests;

[Explicit("Read-only installed navmesh probe: line of sight between Darkness Falls monsters for Bring A Friend (2026-11-04)"), NonParallelizable]
public class UT_DarknessFallsBafSight
{
    [Test]
    public void MonstersOfOneCampSeeEachOtherOnTheInstalledMesh()
    {
        string root = Environment.GetEnvironmentVariable("OFFLINE_DAOC_NAV_ROOT");
        Assert.That(root, Is.Not.Null.And.Not.Empty, "Set OFFLINE_DAOC_NAV_ROOT to an install's runtime/server folder");
        string previous = Environment.CurrentDirectory;
        var loaded = new List<Zone>();
        try
        {
            NativeLibrary.SetDllImportResolver(typeof(LocalPathfindingMgr).Assembly,
                (name, asm, search) => name == "lib/Detour" ? NativeLibrary.Load(Path.Combine(root, "lib", "Detour.dll")) : IntPtr.Zero);
            Environment.CurrentDirectory = root;
            using var db = new SQLiteConnection($"Data Source={Path.GetFullPath("../data/opendaoc.sqlite3.db")};Read Only=True;Pooling=False;");
            db.Open();
            var nav = PathfindingProvider.LocalPathfindingMgr;
            Region region = (Region)typeof(UT_AuditedDungeonInstalledMesh)
                .GetMethod("BuildRegion", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { db, 249, loaded });

            void Sight(string label, Vector3 from, Vector3 to)
            {
                Zone zone = region.GetZone((int)from.X, (int)from.Y);
                if (zone == null) { TestContext.WriteLine($"[{label}] no zone"); return; }
                var floorFrom = nav.GetClosestPoint(zone, from, 72, 72, 4096, nav.DefaultFilters);
                var floorTo = nav.GetClosestPoint(zone, to, 72, 72, 4096, nav.DefaultFilters);
                bool los = nav.HasLineOfSight(zone, from, to, nav.DefaultFilters);
                TestContext.WriteLine($"[{label}] los={los} dist2d={Vector2.Distance(new(from.X, from.Y), new(to.X, to.Y)):0} dz={to.Z - from.Z:0} " +
                    $"floorFrom={(floorFrom.HasValue ? $"{floorFrom.Value.Z:0}" : "NONE")} floorTo={(floorTo.HasValue ? $"{floorTo.Value.Z:0}" : "NONE")}");
            }

            // Deamhaness (level 32) around (29918,17926,18285) and the rocot cluster (level 36-38), from the Mob table.
            var d0 = new Vector3(29918, 17926, 18285);
            Sight("deamhaness > deamhaness 593", d0, new(30157, 17566, 18285));
            Sight("deamhaness > deamhaness 568", d0, new(29409, 18110, 18355));
            Sight("deamhaness > deamhaness 652", d0, new(30607, 17963, 18337));
            Sight("deamhaness > deamhaness 806", d0, new(29684, 17381, 18339));
            Sight("deamhaness > rocot", d0, new(30400, 18012, 18319));
            var r0 = new Vector3(30447, 18164, 18318);
            Sight("rocot > rocot a", r0, new(30400, 18012, 18319));
            Sight("rocot > rocot b", r0, new(30474, 18382, 18308));
            Sight("rocot > rocot c", r0, new(29399, 18271, 18303));
            Sight("rocot > rocot d", r0, new(29440, 18432, 18316));
            Sight("rocot > rocot e (+174 high)", r0, new(30849, 17893, 18459));
            Sight("same mob to itself 2 apart", r0, new(30449, 18166, 18318));
        }
        finally { foreach (var z in loaded) LocalPathfindingMgr.UnloadNavMesh(z); Environment.CurrentDirectory = previous; }
    }
}
