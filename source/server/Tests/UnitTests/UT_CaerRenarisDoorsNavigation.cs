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

[Explicit("Read-only installed navmesh probe: the Caer Renaris doors 11181205 and 11181206 that find no door polygon (2026-11-04)"), NonParallelizable]
public class UT_CaerRenarisDoorsNavigation
{
    [Test]
    public void RenarisDoorsAndTheRoutesInto()
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
                .Invoke(null, new object[] { db, 1, loaded });
            Zone zone = region.GetZone(579877, 429821);
            var near = typeof(LocalPathfindingMgr).GetMethod("GetNearestPolys", BindingFlags.NonPublic | BindingFlags.Static);
            var mask = typeof(LocalPathfindingMgr).GetField("_doorMask", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);

            (string Name, Vector3 P)[] doors =
            {
                ("11181202", new(579536, 429736, 4834)), ("11181203", new(579433, 429913, 5042)), ("11181204", new(579406, 429974, 5026)),
                ("11181205 (fails)", new(579470, 429283, 4850)), ("11181206 (fails)", new(579599, 429274, 5026)), ("11181201", new(580425, 431126, 4706)),
            };
            foreach (var (name, p) in doors)
            {
                int polys = 0;
                for (int dx = -100; dx <= 100; dx += 50)
                    for (int dy = -100; dy <= 100; dy += 50)
                        polys += ((ulong[])near.Invoke(null, new object[] { zone, p + new Vector3(dx, dy, 0), mask, 12 })).Length;
                var floor = nav.GetClosestPoint(zone, p, 72, 72, 512, nav.DefaultFilters);
                TestContext.WriteLine($"door {name} at {p}: door-polygon hits within 100 units={polys}; floor={(floor.HasValue ? floor.Value.ToString() : "NONE")}");
            }

            void Route(string label, Vector3 from, Vector3 to)
            {
                var a = nav.GetClosestPoint(zone, from, 96, 96, 512, nav.DefaultFilters);
                var b = nav.GetClosestPoint(zone, to, 96, 96, 512, nav.DefaultFilters);
                if (!a.HasValue || !b.HasValue) { TestContext.WriteLine($"route {label}: floor missing ({a.HasValue}/{b.HasValue})"); return; }
                Span<WrappedPathfindingNode> nodes = stackalloc WrappedPathfindingNode[256];
                var path = nav.GetPathStraight(zone, a.Value, b.Value, nav.DefaultFilters, nodes);
                Vector3 last = path.NodeCount > 0 ? nodes[path.NodeCount - 1].Position : a.Value;
                float length = 0; for (int i = 1; i < path.NodeCount; i++) length += Vector3.Distance(nodes[i - 1].Position, nodes[i].Position);
                TestContext.WriteLine($"route {label}: nodes={path.NodeCount} remaining={Vector3.Distance(last, b.Value):0} length={length:0} last={last}");
            }
            var inside = new Vector3(579877, 429821, 4640);
            Route("south outside -> keep centre", new(579470, 428900, 4700), inside);
            Route("south door 11181205 -> keep centre", doors[3].P, inside);
            Route("south postern 11181206 -> keep centre", doors[4].P, inside);
            Route("north door 11181202 -> keep centre", doors[0].P, inside);
            Route("west door 11181203 -> keep centre", doors[1].P, inside);
            Route("east outside -> keep centre", new(580500, 429800, 4600), inside);
            Route("keep centre -> south outside", inside, new(579470, 428900, 4700));
        }
        finally { foreach (var z in loaded) LocalPathfindingMgr.UnloadNavMesh(z); Environment.CurrentDirectory = previous; }
    }
}
