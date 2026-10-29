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

[Explicit("Read-only installed navmesh probe: where a lone Reaver stopped for 15 minutes on its way across zones (2026-10-29)"), NonParallelizable]
public class UT_ZoneStepStallProbe
{
    // Maraelalbel (Reaver L21): from Llyn Barfog toward the small skeletal legionnaire camp of Forest Sauvage, stopped at 542716,479106,2320.
    [TestCase((ushort)1, 542716f, 479106f, 2320f, 580765f, 437612f, 3673f)]
    // Linaelisbeth (Reaver L34): in region 1 at 573974,500305,2490, on her way to the drakoran grunt camp of Avalon Isle (region 51).
    [TestCase((ushort)1, 573974f, 500305f, 2490f, 472938f, 486300f, 2998f)]
    public void TheStopToTheNextZoneStep(ushort regionId, float x, float y, float z, float gx, float gy, float gz)
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
                .Invoke(null, new object[] { db, (int)regionId, loaded });

            Vector3 from = new(x, y, z), goal = new(gx, gy, gz);
            Zone fromZone = region.GetZone((int)from.X, (int)from.Y), goalZone = region.GetZone((int)goal.X, (int)goal.Y);
            TestContext.WriteLine($"from zone {fromZone?.ID} {fromZone?.Description} -> goal zone {goalZone?.ID} {goalZone?.Description}");
            var snap = nav.GetClosestPoint(fromZone, from, 48, 48, 4096, nav.DefaultFilters);
            TestContext.WriteLine($"stop {from}: closest={snap}");
            if (fromZone == goalZone)
            {
                TestContext.WriteLine("same zone: direct path");
            }
            else
            {
                bool step = AutonomousZoneItinerary.TryNextStep(region, fromZone, goalZone, from, goal, nav,
                    out AutonomousZoneBoundaryRouting.Step next, zone => true);
                TestContext.WriteLine($"zone step: ok={step} inside={next.Inside} outside={next.Outside}");
                if (step)
                {
                    Span<WrappedPathfindingNode> nodes = stackalloc WrappedPathfindingNode[512];
                    var path = nav.GetPathStraight(fromZone, from, next.Inside, nav.DefaultFilters, nodes);
                    Vector3 last = path.NodeCount > 0 ? nodes[Math.Min(path.NodeCount, nodes.Length) - 1].Position : from;
                    TestContext.WriteLine($"  stop -> step.Inside: status={path.Status} nodes={path.NodeCount} lastToGoal={Vector3.Distance(last, next.Inside):0} " +
                                          $"straight={Vector3.Distance(from, next.Inside):0}");
                }
            }
        }
        finally { foreach (var z2 in loaded) LocalPathfindingMgr.UnloadNavMesh(z2); Environment.CurrentDirectory = previous; }
    }
}
