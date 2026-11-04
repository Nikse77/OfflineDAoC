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

[Explicit("Read-only installed navmesh probe: four Hibernian bots stood at 384960,385504 with a destination 62,630 units away (2026-11-04)"), NonParallelizable]
public class UT_EmainFarDestinationNavigation
{
    [Test]
    public void TheStandingPointAndItsFarDestinationOnTheInstalledMesh()
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
                .Invoke(null, new object[] { db, 200, loaded });

            void Probe(string label, Vector3 from, Vector3 to)
            {
                Zone zf = region.GetZone((int)from.X, (int)from.Y), zt = region.GetZone((int)to.X, (int)to.Y);
                TestContext.WriteLine($"[{label}] from={from} zone={zf?.ID} to={to} zone={zt?.ID}");
                if (zf == null) return;
                var floor = nav.GetClosestPoint(zf, from, 72, 72, 4096, nav.DefaultFilters);
                var goalFloor = nav.GetClosestPoint(zf, to, 72, 72, 4096, nav.DefaultFilters);
                TestContext.WriteLine($"  floorFrom={(floor.HasValue ? floor.Value.ToString() : "NONE")} floorTo={(goalFloor.HasValue ? goalFloor.Value.ToString() : "NONE")}");
                if (!floor.HasValue) return;
                Span<WrappedPathfindingNode> nodes = stackalloc WrappedPathfindingNode[512];
                var path = nav.GetPathStraight(zf, floor.Value, to, nav.DefaultFilters, nodes);
                Vector3 last = path.NodeCount > 0 ? nodes[path.NodeCount - 1].Position : floor.Value;
                TestContext.WriteLine($"  nodes={path.NodeCount} last={last} remaining={Vector3.Distance(last, to):0}");
                for (int i = 0; i < path.NodeCount && i < 14; i++) TestContext.WriteLine($"    {i}: {nodes[i].Position}");
            }

            var stand = new Vector3(384960, 385504, 6699);
            var druim = new Vector3(333231, 420812, 5184);
            Probe("stand to Druim Ligen rendezvous", stand, druim);
            Probe("stand to its own target", stand, new(385148, 385483, 6706));
            Probe("stand to Dun nGed", stand, new(397098, 399736, 4376));
            Probe("Druim Ligen to stand", druim, stand);
            Probe("Dun nGed to Druim Ligen", new(397098, 399736, 4376), druim);
            Probe("target itself (zone 212 side)", new(385148, 385483, 6706), new(397098, 399736, 4376));
            Probe("zone 212 a bit further in", new(385400, 385400, 6700), new(397098, 399736, 4376));
            Probe("zone 212 from Dun nGed to target", new(397098, 399736, 4376), new(385148, 385483, 6706));
        }
        finally { foreach (var z in loaded) LocalPathfindingMgr.UnloadNavMesh(z); Environment.CurrentDirectory = previous; }
    }
}
