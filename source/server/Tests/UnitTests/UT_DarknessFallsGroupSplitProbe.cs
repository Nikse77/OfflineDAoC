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

[Explicit("Read-only installed navmesh probe: the Darkness Falls spot where four Albion bots of group albion-4272d7-005 stood 4,300 from their leader on 2026-10-28"), NonParallelizable]
public class UT_DarknessFallsGroupSplitProbe
{
    [Test]
    public void TheWaysBetweenTheRouteEndTheLeaderAndTheCamp()
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

            (string Label, Vector3 Point)[] spots =
            {
                ("route end (four followers stand here)", new(32472, 23191, 18835)),
                ("followers' formation spot", new(32659, 23417, 18826)),
                ("leader Elaelalora at 23:08", new(29018, 27580, 20359)),
                ("leader earlier at 23:04", new(33540, 27951, 22841)),
                ("camp: soultorn albion myrmidon", new(34479, 24049, 21758)),
            };
            Span<WrappedPathfindingNode> nodes = stackalloc WrappedPathfindingNode[512];
            foreach (var (label, from) in spots)
            {
                Zone zone = region.GetZone((int)from.X, (int)from.Y);
                var snap = nav.GetClosestPoint(zone, from, 48, 48, 4096, nav.DefaultFilters);
                TestContext.WriteLine($"FROM {label} {from} zone={zone?.ID} {zone?.Description} closest={snap}");
                foreach (var (toLabel, to) in spots)
                {
                    if (toLabel == label) continue;
                    var path = nav.GetPathStraight(zone, from, to, nav.DefaultFilters, nodes);
                    Vector3 last = path.NodeCount > 0 ? nodes[path.NodeCount - 1].Position : from;
                    TestContext.WriteLine($"   -> {toLabel}: status={path.Status} nodes={path.NodeCount} last={last:0} lastToGoal={Vector3.Distance(last, to):0}");
                }
            }
        }
        finally { foreach (var z in loaded) LocalPathfindingMgr.UnloadNavMesh(z); Environment.CurrentDirectory = previous; }
    }
}
