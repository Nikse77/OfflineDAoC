using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests;

[Explicit("Read-only installed navmesh probe: the Albion portal keep (Castle Sauvage) gate-front point 586460,476519 is a navigation pocket"), NonParallelizable]
public class UT_HomeAlbGateFrontNavigation
{
    [Test]
    public void GateFrontAndSurroundingPointsToTheFrontier()
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

            (string Label, Vector3 Goal)[] goals =
            {
                ("Caer Renaris", new(579877, 429821, 4640)),
                ("Caer Berkstead", new(584618, 390674, 5848)),
            };
            var cache = new Dictionary<(int, int), bool>();
            bool Connected(float x, float y)
            {
                var key = ((int)Math.Round(x / 100f), (int)Math.Round(y / 100f));
                if (cache.TryGetValue(key, out bool known)) return known;
                bool ok = false;
                Zone zone = region.GetZone((int)x, (int)y);
                var floor = zone == null ? null : nav.GetClosestPoint(zone, new(x, y, 2600), 72, 72, 4096, nav.DefaultFilters);
                if (floor.HasValue)
                {
                    Span<WrappedPathfindingNode> nodes = stackalloc WrappedPathfindingNode[512];
                    Vector3 goal = new(579877, 429821, 4640);
                    var path = nav.GetPathStraight(zone, floor.Value, goal, nav.DefaultFilters, nodes);
                    Vector3 last = path.NodeCount > 0 ? nodes[path.NodeCount - 1].Position : floor.Value;
                    ok = path.NodeCount >= 100 && Vector3.Distance(last, goal) < 15000;
                }
                return cache[key] = ok;
            }
            var results = new List<(double Score, string Text)>();
            for (int sx = -2100; sx <= 2450; sx += 350)
                for (int sy = -2100; sy <= 2100; sy += 350)
                    for (int a = 0; a < 360; a += 30)
                    {
                        float spotX = 584285 + sx, spotY = 477200 + sy;
                        if (Math.Sqrt(sx * (double)sx + sy * (double)sy) > 2600 || a is > 30 and < 300 || !Connected(spotX, spotY)) continue;
                        double dx = Math.Cos(a * Math.PI / 180), dy = Math.Sin(a * Math.PI / 180);
                        int ok = 0, total = 0;
                        foreach (int forward in new[] { 30, 200, 400, 650 })
                            foreach (int lateral in new[] { -400, -200, 0, 200, 400 })
                            {
                                total++;
                                if (Connected(spotX + (float)(dx * forward - dy * lateral), spotY + (float)(dy * forward + dx * lateral))) ok++;
                            }
                        results.Add(((double)ok / total * 100000 + (sx * 0.9543 - sy * 0.2990 * -1 * -1), $"spot={spotX:0},{spotY:0} dir=({dx:0.0000},{dy:0.0000}) angle={a} fanOk={ok}/{total} distFromPad={Math.Sqrt(sx * (double)sx + sy * (double)sy):0}"));
                    }
            foreach (var (cx, cy) in new[] { (586035f, 475450f), (585685f, 475450f), (585685f, 477200f) })
                foreach (var (ddx, ddy) in new[] { (0.9543, -0.2990), (0.8660, -0.5000), (0.7071, -0.7071) })
                {
                    int okc = 0, totalc = 0;
                    foreach (int forward in new[] { 30, 100, 200, 300, 400, 500, 650 })
                        foreach (int lateral in new[] { -400, -300, -200, -100, 0, 100, 200, 300, 400 })
                        {
                            totalc++;
                            if (Connected(cx + (float)(ddx * forward - ddy * lateral), cy + (float)(ddy * forward + ddx * lateral))) okc++;
                        }
                    TestContext.WriteLine($"BEST CHECK spot={cx:0},{cy:0} dir=({ddx:0.0000},{ddy:0.0000}) fanOk={okc}/{totalc}");
                }
            foreach (var r in results.OrderByDescending(r => r.Score).ThenBy(r => r.Text).Take(14)) TestContext.WriteLine("BEST " + r.Text);
        }
        finally { foreach (var z in loaded) LocalPathfindingMgr.UnloadNavMesh(z); Environment.CurrentDirectory = previous; }
    }
}
