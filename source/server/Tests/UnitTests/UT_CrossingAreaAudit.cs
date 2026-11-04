using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests;

/// <summary>
/// Tool (2026-11-04): before/after audit of the zone-step start ground taken from the nearest border crossing instead of the
/// nearest road anchor. For every listed crossing the start ground is computed both ways; where they differ the planner runs
/// from just inside the border toward typical goals with each, and the first steps are compared. Read only.
/// Env: OFFLINE_DAOC_NAV_ROOT (the runtime/server folder), OFFLINE_DAOC_OUT.
/// </summary>
[TestFixture, Explicit("Crossing area audit tool"), NonParallelizable]
public class UT_CrossingAreaAudit
{
    private static readonly Dictionary<int, Vector3[]> Goals = new()
    {
        [1] = [new(585085, 477504, 2600), new(528451, 358290, 8320)],
        [100] = [new(766235, 669173, 5736), new(704022, 738009, 5704)],
        [200] = [new(333229, 419539, 5336), new(421166, 484998, 1976)],
    };

    [Test]
    public void Audit()
    {
        string root = Environment.GetEnvironmentVariable("OFFLINE_DAOC_NAV_ROOT");
        string output = Environment.GetEnvironmentVariable("OFFLINE_DAOC_OUT");
        Assume.That(root != null && output != null);
        string previous = Environment.CurrentDirectory;
        NativeLibrary.SetDllImportResolver(typeof(LocalPathfindingMgr).Assembly,
            (name, asm, search) => name == "lib/Detour" ? NativeLibrary.Load(Path.Combine(root, "lib", "Detour.dll")) : IntPtr.Zero);
        Environment.CurrentDirectory = root;
        var lines = new List<string>();
        var nav = PathfindingProvider.LocalPathfindingMgr;
        var loaded = new List<Zone>();
        try
        {
            using var db = new SQLiteConnection($"Data Source={Path.GetFullPath("../data/opendaoc.sqlite3.db")};Read Only=True;Pooling=False;");
            db.Open();
            foreach (ushort regionId in new ushort[] { 1, 100, 200 })
            {
                var zones = new List<Zone>();
                using (var cmd = new SQLiteCommand($"select ZoneID,OffsetX,OffsetY,Width,Height from Zones where RegionID={regionId}", db))
                using (var reader = cmd.ExecuteReader())
                    while (reader.Read())
                        zones.Add(new Zone(null, (ushort)reader.GetInt32(0), $"zone{reader.GetInt32(0)}", reader.GetInt32(1) * 8192, reader.GetInt32(2) * 8192,
                            reader.GetInt32(3) * 8192, reader.GetInt32(4) * 8192, (ushort)reader.GetInt32(0), false, 0, false, 0, 0, 0, 0, 0));
                foreach (Zone zone in zones) { LocalPathfindingMgr.LoadNavMesh(zone); loaded.Add(zone); }
                Zone At(Vector3 p) => zones.FirstOrDefault(z => p.X >= z.XOffset && p.X < z.XOffset + z.Width && p.Y >= z.YOffset && p.Y < z.YOffset + z.Height);

                int samples = 0, differ = 0, same = 0, changed = 0, bothOkSame = 0, bothOkDifferent = 0, fixedFail = 0, newFail = 0, bothFail = 0;
                var examples = new List<string>();
                foreach (Zone from in zones)
                {
                    foreach (Zone to in zones)
                    {
                        if (!AutonomousZoneItinerary.TryGetSharedEdge(from, to, out var edge)) continue;
                        var crossings = AutonomousZoneSeams.For(regionId, from.ID, to.ID);
                        if (crossings == null) continue;
                        var all = new List<(Vector2, float, int)>();
                        foreach (var c in crossings)
                            all.Add((edge.Vertical ? new Vector2(edge.Border, c.Along) : new Vector2(c.Along, edge.Border), c.InsideZ, c.InsideArea));
                        for (int i = 0; i < crossings.Length; i += 4)
                        {
                            var c = crossings[i];
                            if (c.InsideArea < 0) continue;
                            Vector3 start = edge.Vertical ? new(edge.Border - edge.Direction * 64, c.Along, c.InsideZ) : new(c.Along, edge.Border - edge.Direction * 64, c.InsideZ);
                            samples++;
                            int? oldArea = AutonomousZoneRoads.AreaAt(from, start);
                            int? newArea = AutonomousZoneItinerary.NearestCrossingArea(all, start);
                            if (newArea == null || newArea == oldArea) { same++; continue; }
                            differ++;
                            foreach (Vector3 goal in Goals[regionId])
                            {
                                Zone goalZone = At(goal);
                                if (goalZone == null || goalZone == from) continue;
                                string Plan(bool legacy, out bool ok, out string stepText)
                                {
                                    AutonomousZoneItinerary.LegacyStartAreaForAudit = legacy;
                                    var trace = new List<string>();
                                    ok = AutonomousZoneItinerary.ProbeNextStep(zones, regionId, from, goalZone, start, goal, nav, out var step, trace);
                                    stepText = ok ? $"{(int)step.Inside.X},{(int)step.Inside.Y}>{(int)step.Outside.X},{(int)step.Outside.Y}" : "FAILED";
                                    return trace.FirstOrDefault(t => t.StartsWith("route areas")) ?? "";
                                }
                                string oldRoute = Plan(true, out bool oldOk, out string oldStep);
                                string newRoute = Plan(false, out bool newOk, out string newStep);
                                AutonomousZoneItinerary.LegacyStartAreaForAudit = false;
                                if (oldStep == newStep) { bothOkSame++; continue; }
                                changed++;
                                string kind = oldOk && newOk ? "both ok, different step" : !oldOk && newOk ? "NOW PLANS" : oldOk ? "NOW FAILS" : "both fail";
                                if (oldOk && newOk) bothOkDifferent++; else if (!oldOk && newOk) fixedFail++; else if (oldOk) newFail++; else bothFail++;
                                if (examples.Count < 60)
                                    examples.Add($"  [{kind}] {from.ID}>{to.ID} at {(int)start.X},{(int)start.Y},{(int)start.Z} goal {goalZone.ID}: area {oldArea}->{newArea}; old {oldRoute} {oldStep}; new {newRoute} {newStep}");
                            }
                        }
                    }
                }
                lines.Add($"region {regionId}: crossing samples {samples}, start ground same {same}, differs {differ}; plans compared with a different step: {changed} " +
                          $"(equal {bothOkSame}); both ok but different {bothOkDifferent}, now plans {fixedFail}, NOW FAILS {newFail}, both fail {bothFail}");
                lines.AddRange(examples);
                foreach (Zone zone in zones) { LocalPathfindingMgr.UnloadNavMesh(zone); loaded.Remove(zone); }
            }
        }
        finally
        {
            AutonomousZoneItinerary.LegacyStartAreaForAudit = false;
            foreach (Zone z in loaded) LocalPathfindingMgr.UnloadNavMesh(z);
            Environment.CurrentDirectory = previous;
        }
        File.WriteAllLines(output, lines);
        TestContext.Progress.WriteLine(string.Join("\n", lines.Where(l => l.StartsWith("region"))));
    }
}
