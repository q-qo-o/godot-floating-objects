# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

This is a Godot 4.6 3D project named `1st-project`. The main scene is `res://terria.tscn`, configured in `project.godot`. Physics is configured to use Jolt Physics and the Windows rendering driver is D3D12.

The project is a small water/buoyancy simulation:

- `terria.tscn` is the main scene. It instantiates `water_surface.tscn`, sets up lighting/environment, creates several floating rigid bodies, and wires cameras plus camera switching.
- `water_surface.tscn` defines a large subdivided plane with an embedded spatial water shader. `water_surface.gd` drives interaction ripples by updating the shader's `ripple_data` uniform from bodies entering the water and from moving floating bodies.
- Floating objects generally use `buoyancy_mesh.gd`, which computes buoyancy from child `MeshInstance3D` triangle geometry by clipping triangles against the water plane and applying Archimedes buoyancy at the calculated center of buoyancy. It also supports optional body-frame hydrodynamic damping/added mass coefficients.
- `buoyancy.gd` is an alternate, simpler voxel-sampling buoyancy implementation for `BoxShape3D` collision shapes. Prefer `buoyancy_mesh.gd` for smoother behavior on tilted or non-box objects.
- `smooth_follow_camera.gd` implements a stable third-person follow camera that tracks position without inheriting target rotation.
- `camera_switcher.gd` switches between exported `Camera3D` references using number keys or Tab.

There is no README, Cursor rules, or Copilot instructions in this repository at the time of writing.

## Common commands

Run these from the repository root.

```bash
# Open the Godot editor for this project
godot --editor --path .

# Run the main scene configured in project.godot
godot --path .

# Run a specific scene
godot --path . res://terria.tscn

# Headless smoke check: load the project briefly, then quit
godot --headless --path . --quit-after 1
```

There are currently no automated tests, lint configuration, or `export_presets.cfg` in this repository. There is therefore no project-specific command for running a single test or building an export yet. If adding tests or export presets later, document the exact commands here.

## Architecture notes

### Water interaction contract

`water_surface.gd` expects a child `Area3D` named `Area3D` under the water mesh. It connects `body_entered` in `_ready()` and creates impact ripples based on `RigidBody3D.linear_velocity` or `CharacterBody3D.velocity`.

For continuous wake ripples, buoyant bodies must be in the `floating_bodies` group and expose metadata:

- `submerged_ratio`: used by `water_surface.gd` to decide whether the body is in water.
- `vertical_velocity`: currently published by buoyancy scripts for external readers.

Both buoyancy scripts add their body to `floating_bodies` and set these metadata keys during physics integration.

### Buoyancy setup

Both buoyancy scripts use the global Y position of `water_surface_node` as the water height. If `water_surface_node` is not assigned, they try to find a node named `WaterSurface` in the current scene and otherwise fall back to water height `0.0`.

`buoyancy_mesh.gd` requirements:

- Attach it to a `RigidBody3D`.
- Provide at least one child `MeshInstance3D`; meshes should be closed and normals should generally point outward.
- A separate `CollisionShape3D` is still needed for Godot physics collision.
- If child mesh resources or mesh nodes change at runtime, call `refresh_meshes()` to rebuild cached triangle data.

`buoyancy.gd` requirements:

- Attach it to a `RigidBody3D`.
- Provide a `CollisionShape3D` child whose shape is `BoxShape3D`.
- Tune `grid_resolution` carefully: higher values improve accuracy but multiply work by `grid_resolution^3`.

### Shader/script coupling

The water shader is embedded in `water_surface.tscn`. `water_surface.gd` writes a `PackedVector4Array` to shader parameter `ripple_data`.

If changing ripple capacity, keep these in sync:

- `MAX_RIPPLES` in `water_surface.gd`.
- The shader uniform array size `uniform vec4 ripple_data[...]` in `water_surface.tscn`.
- The shader loop bound in `sample_ripple_height()` in `water_surface.tscn`.

At the time of writing these are not synchronized: `water_surface.gd` uses `MAX_RIPPLES := 48`, while the embedded shader declares and loops over 16 ripple slots. Reconcile this before relying on more than 16 active ripples.

### Camera controls

`CameraSwitcher` in `terria.tscn` has an exported `cameras` array. Number keys 1-9 switch directly to the corresponding array entry, and Tab cycles when `enable_tab_cycle` is true. The follow camera targets `RigidBody3D1` by default.
