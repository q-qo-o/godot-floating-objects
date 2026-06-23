using Godot;
using System;
using System.Collections.Generic;

namespace FloatingObjects.Buoyancy;

public partial class BuoyancyMesh : RigidBody3D
{
    #region Exports

    [Export]
    public Node3D WaterSurfaceNode { get; set; }

    [Export(PropertyHint.Range, "0,10000")]
    public float FluidDensity { get; set; } = 1000.0f;

    [Export(PropertyHint.Range, "0,1")]
    public float WaterDrag { get; set; } = 0.6f;

    [Export(PropertyHint.Range, "0,1")]
    public float WaterAngularDrag { get; set; } = 0.6f;

    [Export]
    public Vector3 LinearDampingTranslational { get; set; } = Vector3.Zero;

    [Export]
    public Vector3 LinearDampingRotational { get; set; } = Vector3.Zero;

    [Export]
    public Vector3 QuadraticDampingTranslational { get; set; } = Vector3.Zero;

    [Export]
    public Vector3 QuadraticDampingRotational { get; set; } = Vector3.Zero;

    [Export]
    public Vector3 AddedMassTranslational { get; set; } = Vector3.Zero;

    [Export]
    public Vector3 AddedMassRotational { get; set; } = Vector3.Zero;

    [Export]
    public bool DebugDraw { get; set; } = false;

    #endregion

    #region Internal State

    private const float FallbackWaterLevel = 0.0f;
    private float _gravity;

    public float SubmergedRatio { get; private set; } = 0.0f;
    public float VerticalVelocity { get; private set; } = 0.0f;

    private Vector3 _prevLinVelBody = Vector3.Zero;
    private Vector3 _prevAngVelBody = Vector3.Zero;

    private List<MeshEntry> _meshTriangles = new();
    private float _totalMeshVolume = 0.0f;

    private struct MeshEntry
    {
        public Vector3[] Tris;
        public Transform3D Xform;
    }

    #endregion

    #region Initialization

    public override void _Ready()
    {
        AddToGroup("floating_bodies");
        _gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();

        if (WaterSurfaceNode == null)
            WaterSurfaceNode = GetTree().CurrentScene.GetNodeOrNull<Node3D>("WaterSurface");
        if (WaterSurfaceNode == null)
            GD.PushWarning($"BuoyancyMesh: water surface node not found, using fallback level {FallbackWaterLevel}");

        CollectMeshes();
        _prevLinVelBody = Vector3.Zero;
        _prevAngVelBody = Vector3.Zero;
    }

    private void CollectMeshes()
    {
        _meshTriangles.Clear();
        _totalMeshVolume = 0.0f;
        CollectMeshesRecursive(this);

        foreach (var entry in _meshTriangles)
            _totalMeshVolume += ComputeMeshVolume(entry.Tris);

        if (_totalMeshVolume < 1e-6f)
            _totalMeshVolume = 1.0f;
    }

    private void CollectMeshesRecursive(Node node)
    {
        if (node is MeshInstance3D mi && mi.Mesh != null)
        {
            var tris = ExtractTrianglesFromMesh(mi.Mesh);
            if (tris.Length >= 3)
            {
                var xform = GlobalTransform.AffineInverse() * mi.GlobalTransform;
                _meshTriangles.Add(new MeshEntry { Tris = tris, Xform = xform });
            }
        }

        foreach (Node child in node.GetChildren())
            CollectMeshesRecursive(child);
    }

    private static Vector3[] ExtractTrianglesFromMesh(Mesh mesh)
    {
        var result = new List<Vector3>();

        for (int si = 0; si < mesh.GetSurfaceCount(); si++)
        {
            var arrays = mesh.SurfaceGetArrays(si);
            if (arrays.Count == 0) continue;

            var verts = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var indicesVar = arrays[(int)Mesh.ArrayType.Index];

            if (indicesVar.VariantType != Variant.Type.Nil)
            {
                var indices = indicesVar.AsInt32Array();
                for (int i = 0; i + 2 < indices.Length; i += 3)
                {
                    result.Add(verts[indices[i]]);
                    result.Add(verts[indices[i + 1]]);
                    result.Add(verts[indices[i + 2]]);
                }
            }
            else
            {
                for (int i = 0; i + 2 < verts.Length; i += 3)
                {
                    result.Add(verts[i]);
                    result.Add(verts[i + 1]);
                    result.Add(verts[i + 2]);
                }
            }
        }

        return result.ToArray();
    }

    private static float ComputeMeshVolume(Vector3[] tris)
    {
        double vol = 0.0;
        for (int i = 0; i + 2 < tris.Length; i += 3)
            vol += tris[i].Dot(tris[i + 1].Cross(tris[i + 2])) / 6.0;
        return (float)Math.Abs(vol);
    }

    #endregion

    #region Physics Integration

    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        if (_meshTriangles.Count == 0) return;

        float waterHeight = GetWaterHeight();
        double totalSubmergedVolume = 0.0;
        var weightedCentroid = Vector3.Zero;

        var bodyXform = state.Transform;

