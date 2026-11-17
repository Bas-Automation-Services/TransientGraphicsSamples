using Inventor;
using BasAutomation.Geometry;
using BasAutomation.Geometry.Curves;
using BasAutomation.Geometry.Surfaces;
using BasAutomation.Geometry.Surfaces.SweptSurfaces;
using static TransientGraphics.Samples.Post04_CapFaces;

namespace TransientGraphics.Samples;

// Post 9: a beam swept along a circular arc. Same topology as the straight beam; the connectors
// become sweep arcs and each profile curve sweeps to a plane, cylinder, cone, torus or — for an
// ellipse — a B-spline surface, depending on how it sits relative to the centerline axis.
public static class Post09_CurvedBeam
{
    public static SurfaceBody Build(
        Inventor.Application app,
        IReadOnlyList<ICurve3D> outerProfile,
        IReadOnlyList<IReadOnlyList<ICurve3D>> innerProfiles,
        Arc3D centerline)
    {
        var geo = new InventorGeometry(app.TransientGeometry);
        var bodyDef  = app.TransientBRep.CreateSurfaceBodyDefinition();
        var shellDef = bodyDef.LumpDefinitions.Add().FaceShellDefinitions.Add();

        // Profile plane at the arc start, normal along the tangent. The end profile is the start
        // profile rotated about the arc's axis by the sweep angle.
        var startTangent = centerline.TangentAt(0);
        var endTangent   = centerline.TangentAt(1);
        var atStart  = Profiles.FrameAt(centerline.StartPoint, startTangent);
        var sweep    = Matrix3D.CreateRotationAroundPoint(centerline.Normal, centerline.SweepAngle, centerline.Center);
        var atEnd    = sweep * atStart;

        var startOuter = AddProfileEdges(bodyDef, geo, Profiles.Transform(outerProfile, atStart));
        var endOuter   = AddProfileEdges(bodyDef, geo, Profiles.Transform(outerProfile, atEnd));
        var startInner = innerProfiles.Select(p => AddProfileEdges(bodyDef, geo, Profiles.Transform(p, atStart))).ToList();
        var endInner   = innerProfiles.Select(p => AddProfileEdges(bodyDef, geo, Profiles.Transform(p, atEnd))).ToList();

        AddCapFace(shellDef, geo, new Plane3D(centerline.StartPoint, -startTangent), startOuter, startInner, outwardIsProfileNormal: false);
        AddCapFace(shellDef, geo, new Plane3D(centerline.EndPoint, endTangent), endOuter, endInner, outwardIsProfileNormal: true);

        // Side faces: connectors are sweep arcs; surfaces come from the classifier below.
        object Connector(Point3D p) => geo.Arc(SweepArcThrough(p, centerline));
        object Surface(ICurve3D c)  => SweptSurface(geo, c, centerline);

        Post05_StraightBeam.AddSideFaces(bodyDef, shellDef, Profiles.Transform(outerProfile, atStart), startOuter, endOuter, Connector, Surface);
        for (int k = 0; k < innerProfiles.Count; k++)
            Post05_StraightBeam.AddSideFaces(bodyDef, shellDef, Profiles.Transform(innerProfiles[k], atStart), startInner[k], endInner[k], Connector, Surface);

        var body = bodyDef.CreateTransientSurfaceBody(out NameValueMap errors);
        Post02_ConstructionTree.ThrowIfErrors(errors);
        return body;
    }

    // The circle a point traces when swept along the centerline: same axis, radius equal to the
    // point's radial distance, in the plane offset by the point's height along the axis.
    public static Arc3D SweepArcThrough(Point3D point, Arc3D centerline)
    {
        var (radial, height) = LineSweptSurfaceClassifier.GetRadialDistanceAndHeight(point, centerline);
        var sweepCentre = centerline.Center + centerline.Normal * height;
        var toPoint     = (point - sweepCentre).Normalized();
        return new Arc3D(sweepCentre, centerline.Normal, toPoint, radial,
                         startAngle: 0, endAngle: centerline.SweepAngle,
                         isCounterClockwise: centerline.SweepAngle >= 0);
    }

    // The surface one profile curve sweeps to along a circular centerline.
    public static object SweptSurface(InventorGeometry geo, ICurve3D curve, Arc3D centerline)
    {
        var tg = geo.Tg;
        switch (curve)
        {
            case Line3D line:
                switch (LineSweptSurfaceClassifier.Classify(line, centerline))
                {
                    case LineSweptSurfaceKind.RadialPlane:
                    {
                        // A radial line stays in its own plane perpendicular to the axis.
                        var (_, height) = LineSweptSurfaceClassifier.GetRadialDistanceAndHeight(line.MidPoint, centerline);
                        var origin = centerline.Center + centerline.Normal * height;
                        return geo.Pln(new Plane3D(origin, centerline.Normal));
                    }
                    case LineSweptSurfaceKind.Cylinder:
                    {
                        var (center, axis, radius) = CylinderFromArcSweep.Compute(line, centerline);
                        return tg.CreateCylinder(geo.Pt(center), geo.Unit(axis), radius);
                    }
                    default:
                    {
                        var (root, axis, rootRadius, halfAngle) = ConeFromArcSweep.Compute(line, centerline);
                        return tg.CreateCone(geo.Pt(root), geo.Unit(axis), rootRadius, halfAngle, IsExpanding: true);
                    }
                }

            case Arc3D arc:
            {
                var (center, axis, major, minor) = TorusFromArcSweep.Compute(arc, centerline);
                return tg.CreateTorus(geo.Pt(center), geo.Unit(axis), major, minor);
            }

            case Circle3D circle:
            {
                var (center, axis, major, minor) = TorusFromArcSweep.Compute(circle, centerline);
                return tg.CreateTorus(geo.Pt(center), geo.Unit(axis), major, minor);
            }

            case Ellipse3D ellipse:
            {
                // Not one of Inventor's analytic surfaces: an exact rational B-spline surface,
                // the tensor product of the ellipse and the sweep arc (both exact as quadratics).
                var (order, poles, knotsU, knotsV, weights, isPeriodic) = BSplineEllipseSweep.ComputeSurface(ellipse, centerline);
                return tg.CreateBSplineSurface(ref order, ref poles, ref knotsU, ref knotsV, ref weights, ref isPeriodic);
            }

            default:
                throw new NotSupportedException(curve.GetType().Name);
        }
    }
}
