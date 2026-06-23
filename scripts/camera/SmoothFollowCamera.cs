using Godot;

namespace FloatingObjects.Camera;

/// <summary>
/// Smooth follow camera (third-person). Follows target position without
/// inheriting its rotation. Camera maintains a fixed offset and always
/// looks at the target.
/// </summary>
public partial class SmoothFollowCamera : Camera3D
{
    [Export]
    public Node3D Target { get; set; }

    /// <summary>Offset from target in world space (not inherited from target rotation).</summary>
    [Export]
    public Vector3 Offset { get; set; } = new(0.0f, 3.5f, 6.0f);

    /// <summary>Smoothing factor (0=no movement, 1=instant, 0.1~0.3 is smooth).</summary>
    [Export(PropertyHint.Range, "0,1")]
    public float Smoothness { get; set; } = 0.2f;

    /// <summary>Whether to always look at the target center.</summary>
    [Export]
    public bool AlwaysLookAtTarget { get; set; } = true;

    /// <summary>Vertical offset for the look-at point (to avoid looking at feet).</summary>
    [Export]
    public float LookAtHeightOffset { get; set; } = 0.5f;

    public override void _PhysicsProcess(double delta)
    {
        if (Target == null) return;

        var targetPos = Target.GlobalPosition;
        var desiredPos = targetPos + new Vector3(Offset.X, Offset.Y, Offset.Z);

        GlobalPosition = GlobalPosition.Lerp(desiredPos, Smoothness);

        if (AlwaysLookAtTarget)
        {
            var lookAtPos = targetPos + new Vector3(0.0f, LookAtHeightOffset, 0.0f);
            LookAt(lookAtPos, Vector3.Up);
        }
    }
}
