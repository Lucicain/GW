"""Turn tpac-export's animation FBX into files the Bannerlord Modding Kit can import.

tpac-export writes each SkeletalAnimation with a placeholder mesh and the original
0-based frame times. This keeps only the armature (vanilla human_skeleton, 28 bones),
names it human_skeleton_notused and the action after the original take, so the Kit's
take name matches the original "human_skeleton_notused|<take>", and exports with the
same axis settings as the models (Y forward, Z up, no leaf bones), without key
simplification and without Blender's default one-frame import offset.

Run headless with Blender:
  blender -b --factory-startup --python tools/tpac/modkit-anim.py -- <in_dir> <out_dir>
"""
import bpy
import os
import sys

args = sys.argv[sys.argv.index("--") + 1:]
in_dir, out_dir = args[0], args[1]
os.makedirs(out_dir, exist_ok=True)

for f in sorted(os.listdir(in_dir)):
    if not f.lower().endswith(".fbx"):
        continue
    take = os.path.splitext(f)[0].replace("human_skeleton_notused_", "")
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=os.path.join(in_dir, f), anim_offset=0.0)
    for o in [o for o in bpy.data.objects if o.type != "ARMATURE"]:
        bpy.data.objects.remove(o)
    arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    arm.name = "human_skeleton_notused"
    act = arm.animation_data.action
    act.name = take
    # Root motion rides on the armature object's location. Blender reads it as 5.95 where the
    # package says 0.0595 and writes it back out the same way; the FBX is in centimetres and the
    # Kit converts to metres, so the Kit ends up with exactly the package's 0.0595. Do not rescale
    # it: an earlier x0.01 "fix", judged with Assimp (which ignores the unit), left the Kit's root
    # motion 100x too small (2026-09-27).
    start, end = (int(round(x)) for x in act.frame_range)
    scene = bpy.context.scene
    scene.render.fps = 24
    scene.frame_start, scene.frame_end = start, end
    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    out = os.path.join(out_dir, take + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=out,
        use_selection=True,
        object_types={"ARMATURE"},
        axis_forward="Y",
        axis_up="Z",
        add_leaf_bones=False,
        bake_anim=True,
        # One take per action, named "<armature>|<action>" = "human_skeleton_notused|<take>",
        # the original names. Exporting the active action alone names every take "Scene",
        # and the Kit then made one animation and skipped the other three as duplicates.
        bake_anim_use_all_actions=True,
        bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True,
        bake_anim_step=1.0,
        bake_anim_simplify_factor=0.0,
    )
    print(f"ANIM {take}: frames {start}-{end} bones={len(arm.data.bones)} -> {out}")
