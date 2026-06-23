using Godot;
using System;

namespace FloatingObjects.Buoyancy;

/// <summary>
/// Voxel-based buoyancy script (simpler, limited to BoxShape3D).
///
/// Algorithm:
///   1. Reads CollisionShape3D child (BoxShape3D only).
///   2. Divides the box into grid_resolution³ uniform voxels.
///   3. For each voxel, computes the portion below the water surface
///      and applies F = ρ·g·V at the voxel center.
///   4. Summed buoyancy approximates total force and torque.
///
/// Limitations:
///   - Only works with BoxShape3D.
///   - Discretization causes torque jumps at low voxel counts.
///     Use BuoyancyMesh for stable behavior with arbitrary shapes.
/// </summary>
public partial class Buoyancy : RigidBody3D
{
    #region Exports

    [Export]
    public Node3D WaterSurfaceNode { get; set; }

    [Export(PropertyHint.Range, "1,20")]
    public int GridResolution { get; set; } = 6;

    [Export(PropertyHint.Range, "0,10000")]
    public float FluidDensity { get; set; } = 1000.0f;

    [Export(PropertyHint.Range, "0,1")]
    public float WaterDrag { get; set; } = 0.6f;

    [Export(PropertyHint.Range, "0,1")]
    public float WaterAngularDrag { get; set; } = 0.6f;

    #endregion

    #region Internal State

    private const float FallbackWaterLevel = 0.0f;

    private float _gravity;

    /// <summary>Current submerged ratio [0, 1].</summary>
    public float SubmergedRatio { get; private set; } = 0.0f;

    /// <summary>Current vertical velocity.</summary>
    public float VerticalVelocity { get; private set; } = 0.0f;

    #endregion

    #region Initialization

    public override void _Ready()
    {
        AddToGroup("floating_bodies");
        _gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();

        if (WaterSurfaceNode == null)
            WaterSurfaceNode = GetTree().CurrentScene.GetNodeOrNull<Node3D>("WaterSurface");
        if (WaterSurfaceNode == null)
            GD.PushWarning($"Buoyancy: water surface node not found, using fallback level {FallbackWaterLevel}");
    }

    #endregion

    #region Physics Integration

    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        float waterHeight = GetWaterHeight();

        var shapeNode = GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
        shapeNode ??= FindCollisionShape();
        if (shapeNode == null) return;

        if (shapeNode.Shape is not BoxShape3D box) return;

        var size = box.Size;
        var half = size * 0.5f;
        var voxel = size / GridResolution;
        float voxelArea = voxel.X * voxel.Z;
        float totalVolume = size.X * size.Y * size.Z;
        float submergedVolume = 0.0f;

        for (int ix = 0; ix < GridResolution; ix++)
        {
            for (int iy = 0; iy < GridResolution; iy++)
            {
                for (int iz = 0; iz < GridResolution; iz++)
                {
                    var localCenter = new Vector3(
                        (ix + 0.5f) * voxel.X - half.X,
                        (iy + 0.5f) * voxel.Y - half.Y,
                        (iz + 0.5f) * voxel.Z - half.Z
                    );

                    var worldCenter = ToGlobal(localCenter);
                    float voxelBottom = worldCenter.Y - voxel.Y * 0.5f;
                    float submergedHeight = waterHeight - voxelBottom;

                    if (submergedHeight <= 0.0f) continue;
                    submergedHeight = Math.Min(submergedHeight, voxel.Y);

                    float vol = voxelArea * submergedHeight;
                    submergedVolume += vol;

                    var force = Vector3.Up * vol * FluidDensity * _gravity;
                    state.ApplyForce(force, localCenter);
                }
            }
        }

        if (submergedVolume > 0.0f)
        {
            SubmergedRatio = submergedVolume / totalVolume;
            state.LinearVelocity *= 1.0f - WaterDrag * state.Step * SubmergedRatio;
            state.AngularVelocity *= 1.0f - WaterAngularDrag * state.Step * SubmergedRatio;
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

    #region Helpers

    private float GetWaterHeight()
    {
        return WaterSurfaceNode?.GlobalPosition.Y ?? FallbackWaterLevel;
    }

    private CollisionShape3D FindCollisionShape()
    {
        foreach (Node child in GetChildren())
        {
            if (child is CollisionShape3D cs)
                return cs;
        }
        return null;
    }

    #endregion
}
