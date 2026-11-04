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

[Explicit("Read-only installed navmesh probe: keep-travel stalls on the last leg to Dun Crauchon (2026-11-04)"), NonParallelizable]
public class UT_CrauchonApproachNavigation
{
    [Test]
    public void StalledLegsAreReachableOnTheInstalledMesh()
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

            var gate = new Vector3(427712, 318392, 4729);
            Probe("midgard-1", new(430599, 316707, 4153), gate);
            Probe("midgard-2", new(430990, 316831, 4247), new(427786, 318806, 4729));
            Probe("midgard-3", new(430431, 316664, 4133), new(427628, 317920, 4729));
            Probe("midgard-1 to keep", new(430599, 316707, 4153), new(425877, 318720, 4776));
            Probe("albion portal", new(474741, 342534, 4083), new(425201, 318737, 5945));
            Probe("albion portal to gate", new(474741, 342534, 4083), gate);
            Probe("hib hill", new(381392, 548800, 10007), new(381920, 548800, 10371));
        }
        finally { foreach (var z in loaded) LocalPathfindingMgr.UnloadNavMesh(z); Environment.CurrentDirectory = previous; }
    }
}
