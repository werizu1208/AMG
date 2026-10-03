"""グローブ（process_part.py で整えた p_hand.fbx）に、指のボーンとスキンウェイトを付けて書き出す。

使い方:
  blender --background --python rig_hand.py -- <入力 p_hand.fbx> <出力 p_hand_rigged.fbx>

・モデルは左手のグローブ。手首が原点、指先が -Y、手の甲が +Z（Unity では指先 +Z、手の甲 +Y）
・ボーン：Hand（手のひら）＋ Thumb / Index / Middle / Ring / Pinky の各 1〜3（付け根→先端）
・ボーンの位置は、下の目安の点からメッシュの断面の中心へ自動で寄せる
・ウェイトは「各ボーンの線分からの距離」で、近い2本を滑らかに混ぜる（関節がなめらかに曲がる）
"""
import os
import shutil
import sys

import bpy
import numpy as np
from mathutils import Vector

# 目安（手首=原点、単位 m）。付け根 → 先端
FINGERS = {
    "Thumb":  [(-0.029, -0.077), (-0.039, -0.110), (-0.051, -0.128), (-0.063, -0.146)],
    "Index":  [(-0.021, -0.123), (-0.024, -0.194)],
    "Middle": [(0.000, -0.126), (0.000, -0.198)],
    "Ring":   [(0.023, -0.123), (0.028, -0.191)],
    "Pinky":  [(0.046, -0.118), (0.055, -0.167)],
}
SPLIT = (0.45, 0.30, 0.25)   # 指の3関節の長さの割合


def main():
    argv = sys.argv[sys.argv.index("--") + 1:]
    src, dst = argv[0], argv[1]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=src)
    mesh = [o for o in bpy.context.scene.objects if o.type == "MESH"][0]
    for o in list(bpy.context.scene.objects):
        if o is not mesh:
            bpy.data.objects.remove(o, do_unlink=True)
    bpy.context.view_layer.objects.active = mesh
    mesh.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    mesh.name = "Glove"
    verts = np.array([v.co[:] for v in mesh.data.vertices])

    bones = build_bone_lines(verts)
    arm = create_armature(bones)
    weights = skin_weights(verts, bones)
    bind(mesh, arm, bones, weights)

    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(
        filepath=dst,
        use_selection=False,
        object_types={"ARMATURE", "MESH"},
        apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z",
        axis_up="Y",
        primary_bone_axis="Y",
        secondary_bone_axis="X",
        add_leaf_bones=False,
        use_armature_deform_only=True,
        mesh_smooth_type="OFF",
        path_mode="STRIP",
        embed_textures=False,
    )
    copy_textures(src, dst)
    print(f"[A・M・G] 指のボーン {len(bones)} 本を付けて書き出し -> {dst}")


def center_at(verts, x, y, half_w=0.009, half_l=0.004):
    """(x, y) 付近の断面の中心（x と z）を返す"""
    sel = verts[(np.abs(verts[:, 1] - y) < half_l) & (np.abs(verts[:, 0] - x) < half_w)]
    if len(sel) < 3:
        return Vector((x, y, float(np.median(verts[:, 2]))))
    return Vector((float(sel[:, 0].mean()), y, float((sel[:, 2].min() + sel[:, 2].max()) * 0.5)))


def build_bone_lines(verts):
    """ボーン名 → (親, 始点, 終点)"""
    bones = {}
    palm_end = center_at(verts, 0.0, -0.105, half_w=0.04)
    wrist = center_at(verts, 0.0, -0.01, half_w=0.04)
    bones["Hand"] = (None, wrist, palm_end)
    for name, pts in FINGERS.items():
        if len(pts) == 4:   # 親指は目安の4点をそのまま関節に使う
            joints = [center_at(verts, x, y) for x, y in pts]
        else:
            (x0, y0), (x1, y1) = pts
            joints = []
            t = 0.0
            for f in (0.0,) + SPLIT:
                t += f
                joints.append(center_at(verts, x0 + (x1 - x0) * t, y0 + (y1 - y0) * t))
            # 先端は指の延長線上で一番遠い頂点まで伸ばす
            d = (joints[-1] - joints[-2]).normalized()
            near = verts[np.linalg.norm(verts[:, [0, 2]] - np.array([joints[-1].x, joints[-1].z]), axis=1) < 0.012]
            if len(near):
                proj = (near - np.array(joints[0][:])) @ np.array(d[:])
                far = float(proj.max())
                joints[-1] = joints[0] + d * max(far, (joints[-1] - joints[0]).length)
        parent = "Hand"
        for i in range(3):
            bone = f"{name}{i + 1}"
            bones[bone] = (parent, joints[i], joints[i + 1])
            parent = bone
    return bones


def create_armature(bones):
    data = bpy.data.armatures.new("HandArmature")
    arm = bpy.data.objects.new("Armature", data)
    bpy.context.scene.collection.objects.link(arm)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    for name, (parent, head, tail) in bones.items():
        eb = data.edit_bones.new(name)
        eb.head = head
        eb.tail = tail
        eb.roll = 0.0
        if parent:
            eb.parent = data.edit_bones[parent]
            eb.use_connect = False
    bpy.ops.object.mode_set(mode="OBJECT")
    return arm


def segment_distance(p, a, b):
    ab = b - a
    t = np.clip(((p - a) @ ab) / max(ab @ ab, 1e-12), 0.0, 1.0)
    return np.linalg.norm(p - (a + np.outer(t, ab)), axis=1), ((p - a) @ ab) / max(ab @ ab, 1e-12)


def skin_weights(verts, bones):
    names = list(bones.keys())
    dist = np.zeros((len(verts), len(names)))
    for j, n in enumerate(names):
        _, a, b = bones[n]
        d, t = segment_distance(verts, np.array(a[:]), np.array(b[:]))
        if n != "Hand":
            d = np.where(t < -0.25, 1e3, d)   # ボーンの始点より大きく手前の頂点（手のひら側）は動かさない
        dist[:, j] = d
    # 近い2本を逆距離の4乗で混ぜる
    order = np.argsort(dist, axis=1)[:, :2]
    rows = np.arange(len(verts))[:, None]
    d2 = dist[rows, order] + 1e-4
    w = 1.0 / d2 ** 4
    w /= w.sum(axis=1, keepdims=True)
    return names, order, w


def bind(mesh, arm, bones, weights):
    names, order, w = weights
    groups = {n: mesh.vertex_groups.new(name=n) for n in names}
    for i in range(len(order)):
        for k in range(2):
            if w[i, k] > 0.001:
                groups[names[order[i, k]]].add([i], float(w[i, k]), "REPLACE")
    mesh.parent = arm
    mod = mesh.modifiers.new("Armature", "ARMATURE")
    mod.object = arm


def copy_textures(src, dst):
    """マテリアル作成用に、元の p_hand のテクスチャを新しい名前でコピーする"""
    tex = os.path.join(os.path.dirname(dst), "Textures")
    a = os.path.splitext(os.path.basename(src))[0]
    b = os.path.splitext(os.path.basename(dst))[0]
    for kind in ("basecolor", "normal", "rm"):
        s = os.path.join(tex, f"{a}_{kind}.jpg")
        if os.path.exists(s):
            shutil.copyfile(s, os.path.join(tex, f"{b}_{kind}.jpg"))


main()
