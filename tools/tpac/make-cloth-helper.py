"""Build a single-sheet cloth helper (simulation) mesh for a Modding Kit source FBX.

Vanilla armours simulate a separate low-poly sheet (e.g. clo_battanian_scale_armor_b) and map the render
mesh onto it: a render vertex with alpha > 0 follows the sheet at a fixed offset along its normal
(Shaders/Sources/shared_vertex_functions.rsh, USE_MAPPED_CLOTH_SIMULATION). The sheet carries the
max-distance weights in its own vertex alpha, has one submesh, LOD0 only, and is skinned.

This script takes the largest connected piece of the render mesh's cloth material (a thin closed
panel) and casts rays outward from a vertical axis inside the body; each grid point sits just inside
the panel's body-side surface, because the official docs require the simulation mesh to sit tightly
under the render mesh and never cover it. Each row spans the panel's own angular width at that
height. Where the panel is split between the legs, the sheet is split too, as vanilla split skirts
are (sturgian_fortified_armor, veteran_mercenary_armor): each half is skinned only from render
vertices on its own side, so the legs do not drag each other through the sheet.
Alpha is 0 where the nearest render-cloth vertex is pinned and otherwise rises linearly from 0 at
the top row to the bottom alpha (default 40/255) at the hem. Vanilla helpers end at 9-70/255 at
distance 1, so the Kit max distance stays 1. Self-checks report render vertices behind the sheet and
separate render pieces (the front flap) that would be torn across the split. Export settings match
modkit-fbx.py.

Run headless with Blender:
  blender -b --factory-startup --python tools/tpac/make-cloth-helper.py -- \
      <render.fbx> <out.fbx> <render object> <cloth material> <helper name> [columns rows [bottom alpha 0-255]]
"""
import bpy
import bmesh
import sys
from mathutils import Vector
from mathutils.bvhtree import BVHTree

args = sys.argv[sys.argv.index("--") + 1:]
in_path, out_path, render_name, cloth_material, helper_name = args[:5]
columns, rows = (int(args[5]), int(args[6])) if len(args) >= 7 else (16, 12)
bottom_alpha = (int(args[7]) if len(args) >= 8 else 40) / 255

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=in_path)
render = bpy.data.objects[render_name]
armature = render.parent if render.parent and render.parent.type == "ARMATURE" else next(
    o for o in bpy.data.objects if o.type == "ARMATURE")
slot = next(i for i, s in enumerate(render.material_slots) if s.material and s.material.name == cloth_material)
material = render.material_slots[slot].material
mw = render.matrix_world

# Largest connected piece of the cloth material, as a world-space BVH.
bm = bmesh.new()
bm.from_mesh(render.data)
bm.transform(mw)
cloth_faces = [f for f in bm.faces if f.material_index == slot]
cloth_set = set(cloth_faces)
seen, pieces = set(), []
for face in cloth_faces:
    if face in seen:
        continue
    stack, piece = [face], []
    seen.add(face)
    while stack:
        current = stack.pop()
        piece.append(current)
        for edge in current.edges:
            for other in edge.link_faces:
                if other in cloth_set and other not in seen:
                    seen.add(other)
                    stack.append(other)
    pieces.append(piece)
main = max(pieces, key=len)
main_verts = sorted({v for f in main for v in f.verts}, key=lambda v: v.index)
remap = {v.index: i for i, v in enumerate(main_verts)}
bvh = BVHTree.FromPolygons([v.co.copy() for v in main_verts],
                           [[remap[v.index] for v in f.verts] for f in main])
xs = [v.co.x for v in main_verts]
zs = [v.co.z for v in main_verts]
ys = [v.co.y for v in main_verts]
x0, x1, z0, z1 = min(xs), max(xs), min(zs), max(zs)

