"""AI生成（Tripo）のパーツモデルを、ゲームでそのまま使える形に整えてFBXで書き出す。

使い方（Blenderをバックグラウンドで実行）:
  blender --background --python process_part.py -- <入力.glb> <出力.fbx> <パーツ名>

やること:
  1. GLBを読み込み、メッシュを1つにまとめる
  2. ポリゴンを目標数まで削減（Decimate）
  3. テクスチャを縮小
  4. ゲーム内の実寸・付け根（原点）・向きに合わせる
     - Unityで VisualSlot に入れたとき、オフセット0でそのまま骨に合うようにする
     - 手足・倒木：付け根から先端へ Unity の +Z（Blenderの -Y）に伸びる
     - 頭・胴・スカート・足：直立のまま、正面が Unity の +Z を向く
  5. テクスチャ埋め込みのFBXで書き出す（Unityの +Y 上 / +Z 前に合わせた軸変換つき）
"""
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Matrix, Vector

# kind:
#   limb    … 主軸（PCA）に沿って伸びる部品。root は付け根側の端（top=画像の上端 / bottom=下端）
#   upright … 直立の部品。height に合わせて縮尺し、pivot（下端0〜上端1の割合）を原点にする
PARTS = {
    "head":      dict(kind="upright", height=1.00, pivot=0.63, tris=9000, tex=2048),  # 原点＝首の球（上から37%）
    "torso":     dict(kind="upright", height=0.65, pivot=0.00, tris=6000, tex=2048),  # 原点＝腰側の下端
    "skirt":     dict(kind="upright", height=0.45, pivot=0.92, tris=7000, tex=2048),  # 原点＝腰（上端付近）
    "upper_arm": dict(kind="limb", length=0.40, root="top", tris=1500, tex=1024),
    "forearm":   dict(kind="limb", length=0.40, root="top", tris=1500, tex=1024),
    "hand":      dict(kind="limb", length=0.18, root="top", tris=2500, tex=1024),
    "thigh":     dict(kind="limb", length=0.44, root="top", tris=1500, tex=1024),
    "shin":      dict(kind="limb", length=0.44, root="top", tris=1500, tex=1024),
    "foot":      dict(kind="upright", height=0.15, pivot=1.00, tris=1500, tex=1024),  # 原点＝足首（上端）
    "log":       dict(kind="limb", length=6.00, root="bottom", tris=5000, tex=2048),  # 根の側が腕から生える
    # フェーズ2：巨木の怪物
    "tree_body": dict(kind="upright", height=11.2, pivot=0.00, tris=15000, tex=2048, strip_floaters=True),  # 原点＝根元。空洞が高さ6.5mに来る
    "tree_face": dict(kind="upright", height=3.20, pivot=0.75, tris=5000, tex=2048),   # 原点＝仮面の中心（髪が下に垂れる）
}


def main():
    argv = sys.argv[sys.argv.index("--") + 1:]
    src, dst, part = argv[0], argv[1], argv[2]
    cfg = PARTS[part]

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=src)

    obj = join_meshes()
    if cfg.get("strip_floaters"):
        obj = strip_floaters(obj)
    decimate(obj, cfg["tris"])
    normalize(obj, cfg)
    # テクスチャは FBX の隣の Textures/ に <パーツ名>_basecolor.jpg などで書き出す。
    # マテリアルは Unity 側（メニュー「A・M・G > ステージ1ボスのマテリアルを作成」）で URP Lit として作る
    save_textures(cfg["tex"], os.path.join(os.path.dirname(dst), "Textures"), part)

    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(
        filepath=dst,
        use_selection=False,
        object_types={"MESH"},
        apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=True,
        mesh_smooth_type="FACE",
        path_mode="STRIP",
        embed_textures=False,
    )
    tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
    print(f"[A・M・G] {part}: {tris} tris -> {dst}")


def join_meshes():
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    for o in meshes:
        # 親（GLBのノード）の変形をメッシュに焼き込む
        o.data.transform(o.matrix_world)
        o.parent = None
        o.matrix_world = Matrix.Identity(4)
    for o in list(bpy.context.scene.objects):
        if o.type != "MESH":
            bpy.data.objects.remove(o, do_unlink=True)
    bpy.ops.object.select_all(action="DESELECT")
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    obj.name = "Part"
    return obj


