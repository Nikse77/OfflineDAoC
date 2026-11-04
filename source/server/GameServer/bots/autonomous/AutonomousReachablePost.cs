using System;
using System.Numerics;

namespace DOL.GS;

/// <summary>
/// A post the navmesh can reach (owner 2026-11-04). A spot chosen on the ground (a muster slot, a ring around a keep) is
/// snapped to the nearest floor of the mesh, and the nearest floor may be a separate piece of mesh with no way in: four
/// Albion bots walked to the end of the road beside Castle Sauvage, 245 units short of slot 14, failed three recoveries,
/// were put back at the keep and walked there again, for ten minutes, and the muster never counted them. Five of the
/// 24 slots of that muster were unreachable; so were two of the Druim Ligen ones. A post is now kept only if a path from
/// the place the bots arrive at gets to it; otherwise the post is where the path ends.
/// </summary>
public static class AutonomousReachablePost
{
    /// <summary>Close enough to count as reaching the post (arrival at a post is judged at 160 units).</summary>
    public const float Tolerance = 120f;

    /// <summary>Pure rule: the wanted post when the path reaches it (or there is no answer), else the end of the path.</summary>
    public static Vector3 Decide(Vector3 wanted, Vector3? pathEnd) =>
        pathEnd is not { } end || Vector2.Distance(new(end.X, end.Y), new(wanted.X, wanted.Y)) <= Tolerance ? wanted : end;

    /// <summary>The post the path from <paramref name="from"/> can reach; the wanted one when the mesh gives no answer.</summary>
    public static Vector3 Resolve(IPathfindingMgr nav, Zone zone, Vector3 from, Vector3 wanted)
    {
        if (nav == null || zone == null || !nav.IsAvailable || !nav.HasNavmesh(zone)) return wanted;
        try
        {
            Vector3 start = nav.GetClosestPoint(zone, from, 72, 72, 4096, nav.DefaultFilters) ?? from;
            Span<WrappedPathfindingNode> nodes = stackalloc WrappedPathfindingNode[256];
            PathfindingResult result = nav.GetPathStraight(zone, start, wanted, nav.DefaultFilters, nodes);
            if (result.NodeCount <= 0 || result.NodeCount > nodes.Length) return wanted;
            return Decide(wanted, nodes[result.NodeCount - 1].Position);
        }
        catch
        {
            return wanted;
        }
    }
}
