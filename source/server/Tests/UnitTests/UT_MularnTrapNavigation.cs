using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.SQLite;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests;

[Explicit("Read-only installed navmesh probe: the Vale of Mularn spot (region 100) where five Midgard battleground-tour bots stood still on 2026-10-28"), NonParallelizable]
public class UT_MularnTrapNavigation
{
    [Test]
    public void TheZoneStepsFromTheSpotToThePorterWalkedLegByLeg()
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
                .Invoke(null, new object[] { db, 100, loaded });

            Vector3 goal = new(725204, 759950, 4531);
            Zone goalZone = region.GetZone((int)goal.X, (int)goal.Y);
            TestContext.WriteLine($"GOAL {goal} zone={goalZone?.ID} {goalZone?.Description}");
            foreach (var (label, start) in new[] { ("A", new Vector3(704288, 737354, 5706)), ("B", new Vector3(704544, 737216, 5706)) })
            {
                Vector3 cursor = start;
                Zone cursorZone = region.GetZone((int)cursor.X, (int)cursor.Y);
                var seen = new List<string>();
                for (int leg = 0; leg < 10; leg++)
                {
                    TestContext.WriteLine($"{label} leg {leg}: at {cursor:0} zone {cursorZone.ID} {cursorZone.Description}");
                    if (cursorZone == goalZone) { TestContext.WriteLine($"{label}: reached the goal zone"); break; }
                    bool ok = AutonomousZoneItinerary.TryNextStep(region, cursorZone, goalZone, cursor, goal, nav, out var step,
                        zone => AutonomousRealmBoundary.Allows(eRealm.Midgard, 100, zone.ID));
                    if (!ok) { TestContext.WriteLine($"{label}: NO STEP from zone {cursorZone.ID}"); break; }
                    TestContext.WriteLine($"{label}   step inside {step.Inside:0} -> outside {step.Outside:0}");
                    string key = $"{(int)step.Inside.X},{(int)step.Inside.Y}>{(int)step.Outside.X},{(int)step.Outside.Y}";
                    if (seen.Contains(key)) { TestContext.WriteLine($"{label}: the same step again - a cycle"); break; }
                    seen.Add(key);
                    cursor = step.Outside;
                    cursorZone = region.GetZone((int)cursor.X, (int)cursor.Y);
                }
            }
            // the same planning with its trace, from the spot, for the first leg
            var trace = new List<string>();
            Zone zoneA = region.GetZone(704288, 737354);
            AutonomousZoneItinerary.ProbeNextStep(region.Zones, 100, zoneA, goalZone, new Vector3(704288, 737354, 5706), goal,
                nav, out _, trace, zone => AutonomousRealmBoundary.Allows(eRealm.Midgard, 100, zone.ID));
            foreach (string line in trace.Take(24)) TestContext.WriteLine("TRACE " + line);
        }
        finally { foreach (var z in loaded) LocalPathfindingMgr.UnloadNavMesh(z); Environment.CurrentDirectory = previous; }
    }

    [Test]
    public void TheSpotNearTheStormerPorterAndItsExits()
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
                .Invoke(null, new object[] { db, 100, loaded });

            (string Label, Vector3 Point)[] spots =
            {
                ("spot A (Dageildhild, Arngearvald)", new(704288, 737354, 5706)),
                ("spot A north node", new(704288, 737216, 5706)),
                ("spot B (Egileildgrim)", new(704544, 737216, 5706)),
                ("spot B south node", new(704544, 737344, 5706)),
            };
            (string Label, Vector3 Point)[] goals =
            {
                ("porter stand-off (Stor Gothi Annark)", new(725204, 759950, 4531)),
                ("a point 3000 north of the spot", new(704288, 740354, 5706)),
                ("a point 3000 east of the spot", new(707288, 737354, 5706)),
                ("a point 3000 south of the spot", new(704288, 734354, 5706)),
                ("a point 3000 west of the spot", new(701288, 737354, 5706)),
            };
            Span<WrappedPathfindingNode> nodes = stackalloc WrappedPathfindingNode[256];
            foreach (var (label, start) in spots)
            {
                Zone zone = region.GetZone((int)start.X, (int)start.Y);
                var small = nav.GetClosestPoint(zone, start, 48, 48, 96, nav.DefaultFilters);
                var tall = nav.GetClosestPoint(zone, start, 48, 48, 4096, nav.DefaultFilters);
                TestContext.WriteLine($"START {label} {start} zone={zone?.ID} {zone?.Description} small={small} tall={tall}");
                foreach (var (goalLabel, goal) in goals)
                {
                    var path = nav.GetPathStraight(zone, start, goal, nav.DefaultFilters, nodes);
                    Vector3 last = path.NodeCount > 0 ? nodes[path.NodeCount - 1].Position : start;
                    TestContext.WriteLine($"  -> {goalLabel}: status={path.Status} nodes={path.NodeCount} lastNode={last} lastToGoal={Vector3.Distance(last, goal):0}");
                }
            }
        }
        finally { foreach (var z in loaded) LocalPathfindingMgr.UnloadNavMesh(z); Environment.CurrentDirectory = previous; }
    }
}