def strip_floaters(obj):
    """大きな本体から離れて浮いている小さなかけら（生成ミス）だけを取り除く。
    小さなかけらでも、本体に接している（＝枝の一部など）ものは残す"""
    from mathutils.kdtree import KDTree

    total = len(obj.data.vertices)
    allv = np.array([v.co[:] for v in obj.data.vertices])
    gap = float((allv.max(axis=0) - allv.min(axis=0)).max()) * 0.02  # 全体の大きさの2%以上離れていたら「浮いている」

    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.separate(type="LOOSE")
    bpy.ops.object.mode_set(mode="OBJECT")
    parts = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    big = [o for o in parts if len(o.data.vertices) >= total * 0.005]
    small = [o for o in parts if len(o.data.vertices) < total * 0.005]

    def build_tree(objs):
        cos = [v.co.copy() for o in objs for i, v in enumerate(o.data.vertices) if i % 3 == 0]
        t = KDTree(len(cos))
        for i, co in enumerate(cos):
            t.insert(co, i)
        t.balance()
        return t

    def nearest(t, o):
        verts = o.data.vertices
        step = max(1, len(verts) // 40)
        return min(t.find(verts[i].co)[2] for i in range(0, len(verts), step))

    # 本体からたどれる（近くにある）かけらを、つながりがなくなるまで繰り返し取り込む
    kept = list(big)
    rest = list(small)
    while rest:
        t = build_tree(kept)
        near = [o for o in rest if nearest(t, o) <= gap]
        if not near:
            break
        kept += near
        rest = [o for o in rest if o not in near]

    removed = 0
    for o in rest:
        bpy.data.objects.remove(o, do_unlink=True)
        removed += 1
    print(f"[A・M・G] 浮いたかけらを {removed} 個削除（全 {len(parts)} 個中）")
    keep = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    bpy.ops.object.select_all(action="DESELECT")
    for o in keep:
        o.select_set(True)
    bpy.context.view_layer.objects.active = keep[0]
    if len(keep) > 1:
        bpy.ops.object.join()
    return bpy.context.view_layer.objects.active


def decimate(obj, target_tris):
    tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
    if tris <= target_tris:
        return
    mod = obj.modifiers.new("Decimate", "DECIMATE")
    mod.decimate_type = "COLLAPSE"
    mod.ratio = target_tris / tris
    mod.use_collapse_triangulate = True
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=mod.name)


def normalize(obj, cfg):
    verts = np.array([v.co[:] for v in obj.data.vertices])
    if cfg["kind"] == "limb":
        center = verts.mean(axis=0)
        cov = np.cov((verts - center).T)
        w, vecs = np.linalg.eigh(cov)
        axis = Vector(vecs[:, np.argmax(w)]).normalized()
        # 付け根 → 先端の向きにそろえる（top：下向き / bottom：上向き）
        want_down = cfg["root"] == "top"
        if (axis.z < 0) != want_down:
            axis = -axis
        proj = (verts - center) @ np.array(axis[:])
        root = Vector(center) + axis * float(proj.min())
        length = float(proj.max() - proj.min())
        scale = cfg["length"] / length
        rot = axis.rotation_difference(Vector((0.0, -1.0, 0.0))).to_matrix().to_4x4()
        m = Matrix.Scale(scale, 4) @ rot @ Matrix.Translation(-root)
    else:
        mn, mx = verts.min(axis=0), verts.max(axis=0)
        height = float(mx[2] - mn[2])
        scale = cfg["height"] / height
        pivot = Vector(((mn[0] + mx[0]) / 2, (mn[1] + mx[1]) / 2, mn[2] + cfg["pivot"] * height))
        m = Matrix.Scale(scale, 4) @ Matrix.Translation(-pivot)
    obj.data.transform(m)
    obj.data.update()


def texture_kind(name):
    n = name.lower()
    if "normal" in n:
        return "normal"
    if "basecolor" in n or "base_color" in n or "albedo" in n or "diffuse" in n:
        return "basecolor"
    if "rm" in n or "rough" in n or "metal" in n:
        return "rm"
    return os.path.splitext(n)[0]


def save_textures(size, tex_dir, part):
    """テクスチャを縮小して <パーツ名>_<種類>.jpg で保存する（basecolor / normal / rm）"""
    os.makedirs(tex_dir, exist_ok=True)
    for img in list(bpy.data.images):
        if img.size[0] == 0:
            continue
        if max(img.size[0], img.size[1]) > size:
            img.scale(size, size)
        img.filepath_raw = os.path.join(tex_dir, f"{part}_{texture_kind(img.name)}.jpg")
        img.file_format = "JPEG"
        img.save()


main()