# Pinned render-cloth vertices (alpha 0) and skin weights per render vertex, for transfer.
colors = render.data.color_attributes.active_color or render.data.color_attributes[0]
corner_alpha = {}
for poly in render.data.polygons:
    if poly.material_index != slot:
        continue
    for li in poly.loop_indices:
        vi = render.data.loops[li].vertex_index
        a = colors.data[li if colors.domain == "CORNER" else vi].color[3]
        corner_alpha[vi] = min(corner_alpha.get(vi, 1.0), a)
cloth_vertex_ids = sorted(corner_alpha)
kd_points = [mw @ render.data.vertices[i].co for i in cloth_vertex_ids]
from mathutils.kdtree import KDTree
kd = KDTree(len(kd_points))
for i, p in enumerate(kd_points):
    kd.insert(p, i)
kd.balance()
group_names = {g.index: g.name for g in render.vertex_groups}


GAP = 0.005  # metres the sheet sits inside the panel's inner (body-side) surface
import math
# Rays go outward from a vertical axis inside the body, so the first hit is the panel's body-side
# surface even where it wraps back around the hips. The official cloth docs require the simulation
# mesh to sit tightly under the render mesh and never cover it.
axis_y = max(ys) + 0.05
axis_x = (x0 + x1) / 2


def direction(theta):
    return Vector((math.sin(theta), -math.cos(theta), 0.0))  # theta 0 = straight ahead (-Y)


def mid_point(theta, z):
    d = direction(theta)
    hit = bvh.ray_cast(Vector((axis_x, axis_y, z)), d)
    if hit[0] is None:
        return None
    return hit[0] - d * GAP


thetas = [math.radians(-120 + 240 * i / 480) for i in range(481)]


step = thetas[1] - thetas[0]


def span_at(z):
    """(left edge, right edge, slit or None) of the panel at height z, as ray angles."""
    inside = [t for t in thetas if mid_point(t, z) is not None]
    if len(inside) < 2:
        return None
    slit = None
    for u, w in zip(inside, inside[1:]):
        if w - u > 3 * step and (slit is None or w - u > slit[1] - slit[0]):
            slit = (u, w)  # last hit left of the gap, first hit right of it
    return inside[0], inside[-1], slit


def chord(span, z):
    return (mid_point(span[0], z) - mid_point(span[1], z)).length if span else 0.0


# The top row starts where the panel has widened to 70% of its widest span: above that is the narrow
# peak inside the belt, which stays skinned (pinned) and needs no sheet.
inset = 0.005
heights = [z1 - (z1 - z0) * i / 200 for i in range(201)]
spans = [(z, span_at(z)) for z in heights]
widest = max(chord(sp, z) for z, sp in spans)
z_top = next(z for z, sp in spans if chord(sp, z) >= 0.7 * widest)
z_bottom = z0 + inset
# The slit opens at the highest height from which every lower height has a gap.
z_open = None
for z, sp in reversed(spans):
    if z >= z_top or sp is None or z < z_bottom:
        continue
    if sp[2] is None:
        break
    z_open = z


def angle_of(p):
    return math.atan2(p.x - axis_x, -(p.y - axis_y))


# Another cloth piece (the front flap) lying over the slit holds both sides together there, as the
# real garment does: open the sheet's slit only below the lowest point where such a piece crosses.
if z_open is not None:
    crossing = []
    for piece in pieces:
        if piece is main:
            continue
        for v in {v for f in piece for v in f.verts}:
            if corner_alpha.get(v.index, 0.0) <= 0.0 or v.co.z > z_open:
                continue
            sp = span_at(v.co.z)
            if sp and sp[2] and angle_of(v.co) > (sp[2][0] + sp[2][1]) / 2:
                crossing.append(v.co.z)  # past the slit's middle: it would follow the right half
    if crossing:
        below = [z for z, sp in spans if z < min(crossing) - 0.04 and z >= z_bottom]  # a row of margin
        z_open = below[0] if below else None
