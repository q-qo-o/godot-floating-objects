using Godot;
using System;
using System.Collections.Generic;

namespace FloatingObjects.Water;

/// <summary>
/// Drives procedural water surface: manages ripple interaction data sent to the shader,
/// handles impact ripples from body collisions, and generates wake ripples for moving
/// floating objects.
/// </summary>
public partial class WaterSurface : MeshInstance3D
{
    private const int MaxRipples = 32;
    private const float RippleDuration = 4.0f;
    private const float WakeInterval = 0.15f;

    private readonly List<Vector4> _ripples = new();
    private ShaderMaterial _material;
    private float _timeOffset;
    private readonly Dictionary<ulong, Vector3> _floaterPrevPos = new();
    private readonly Dictionary<ulong, float> _floaterWakeTimer = new();

    private Area3D _area;

    public override void _Ready()
    {
        _material = MaterialOverride as ShaderMaterial;
        _material ??= GetSurfaceOverrideMaterial(0) as ShaderMaterial;
        _material ??= Mesh?.SurfaceGetMaterial(0) as ShaderMaterial;
        _timeOffset = Time.GetTicksMsec() / 1000.0f;

        _area = GetNode<Area3D>("Area3D");
        _area.BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node3D body)
    {
        float speed = 0.0f;
        if (body is RigidBody3D rb)
            speed = rb.LinearVelocity.Length();
        else if (body is CharacterBody3D cb)
            speed = cb.Velocity.Length();

        float intensity = Math.Clamp(speed * 0.06f, 0.08f, 1.5f);
        AddRipple(body.GlobalPosition, intensity);
    }

    private void AddRipple(Vector3 worldPos, float intensity = 1.0f)
    {
        float now = Time.GetTicksMsec() / 1000.0f - _timeOffset;
        var newRipple = new Vector4(worldPos.X, worldPos.Z, now, intensity);

        if (_ripples.Count < MaxRipples)
        {
            _ripples.Add(newRipple);
        }
        else
        {
            int oldestIdx = 0;
            float oldestTime = _ripples[0].Z;
            for (int i = 1; i < _ripples.Count; i++)
            {
                if (_ripples[i].Z < oldestTime)
                {
                    oldestTime = _ripples[i].Z;
                    oldestIdx = i;
                }
            }
            _ripples[oldestIdx] = newRipple;
        }
    }

    public override void _Process(double delta)
    {
        float now = Time.GetTicksMsec() / 1000.0f - _timeOffset;
        var active = new List<Vector4>();
        var valid = new List<Vector4>();

        foreach (var r in _ripples)
        {
            float elapsed = now - r.Z;
            if (elapsed < 0.0f) continue;
            float t = elapsed / RippleDuration;
            float intensity = r.W * Math.Max(0.0f, 1.0f - t * t);
            if (intensity > 0.005f)
            {
                valid.Add(r);
                active.Add(new Vector4(r.X, r.Y, elapsed, intensity));
            }
        }

        _ripples.Clear();
        _ripples.AddRange(valid);

        while (active.Count < MaxRipples)
            active.Add(Vector4.Zero);

        _material?.SetShaderParameter("ripple_data", new PackedVector4Array(active.ToArray()));

        // Wake ripples for moving floating bodies
        var bodies = GetTree().GetNodesInGroup("floating_bodies");
        foreach (var body in bodies)
        {
            var body3d = body as Node3D;
            if (body3d == null) continue;

            float sr = body.GetMeta("submerged_ratio", 0.0f).AsSingle();
            if (sr < 0.05f)
            {
                _floaterPrevPos.Remove(body.GetInstanceId());
                _floaterWakeTimer.Remove(body.GetInstanceId());
                continue;
            }

            Vector3 vel = Vector3.Zero;
            if (body is RigidBody3D rb)
                vel = rb.LinearVelocity;
            else if (body is CharacterBody3D cb)
                vel = cb.Velocity;

            float horizontalSpeed = new Vector3(vel.X, 0.0f, vel.Z).Length();
            if (horizontalSpeed < 0.1f)
            {
                _floaterPrevPos[body.GetInstanceId()] = body3d.GlobalPosition;
                continue;
            }

            var prev = _floaterPrevPos.GetValueOrDefault(body.GetInstanceId(), body3d.GlobalPosition);
            float dist = prev.DistanceTo(body3d.GlobalPosition);
            float timer = _floaterWakeTimer.GetValueOrDefault(body.GetInstanceId(), 0.0f);
            timer += (float)delta;

            if (dist > 0.03f && timer >= WakeInterval)
            {
                float intensity = Math.Clamp(horizontalSpeed * 0.04f, 0.04f, 0.8f);
                AddRipple(body3d.GlobalPosition, intensity);
                _floaterWakeTimer[body.GetInstanceId()] = 0.0f;
            }
            else
            {
                _floaterWakeTimer[body.GetInstanceId()] = timer;
            }

            _floaterPrevPos[body.GetInstanceId()] = body3d.GlobalPosition;
        }
    }
}
