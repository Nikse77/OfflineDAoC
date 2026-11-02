using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace DOL.GS;

/// <summary>
/// What side of a zone's rectangle a waypoint may not come too close to.
/// </summary>
[Flags]
public enum eMapSide
{
    None = 0,
    West = 1,
    East = 2,
    North = 4,
    South = 8,
}

/// <summary>
/// Owner 2026-11-02: an RvR party's route keeps at least <see cref="Margin"/> units from the outer edge of the map.
/// The navmesh does not know where the world ends, so Detour keeps answering with the shortest polyline, which can hug
/// the last walkable strip along the border; <see cref="TryKeepInside"/> moves such a waypoint back inside and lets the
/// mesh re-project it, and a waypoint that cannot be re-projected close by is left exactly where it was.
///
/// Only a side with no neighbouring zone counts as the map edge. An inner border is precisely where a zone crossing
/// happens (the inside/outside seam the party walks through), so a margin there would break every seam in the world:
/// that is the reason this lives in the path result and not in the zone router.
/// </summary>
public static class AutonomousMapEdgeMargin
{
    /// <summary>Owner 2026-11-02: the distance from the map border a party route is kept at.</summary>
    public const float Margin = 600f;

    /// <summary>How far the mesh may move the clamped point before the original waypoint is kept instead.</summary>
    private const float ReprojectTolerance = 600f;

    /// <summary>A zone narrower than this cannot hold the margin on both axes; leave it alone rather than squeeze it.</summary>
    private const float MinimumSpan = 2 * Margin + 256;

    private static readonly ConditionalWeakTable<Zone, ZoneSides> OuterSides = new();

    private sealed class ZoneSides
    {
        public eMapSide Sides;
    }

    /// <summary>
    /// Keeps <paramref name="point"/> at least <see cref="Margin"/> inside every outer side of <paramref name="zone"/>.
    /// Returns false, with the point untouched, when there is nothing to do or when the clamped point has no mesh
    /// within <see cref="ReprojectTolerance"/> units (a border ledge): a party left on the strip is better than one
    /// sent at a wall it cannot walk to.
    /// </summary>
    public static bool TryKeepInside(Zone zone, IPathfindingMgr nav, Vector3 point, out Vector3 kept)
    {
        kept = point;
        if (zone?.ZoneRegion == null || nav == null)
            return false;

        eMapSide outer = OuterSidesFor(zone);
        if (outer == eMapSide.None)
            return false;

        Vector3 clamped = point;
        if (!TryClamp(zone.XOffset, zone.YOffset, zone.XOffset + zone.Width, zone.YOffset + zone.Height, outer, ref clamped))
            return false;

        Vector3? onMesh = nav.GetClosestPoint(zone, clamped, 96, 96, 4096, nav.DefaultFilters);
        if (onMesh is not { } resolved || Vector3.DistanceSquared(resolved, clamped) > ReprojectTolerance * ReprojectTolerance)
            return false;

        kept = resolved;
        return true;
    }

    /// <summary>
    /// The arithmetic half, kept free of the world so a fixture can pin it: clamp a point to <see cref="Margin"/> inside
    /// the given rectangle on the sides flagged as the map edge.
    /// </summary>
    public static bool TryClamp(float x0, float y0, float x1, float y1, eMapSide outer, ref Vector3 point)
    {
        if (x1 <= x0 || y1 <= y0)
            return false;

        float x = point.X;
        float y = point.Y;
        if (x1 - x0 > MinimumSpan)
        {
            if ((outer & eMapSide.West) != 0 && x < x0 + Margin)
                x = x0 + Margin;
            if ((outer & eMapSide.East) != 0 && x > x1 - Margin)
                x = x1 - Margin;
        }

        if (y1 - y0 > MinimumSpan)
        {
            if ((outer & eMapSide.North) != 0 && y < y0 + Margin)
                y = y0 + Margin;
            if ((outer & eMapSide.South) != 0 && y > y1 - Margin)
                y = y1 - Margin;
        }

        if (x == point.X && y == point.Y)
            return false;

        point = new Vector3(x, y, point.Z);
        return true;
    }

    /// <summary>Which sides of the zone have no ground continuing past them, i.e. the edge of the map.</summary>
    public static eMapSide OuterSidesFor(Zone zone) =>
        OuterSides.GetValue(zone, ComputeOuterSides).Sides;

    private static ZoneSides ComputeOuterSides(Zone zone) =>
        new() { Sides = ComputeOuterSidesCore(zone) };

    private static eMapSide ComputeOuterSidesCore(Zone zone)
    {
        Region region = zone.ZoneRegion;
        if (region == null)
            return eMapSide.None;

        var outer = eMapSide.West | eMapSide.East | eMapSide.North | eMapSide.South;
        float x0 = zone.XOffset;
        float y0 = zone.YOffset;
        float x1 = x0 + zone.Width;
        float y1 = y0 + zone.Height;

        foreach (Zone other in region.Zones)
        {
            if (other == null || other.ID == zone.ID)
                continue;

            float ox0 = other.XOffset;
            float oy0 = other.YOffset;
            float ox1 = ox0 + other.Width;
            float oy1 = oy0 + other.Height;

            // Any overlap past a single unit means the world continues across that line.
            bool sharesRows = Math.Min(y1, oy1) - Math.Max(y0, oy0) > 1f;
            bool sharesColumns = Math.Min(x1, ox1) - Math.Max(x0, ox0) > 1f;

            if (sharesRows && Math.Abs(ox1 - x0) <= 1f)
                outer &= ~eMapSide.West;
            if (sharesRows && Math.Abs(ox0 - x1) <= 1f)
                outer &= ~eMapSide.East;
            if (sharesColumns && Math.Abs(oy1 - y0) <= 1f)
                outer &= ~eMapSide.North;
            if (sharesColumns && Math.Abs(oy0 - y1) <= 1f)
                outer &= ~eMapSide.South;
        }

        return outer;
    }
}
