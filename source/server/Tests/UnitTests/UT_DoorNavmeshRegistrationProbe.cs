using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests;

[Explicit("Read-only installed navmesh probe: how far is the nearest door polygon from each door that failed to register at start-up"), NonParallelizable]
public class UT_DoorNavmeshRegistrationProbe
{
    [Test]
    public void FailedDoorsAgainstTheNearestDoorPolygon()
    {
        string root = Environment.GetEnvironmentVariable("OFFLINE_DAOC_NAV_ROOT");
        Assert.That(root, Is.Not.Null.And.Not.Empty, "Set OFFLINE_DAOC_NAV_ROOT to an install's runtime/server folder");
        string previous = Environment.CurrentDirectory;
        var loaded = new List<Zone>();
        try
        {
            string logText;
            using (var stream = new FileStream(Path.Combine(root, "logs", "server.log"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream))
                logText = reader.ReadToEnd();
            var doors = new Regex(@"Failed to register door.*?\(Id: (\d+)\) \(Name: ([^)]*)\) \(Loc: (-?\d+),(-?\d+),(-?\d+)\) \(ZoneId: (\d+)\) \(RegionId: (\d+)\)")
                .Matches(logText).Select(m => (Id: m.Groups[1].Value, Name: m.Groups[2].Value,
                    P: new Vector3(int.Parse(m.Groups[3].Value), int.Parse(m.Groups[4].Value), int.Parse(m.Groups[5].Value)),
                    Zone: int.Parse(m.Groups[6].Value), Region: int.Parse(m.Groups[7].Value)))
                .GroupBy(d => d.Id).Select(g => g.First()).ToList();
            TestContext.WriteLine($"{doors.Count} distinct failed doors in the log");

            NativeLibrary.SetDllImportResolver(typeof(LocalPathfindingMgr).Assembly,
                (name, asm, search) => name == "lib/Detour" ? NativeLibrary.Load(Path.Combine(root, "lib", "Detour.dll")) : IntPtr.Zero);
            Environment.CurrentDirectory = root;
            using var db = new SQLiteConnection($"Data Source={Path.GetFullPath("../data/opendaoc.sqlite3.db")};Read Only=True;Pooling=False;");
            db.Open();
            var build = typeof(UT_AuditedDungeonInstalledMesh).GetMethod("BuildRegion", BindingFlags.NonPublic | BindingFlags.Static);
            var near = typeof(LocalPathfindingMgr).GetMethod("GetNearestPolys", BindingFlags.NonPublic | BindingFlags.Static);
            var mask = typeof(LocalPathfindingMgr).GetField("_doorMask", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            var nav = PathfindingProvider.LocalPathfindingMgr;

            foreach (var regionGroup in doors.GroupBy(d => d.Region).OrderBy(g => g.Key))
            {
                Region region = (Region)build.Invoke(null, new object[] { db, regionGroup.Key, loaded });
                foreach (var d in regionGroup.OrderBy(d => d.Zone).ThenBy(d => d.Name))
                {
                    Zone zone = region.GetZone((int)d.P.X, (int)d.P.Y);
                    if (zone == null) { TestContext.WriteLine($"R{d.Region} Z{d.Zone} {d.Name} id={d.Id} at {d.P}: no zone"); continue; }
                    // Search for door polygons on growing rings around the door (the registration itself looks only 64 units around).
                    string best = "none within 1200";
                    for (int ring = 0; ring <= 1200; ring += 50)
                    {
                        float bestDist = float.MaxValue; Vector3 bestOff = default; int count = 0;
                        for (int dx = -ring; dx <= ring; dx += 50)
                            for (int dy = -ring; dy <= ring; dy += 50)
                            {
                                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != ring) continue;
                                var polys = (ulong[])near.Invoke(null, new object[] { zone, d.P + new Vector3(dx, dy, 0), mask, 12 });
                                if (polys.Length == 0) continue;
                                count += polys.Length;
                                float dist = MathF.Sqrt(dx * dx + dy * dy);
                                if (dist < bestDist) { bestDist = dist; bestOff = new(dx, dy, 0); }
                            }
                        if (count > 0) { best = $"nearest door polygon about {bestDist:0} units away (offset {bestOff.X:0},{bestOff.Y:0})"; break; }
                    }
                    var floor = nav.GetClosestPoint(zone, d.P, 72, 72, 512, nav.DefaultFilters);
                    TestContext.WriteLine($"R{d.Region} Z{zone.ID} {d.Name} id={d.Id} at {d.P}: {best}; walkable floor {(floor.HasValue ? $"{Vector3.Distance(floor.Value, d.P):0} units" : "NONE within 512")}");
                }
            }
        }
        finally { foreach (var z in loaded) LocalPathfindingMgr.UnloadNavMesh(z); Environment.CurrentDirectory = previous; }
    }
}
