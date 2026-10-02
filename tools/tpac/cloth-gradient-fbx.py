"""Write a vertical max-distance ramp into the cloth vertex-colour alpha of a Modding Kit source FBX.

Bannerlord's GPU cloth solver limits each cloth vertex to a sphere around its skinned position whose
radius is alpha/255 * the submesh max distance (Shaders/Sources/gpucloth_solver.rs). This script keeps
every corner that is already pinned (alpha 0) and gives every other corner of the cloth material
alpha = (top - z) / (top - bottom), measured over that material's own height: the top edge is 0 and
the bottom edge fully free. Only the LOD0 object is touched (only LOD0 simulates cloth); nothing but
the colour alpha changes. Export settings match modkit-fbx.py.

Run headless with Blender:
  blender -b --factory-startup --python tools/tpac/cloth-gradient-fbx.py -- <in.fbx> <out.fbx> <cloth material>
"""
import bpy
import sys

args = sys.argv[sys.argv.index("--") + 1:]
in_path, out_path, cloth_material = args[0], args[1], args[2]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=in_path)

lod0 = [o for o in bpy.data.objects if o.type == "MESH" and ".lod" not in o.name]
for obj in lod0:
    mesh = obj.data
    slots = [i for i, s in enumerate(obj.material_slots) if s.material and s.material.name == cloth_material]
    if not slots:
        continue
    colors = mesh.color_attributes.active_color or (mesh.color_attributes[0] if mesh.color_attributes else None)
    if colors is None:
        raise SystemExit(f"{obj.name}: no colour attribute")
    world = obj.matrix_world
    cloth_loops = []
    for poly in mesh.polygons:
        if poly.material_index in slots:
            cloth_loops.extend(poly.loop_indices)
    def height(loop_index):
        return (world @ mesh.vertices[mesh.loops[loop_index].vertex_index].co).z
    def color_index(loop_index):
        return loop_index if colors.domain == "CORNER" else mesh.loops[loop_index].vertex_index
    heights = [height(i) for i in cloth_loops]
    top, bottom = max(heights), min(heights)
    pinned = changed = 0
    for loop_index, z in zip(cloth_loops, heights):
        c = colors.data[color_index(loop_index)]
        rgba = list(c.color)
        if round(rgba[3] * 255) == 0:
            pinned += 1
            continue
        alpha = round(255 * min(1.0, max(0.0, (top - z) / (top - bottom)))) / 255
        if abs(alpha - rgba[3]) > 1e-6:
            changed += 1
        rgba[3] = alpha
        c.color = rgba
    print(f"{obj.name}: material={cloth_material} domain={colors.domain} type={colors.data_type} "
          f"top={top:.4f} bottom={bottom:.4f} corners={len(cloth_loops)} pinned kept={pinned} changed={changed}")

bpy.ops.object.select_all(action="DESELECT")
for obj in bpy.data.objects:
    if obj.type in {"ARMATURE", "MESH"}:
        obj.select_set(True)
bpy.ops.export_scene.fbx(
    filepath=out_path,
    use_selection=True,
    object_types={"ARMATURE", "MESH"},
    axis_forward="Y",
    axis_up="Z",
    add_leaf_bones=False,
    bake_anim=False,
    mesh_smooth_type="OFF",
    path_mode="STRIP",
)
print(f"saved {out_path}")