row_z = [z_top - (z_top - z_bottom) * r / (rows - 1) for r in range(rows)]
if z_open is not None:
    k = min(range(1, rows), key=lambda r: abs(row_z[r] - z_open))
    row_z[k] = z_open
    bottom_span = span_at(z_bottom)
    lw, rw = bottom_span[2][0] - bottom_span[0], bottom_span[1] - bottom_span[2][1]
    left_columns = max(2, min(columns - 2, round(columns * lw / (lw + rw))))
    apex = sum(span_at(z_open)[2]) / 2
else:
    left_columns = columns
gapped_rows = set()
grid = {}
for r, z in enumerate(row_z):
    tl, tr, slit = span_at(z)
    if z_open is not None and z <= z_open + 1e-6 and slit is not None:
        gapped_rows.add(r)
        segments = [(0, left_columns, tl, slit[0]), (left_columns, columns, slit[1], tr)]
    elif z_open is not None:
        half = (tr - tl) / (columns - 1) / 2
        segments = [(0, left_columns, tl, apex - half), (left_columns, columns, apex + half, tr)]
    else:
        segments = [(0, columns, tl, tr)]
    for c0, c1, t0, t1 in segments:
        for c in range(c0, c1):
            t = t0 + (t1 - t0) * (c - c0) / max(1, c1 - c0 - 1)
            p = mid_point(t, z)
            if p is None:
                # A small hole in the panel: step inward to the nearest hit.
                for k2 in range(1, 40):
                    p = mid_point(t + k2 * step * (1 if t < (t0 + t1) / 2 else -1), z)
                    if p is not None:
                        break
            grid[(r, c)] = p


def side_of(r, c):
    """-1 left half, +1 right half, 0 above the slit."""
    if r not in gapped_rows:
        return 0
    return -1 if c < left_columns else 1


mesh = bpy.data.meshes.new(helper_name)
index = {}
verts = []
for key in sorted(grid):
    if grid[key] is None:
        continue
    index[key] = len(verts)
    verts.append(grid[key])
faces = []
for r in range(rows - 1):
    for c in range(columns - 1):
        if c == left_columns - 1 and r in gapped_rows:
            continue  # the slit
        quad = [(r, c), (r + 1, c), (r + 1, c + 1), (r, c + 1)]
        if all(k in index for k in quad):
            a, b, cc, d = (index[k] for k in quad)
            faces.append((a, b, cc))
            faces.append((a, cc, d))
mesh.from_pydata(verts, [], faces)
mesh.update()
helper = bpy.data.objects.new(helper_name, mesh)
bpy.context.scene.collection.objects.link(helper)
helper.data.materials.append(material)

# Planar UV so the mesh imports cleanly; the sheet is never drawn.
uv = mesh.uv_layers.new(name="UVMap")
for loop in mesh.loops:
    p = verts[loop.vertex_index]
    uv.data[loop.index].uv = ((p.x - x0) / (x1 - x0), (p.z - z0) / (z1 - z0))

# Skin weights: inverse-distance average of the three nearest main-panel vertices on the same side
# of the slit, renormalised.
main_ids = [v.index for v in main_verts]
main_points = [mw @ render.data.vertices[i].co for i in main_ids]
kd_main = KDTree(len(main_points))
for i, p in enumerate(main_points):
    kd_main.insert(p, i)
kd_main.balance()
key_of = {vi: key for key, vi in index.items()}
for name in group_names.values():
    helper.vertex_groups.new(name=name)
for vi, p in enumerate(verts):
    side = side_of(*key_of[vi])
    candidates = kd_main.find_n(p, 24)
    if side:
        candidates = [x for x in candidates if (angle_of(x[0]) - apex) * side > 0 or x[0].z > z_open]
    accum = {}
    for co, k, dist in candidates[:3]:
        w_near = 1.0 / max(dist, 1e-5)
        for g in render.data.vertices[main_ids[k]].groups:
            accum[g.group] = accum.get(g.group, 0.0) + g.weight * w_near
    total = sum(accum.values()) or 1.0
    for gi, w in accum.items():
        if w / total > 1e-4:
            helper.vertex_groups[group_names[gi]].add([vi], w / total, "REPLACE")

