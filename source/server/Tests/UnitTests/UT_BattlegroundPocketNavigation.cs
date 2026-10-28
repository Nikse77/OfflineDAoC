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

[Explicit("Read-only installed navmesh probe: Caer Caledon (region 250) pocket at 30420,35155 where 14 Albion stormers stood on 2026-10-28"), NonParallelizable]
public class UT_BattlegroundPocketNavigation
{
    [Test]
    public void CaerCaledonPocketToGatesAndLord()
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
                .Invoke(null, new object[] { db, 250, loaded });

            Vector3 pocket = new(30420, 35155, 3278);
            Vector3 pocketB = new(30292, 35003, 3289);
            Vector3 escaped = new(32222, 38715, 3725);
            (string Label, Vector3 Goal)[] goals =
            {
                ("gate 250000302 (destroyed)", new(33192, 37634, 3914)),
                ("gate 250000301 (alive)", new(32673, 39237, 3783)),
                ("lord", new(32982, 37439, 4890)),
                ("where Fergeaean got out", escaped),
            };
            foreach (var (startLabel, start) in new[] { ("pocket", pocket), ("pocketB", pocketB), ("escaped", escaped) })
            {
                Zone zone = region.GetZone((int)start.X, (int)start.Y);
                var small = nav.GetClosestPoint(zone, start, 48, 48, 96, nav.DefaultFilters);
                var tall = nav.GetClosestPoint(zone, start, 48, 48, 4096, nav.DefaultFilters);
                TestContext.WriteLine($"START {startLabel} {start} zone={zone?.ID} small={small} tall={tall}");
                foreach (var (label, goal) in goals)
                {
                    if (startLabel == "escaped" && label.StartsWith("where")) continue;
                    Span<WrappedPathfindingNode> nodes = stackalloc WrappedPathfindingNode[256];
                    var path = nav.GetPathStraight(zone, start, goal, nav.DefaultFilters, nodes);
                    Vector3 last = path.NodeCount > 0 ? nodes[path.NodeCount - 1].Position : start;
                    TestContext.WriteLine($"  {startLabel} -> {label}: status={path.Status} nodes={path.NodeCount} lastNode={last} lastToGoal={Vector3.Distance(last, goal):0}");
                }
            }
        }
        finally { foreach (var z in loaded) LocalPathfindingMgr.UnloadNavMesh(z); Environment.CurrentDirectory = previous; }
    }
}