        foreach (var entry in _meshTriangles)
        {
            var localTris = entry.Tris;
            var worldXform = bodyXform * entry.Xform;

            for (int i = 0; i + 2 < localTris.Length; i += 3)
            {
                var v0w = worldXform * localTris[i];
                var v1w = worldXform * localTris[i + 1];
                var v2w = worldXform * localTris[i + 2];

                var clipped = ClipTriangleToWater(v0w, v1w, v2w, waterHeight);

                foreach (var tri in clipped)
                {
                    var t0 = tri[0];
                    var t1 = tri[1];
                    var t2 = tri[2];

                    var refPt = new Vector3(0.0f, waterHeight, 0.0f);
                    var r0 = t0 - refPt;
                    var r1 = t1 - refPt;
                    var r2 = t2 - refPt;

                    double dv = r0.Dot(r1.Cross(r2)) / 6.0;
                    var dc = (refPt + t0 + t1 + t2) / 4.0f;

                    totalSubmergedVolume += dv;
                    weightedCentroid += dc * (float)dv;
                }
            }
        }

        if (Math.Abs(totalSubmergedVolume) > 1e-9)
        {
            var centroidWorld = weightedCentroid / (float)totalSubmergedVolume;
            float submergedVol = (float)Math.Abs(totalSubmergedVolume);
            SubmergedRatio = Math.Clamp(submergedVol / _totalMeshVolume, 0.0f, 1.0f);

            var buoyancyForce = Vector3.Up * submergedVol * FluidDensity * _gravity;

            var localPos = bodyXform.AffineInverse() * centroidWorld;
            state.ApplyForce(buoyancyForce, localPos);

            state.LinearVelocity *= 1.0f - WaterDrag * state.Step * SubmergedRatio;
            state.AngularVelocity *= 1.0f - WaterAngularDrag * state.Step * SubmergedRatio;

            ApplyHydrodynamicDamping(state);
        }
        else
        {
            SubmergedRatio = 0.0f;
        }

        VerticalVelocity = state.LinearVelocity.Y;
        SetMeta("submerged_ratio", SubmergedRatio);
        SetMeta("vertical_velocity", VerticalVelocity);
    }

    #endregion

    #region Hydrodynamic Damping

    private void ApplyHydrodynamicDamping(PhysicsDirectBodyState3D state)
    {
        if (SubmergedRatio <= 0.001f)
        {
            _prevLinVelBody = Vector3.Zero;
            _prevAngVelBody = Vector3.Zero;
            return;
        }

        var rot = state.Transform.Basis;
        var linVelBody = rot.Transposed() * state.LinearVelocity;
        var angVelBody = rot.Transposed() * state.AngularVelocity;

        var fLin = -LinearDampingTranslational * linVelBody;
        var tLin = -LinearDampingRotational * angVelBody;

        var fQuad = -QuadraticDampingTranslational * new Vector3(
            Math.Abs(linVelBody.X) * linVelBody.X,
            Math.Abs(linVelBody.Y) * linVelBody.Y,
            Math.Abs(linVelBody.Z) * linVelBody.Z);
        var tQuad = -QuadraticDampingRotational * new Vector3(
            Math.Abs(angVelBody.X) * angVelBody.X,
            Math.Abs(angVelBody.Y) * angVelBody.Y,
            Math.Abs(angVelBody.Z) * angVelBody.Z);

        float dt = Math.Max(state.Step, 1e-6f);
        var linAccBody = (linVelBody - _prevLinVelBody) / dt;
        var angAccBody = (angVelBody - _prevAngVelBody) / dt;
        var fAm = -AddedMassTranslational * linAccBody;
        var tAm = -AddedMassRotational * angAccBody;

        var totalForceBody = (fLin + fQuad + fAm) * SubmergedRatio;
        var totalTorqueBody = (tLin + tQuad + tAm) * SubmergedRatio;

        state.ApplyCentralForce(rot * totalForceBody);
        state.ApplyTorque(rot * totalTorqueBody);

        _prevLinVelBody = linVelBody;
        _prevAngVelBody = angVelBody;
    }

    #endregion

    #region Triangle-Water Clipping

    private static List<Vector3[]> ClipTriangleToWater(Vector3 v0, Vector3 v1, Vector3 v2, float h)
    {
        var pts = new[] { v0, v1, v2 };
        bool[] below = { v0.Y < h, v1.Y < h, v2.Y < h };

        int count = 0;
        foreach (var b in below) { if (b) count++; }

        if (count == 0) return new List<Vector3[]>();
        if (count == 3) return new List<Vector3[]> { new[] { v0, v1, v2 } };

        Vector3 Intersect(Vector3 a, Vector3 b)
        {
            float dy = b.Y - a.Y;
            if (Math.Abs(dy) < 1e-9f) return a;
            float t = (h - a.Y) / dy;
            return a + (b - a) * t;
        }

        if (count == 1)
        {
            int i = Array.IndexOf(below, true);
            var p0 = pts[i];
            var p1 = pts[(i + 1) % 3];
            var p2 = pts[(i + 2) % 3];
            return new List<Vector3[]> { new[] { p0, Intersect(p0, p1), Intersect(p0, p2) } };
        }

        int j = Array.IndexOf(below, false);
        var qAbove = pts[j];
        var q1 = pts[(j + 1) % 3];
        var q2 = pts[(j + 2) % 3];
        var i1a = Intersect(q1, qAbove);
        var i2a = Intersect(q2, qAbove);
        return new List<Vector3[]>
        {
            new[] { q1, q2, i2a },
            new[] { q1, i2a, i1a }
        };
    }

    #endregion

    #region Helpers

    private float GetWaterHeight()
    {
        return WaterSurfaceNode?.GlobalPosition.Y ?? FallbackWaterLevel;
    }

    public void RefreshMeshes()
    {
        CollectMeshes();
        _prevLinVelBody = Vector3.Zero;
        _prevAngVelBody = Vector3.Zero;
    }

    #endregion
}