# Max-distance weights in vertex alpha.
top, bottom = max(p.z for p in verts), min(p.z for p in verts)
alpha_attr = mesh.color_attributes.new(name="Color", type="BYTE_COLOR", domain="CORNER")
pinned = 0
vertex_alpha = []
for p in verts:
    _, k, _ = kd.find(p)
    if corner_alpha[cloth_vertex_ids[k]] <= 0.0:
        a = 0.0
        pinned += 1
    else:
        a = round(255 * bottom_alpha * min(1.0, max(0.0, (top - p.z) / (top - bottom)))) / 255
    vertex_alpha.append(a)
for loop in mesh.loops:
    alpha_attr.data[loop.index].color = (1.0, 1.0, 1.0, vertex_alpha[loop.vertex_index])
mesh.color_attributes.active_color = alpha_attr

helper.parent = armature
modifier = helper.modifiers.new("Armature", "ARMATURE")
modifier.object = armature

# Self-check: every render-cloth vertex (all pieces) must lie on the sheet's outer (normal) side.
helper_bvh = BVHTree.FromPolygons([v.copy() for v in verts], faces)
# Pinned render vertices (alpha 0) keep their skinning and are not mapped, so they are exempt.
behind = []
mapped = 0
for k, p in enumerate(kd_points):
    if corner_alpha[cloth_vertex_ids[k]] <= 0.0:
        continue
    mapped += 1
    hit = helper_bvh.find_nearest(p)
    if hit[0] is None:
        continue
    d = (p - hit[0]).dot(hit[1])
    if d < -1e-4:
        behind.append((d, p))
worst = min((d for d, _ in behind), default=0.0)
print(f"check: mapped render vertices behind the sheet = {len(behind)}/{mapped} (worst {worst*100:.2f} cm)")
for d, p in sorted(behind)[:12]:
    print(f"   behind {d*100:6.2f} cm at x={p.x:.3f} y={p.y:.3f} z={p.z:.3f}")

# Self-check: a separate render piece (e.g. the front flap) must not map onto both halves of the slit.
if gapped_rows:
    vertex_side = [side_of(*key_of[i]) for i in range(len(verts))]
    # A face belongs to one half only when all three corners are below the opening on that side.
    face_side = []
    for face in faces:
        sides_of_face = {vertex_side[i] for i in face}
        face_side.append(sides_of_face.pop() if len(sides_of_face) == 1 else 0)
    for n_piece, piece in enumerate(pieces):
        if piece is main:
            continue
        sides = {}
        for v in {v for f in piece for v in f.verts}:
            if corner_alpha.get(v.index, 0.0) <= 0.0:
                continue
            fi = helper_bvh.find_nearest(v.co)[2]
            sides[face_side[fi]] = sides.get(face_side[fi], 0) + 1
        torn = "  <-- TORN across the slit" if sides.get(-1) and sides.get(1) else ""
        print(f"check: render piece {n_piece} ({len(piece)} faces) maps to halves {sides}{torn}")
    print(f"slit: opens at z={z_open:.3f}, left half {left_columns} columns, right half {columns - left_columns}")

# Keep only the armature and the helper for export.
for o in list(bpy.data.objects):
    if o not in (armature, helper):
        bpy.data.objects.remove(o, do_unlink=True)

print(f"{helper_name}: grid {columns}x{rows} verts={len(verts)} faces={len(faces)} "
      f"x[{x0:.3f},{x1:.3f}] z[{z0:.3f},{z1:.3f}] pinned={pinned} "
      f"alpha min={min(vertex_alpha):.3f} max={max(vertex_alpha):.3f} main piece faces={len(main)}")

bpy.ops.object.select_all(action="DESELECT")
armature.select_set(True)
helper.select_set(True)
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
