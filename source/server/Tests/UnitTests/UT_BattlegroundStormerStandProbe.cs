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

[Explicit("Read-only installed navmesh probe: Caer Caledon (region 250) spots where Albion stormers stood on 2026-10-29 with a path found and 0 nodes"), NonParallelizable]
public class UT_BattlegroundStormerStandProbe
{
    [Test]
    public void TheStandingSpotsToTheGateAndLord()
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

            (string Label, Vector3 Point)[] spots =
            {
                ("stand A (trace, gate step)", new(33795, 40471, 3267)),
                ("stand B (trace, lord step)", new(33599, 40261, 3349)),
                ("gate 250000301", new(32673, 39237, 3783)),
                ("lord", new(32982, 37439, 4890)),
                ("gate 250000302 (destroyed)", new(33192, 37634, 3914)),
                ("pocket 2026-10-28", new(30420, 35155, 3278)),
            };
            Span<WrappedPathfindingNode> nodes = stackalloc WrappedPathfindingNode[256];
            foreach (var (label, from) in spots)
            {
                Zone zone = region.GetZone((int)from.X, (int)from.Y);
                var tall = nav.GetClosestPoint(zone, from, 48, 48, 4096, nav.DefaultFilters);
                TestContext.WriteLine($"FROM {label} {from} zone={zone?.ID} closest={tall}");
                foreach (var (toLabel, to) in spots)
                {
                    if (toLabel == label) continue;
                    var path = nav.GetPathStraight(zone, from, to, nav.DefaultFilters, nodes);
                    Vector3 last = path.NodeCount > 0 ? nodes[path.NodeCount - 1].Position : from;
                    TestContext.WriteLine($"   -> {toLabel}: status={path.Status} nodes={path.NodeCount} lastToGoal={Vector3.Distance(last, to):0}");
                }
            }
        }
        finally { foreach (var z in loaded) LocalPathfindingMgr.UnloadNavMesh(z); Environment.CurrentDirectory = previous; }
    }
}
