# Godot Floating Objects

A small Godot 4.6 3D water and buoyancy simulation. The project demonstrates floating rigid bodies, interaction ripples on a procedural water surface, and camera switching/follow-camera controls.

## Features

- Procedural water plane with noise-based waves and impact/wake ripples.
- Mesh-based buoyancy for closed `MeshInstance3D` geometry, including optional hydrodynamic damping and added mass coefficients.
- Simpler voxel-based buoyancy script for `BoxShape3D` bodies.
- Multiple floating bodies in the main scene, including boxes and a sphere.
- Camera switching with number keys and Tab.
- Smooth third-person follow camera that tracks position without inheriting target rotation.

## Requirements

- Godot 4.6 or newer.
- Jolt Physics support enabled by the engine/project configuration.

## Running the project

From the repository root:

```bash
# Open the project in the Godot editor
godot --editor --path .

# Run the configured main scene
godot --path .

# Run the main scene explicitly
godot --path . res://scenes/terria.tscn
```

The main scene is `res://scenes/terria.tscn`.

## Controls

- Press `1`-`9` to switch directly to a configured camera.
- Press `Tab` to cycle cameras.

## Project structure

```text
assets/
  icons/        Project icon and import metadata.
scenes/         Main scene and reusable water surface scene.
scripts/
  buoyancy/     Mesh-based and voxel-based buoyancy scripts.
  camera/       Camera switching and smooth follow camera scripts.
  water/        Water surface ripple driver script.
```

## Buoyancy setup notes

For the mesh-based buoyancy script:

- Attach `scripts/buoyancy/buoyancy_mesh.gd` to a `RigidBody3D`.
- Add at least one child `MeshInstance3D`; closed meshes with outward normals work best.
- Add a separate `CollisionShape3D` for regular physics collision.
- Assign `water_surface_node`, or provide a node named `WaterSurface` in the current scene.

For the voxel-based buoyancy script:

- Attach `scripts/buoyancy/buoyancy.gd` to a `RigidBody3D`.
- Add a child `CollisionShape3D` using `BoxShape3D`.
- Tune `grid_resolution` carefully because work scales with `grid_resolution^3`.

## Notes

There are currently no automated tests, lint configuration, or export presets in this repository.
