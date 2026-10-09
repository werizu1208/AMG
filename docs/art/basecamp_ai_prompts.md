# 拠点（ベースキャンプ）AI生成プロンプト集

対魔法少女殲滅部隊 A・M・G の拠点を、テキストプロンプトから3D生成するためのプロンプト集です。
Notion「拠点」ページと、手描きのラフ（`Assets/AMG/Textures/UI/` の CampBase / Base_Stage / Base_Upgrade / Base_kennkyuu / Stage_select / Upgrade / kenkyuusitu）を元にしています。

AI生成は英語のほうが安定するので、プロンプトは英語、説明は日本語にしています。

---

## 拠点の構成

Basecamp シーンは、部屋（約10m × 10m）が横に並んでいて、メニューのボタンに合わせてカメラが切り替わります。

| 部屋（シーン上の名前） | メニュー | 映すもの | ここに置くモデル |
|---|---|---|---|
| `FirstRoom` | どれにも合わせていない・やめる | 拠点を見下ろす | 拠点の全景（2-1〜2-6） |
| `StageSelectRoom` | 作戦開始 | 大量のモニターと司令官のいる司令塔 | 模型の地図・モニター壁・司令卓（3-1〜3-4） |
| `FirstRoom (2)` | 装備・強化・変更 | 待機中のプレイヤー | ウェポンラック・作業台・武器（4-1〜4-4） |
| `FirstRoom (3)` | 武器・作成・研究 | 研究室のホログラムの設計図 | 標本カプセル・実験台・ホログラム台・研究素材（5-1〜5-6） |

### 世界観から決めた見た目の方針

- A・M・G は世界が結託して作った **完全に秘匿された裏の組織**。表向きは存在しない部隊なので、拠点は **仮設の軍事キャンプ**（テント・コンテナ・プレハブ）で、豪華な基地ではない
- 魔法的な手段は察知されるので、**人間の技術だけ**で作られている（魔法陣や光る魔法の装置は置かない）
- ただし研究室だけは、**魔法少女の死骸を研究**しているので、標本や異質な素材が並ぶ不気味な場所にする
- 体験の軸が「絶望感・緊張感」なので、明るく清潔ではなく、**使い込まれて汚れた**雰囲気にする

---

## 進め方

1. **デザイン画を作る**（画像生成AI）… 「1. デザイン画」
2. **3D生成する**（Tripo / Meshy / Rodin など）… 「2〜5」の各プロンプト
   - Text to 3D でも作れますが、手順1のデザイン画から該当部分を切り抜いて **Image to 3D** に使うと、拠点全体の統一感が出ます
   - 1つのプロンプトで **1つの物** を作ります（部屋まるごとは作らない。部屋の床・壁は Unity の基本形状のままか、別途作る）
3. **Blenderで整える** … 「6. Blenderでの整え方」
4. **Unityに置く** … 「7. Unityでの配置」

---

## 共通のスタイル指定

すべてのプロンプトの末尾に付ける共通部分です。

```
covert military special forces base, makeshift field camp, modern equipment,
gritty realistic, worn and used, scratches, dust and grime,
muted olive drab, dark gray and khaki palette, matte surfaces,
stylized realistic, game-ready asset, PBR textures, clean topology, low poly,
single object, centered, neutral lighting, plain background
```

共通のネガティブプロンプト（使えるツールのみ）：

```
magic circle, glowing runes, fantasy, medieval, sci-fi spaceship, neon,
bright colors, glossy plastic, cartoon, chibi, clean new,
multiple objects, scene, room, ground plane, pedestal, text, logo, watermark, blurry
```

> 研究室の標本など「不気味さ」を出したいものは、各プロンプトの中で個別に指定しています。

---

## 1. デザイン画（画像生成AI用）

### 1-1. 拠点の全景（見下ろし）… CampBase

```
concept art, top-down three-quarter view of a hidden military field base at dusk,
chain-link fences topped with barbed wire around the perimeter,
a large olive green command tent in the center back,
a military helicopter and an armored truck parked on the left,
sandbags and supply crates stacked on the right,
an equipment workbench under a tarp canopy at the front left,
a white prefab research building at the front right,
floodlights on poles, mud and gravel ground, ruined city skyline far away,
tense and desperate atmosphere, muted olive and gray palette
```

### 1-2. 司令塔の中（作戦開始）… Base_Stage / Stage_select

```
concept art, interior of a military command center inside a large tent,
a wall of many monitors showing maps and data,
a large table in the center with a terrain model map,
the map shows a huge crater in the middle and six surrounding areas connected by lines,
red pins and markers on the map, a commander with a headset standing by the table,
operators at desks with laptops, dim lighting from the screens,
tense atmosphere, muted olive and gray palette
```

### 1-3. 装備・強化（ウェポンラックと作業台）… Base_Upgrade / Upgrade

```
concept art, front view of a military armory corner,
a pegboard wall with hanging tools, a pistol and an assault rifle on hooks,
a long heavy workbench in front with a disassembled rifle and parts on it,
a soldier in tactical gear and headset standing idle holding a rifle,
work lamp lighting, worn metal and wood, muted olive and gray palette
```

### 1-4. 研究室（武器・作成・研究）… kenkyuusitu / Base_kennkyuu

```
concept art, interior of a secret research laboratory,
a tall cylindrical specimen tank filled with green liquid,
a dark silhouette of a girl floating inside the tank,
a wall board with pinned strange specimens and fragments,
a lab bench with flasks and test tubes,
thick black cables running across the floor to the tank,
a holographic blueprint of a weapon projected above a table,
eerie and cold atmosphere, sterile white walls stained with grime,
muted palette with sickly green accents
```

---

## 2. 拠点の全景（`FirstRoom`）

表の「大きさ」はゲーム内の実寸（メートル）です。生成後にBlenderでこの大きさに合わせます。
原点はすべて **底面の中心**（床に置いたときに接する位置）にします。

| モデル | 大きさ（m） | 備考 |
|---|---|---|
| 作戦会議テント | 幅8 × 奥行6 × 高さ4 | 拠点の中心。外観だけ |
| フェンス（1区画） | 長さ4 × 高さ3 | 並べて使う |
| ヘリコプター | 長さ14 × 高さ4 | 遠景 |
| 装甲トラック | 長さ7 × 高さ3 | 遠景 |
| 土のう・補給コンテナ | 1〜2 | 散らして置く |
| 投光器 | 高さ5 | 照明の位置の目印にもなる |

### 2-1. 作戦会議テント

```
a large military command tent, olive drab canvas, rectangular shape,
roll-up door flap open at the front, guy ropes and stakes,
camouflage net draped over the roof, antenna mast on the side
```

### 2-2. フェンス

```
a section of chain-link fence with barbed wire coils on top,
steel posts, slightly bent and rusty, warning sign plate without text
```

### 2-3. ヘリコプター

```
a military transport helicopter, matte dark olive, parked with rotors stopped,
worn paint, no markings
```

### 2-4. 装甲トラック

```
a military armored transport truck, matte dark olive, canvas-covered cargo bed,
mud on the tires, no markings
```

### 2-5. 土のうと補給コンテナ

```
a small stack of military sandbags and two olive green supply crates,
worn canvas and scratched metal
```

### 2-6. 投光器

```
a tall mobile floodlight tower on a small trailer, four lamps on top,
military olive paint, cables hanging
```

---

## 3. 司令塔（作戦開始 / `StageSelectRoom`）

| モデル | 大きさ（m） | Unityでの置き場所 | 備考 |
|---|---|---|---|
| 模型の地図（台ごと） | 幅2.4 × 奥行1.6 × 高さ1.0 | `MapTable` を差し替え | **ステージの印（球）は Unity 側で上に置く**ので、模型には付けない |
| モニター壁 | 幅5 × 高さ2.5 | 部屋の奥の壁 | 画面は暗い色のままでよい（Unity で光らせる） |
| 司令卓 | 幅2 × 奥行1 × 高さ0.8 | 地図の横 | |
| 司令官 | 身長1.8 | 地図の奥 | 立ちポーズ。動かさない置物 |

### 3-1. 模型の地図

Notion「ゲーム全体の進行」に合わせ、**星の落下地点（巨大クレーター）を中心に、周りに6つのエリア** が広がる地形にします。

```
a military sand table terrain model on a sturdy wooden table,
miniature landscape with a huge impact crater in the center,
six distinct regions around the crater: an overgrown village, a ruined city,
a forest, mountains, a river delta and a wasteland,
tiny ruined buildings, grid lines and coordinate markings on the table edge,
handmade model look, top surface flat and readable from above
```

### 3-2. モニター壁

```
a wall of many computer monitors mounted on a metal rack,
3 rows of 5 screens, cables bundled behind, dark screens,
military command center equipment
```

### 3-3. 司令卓

```
a military command desk with two laptops, a radio set, headsets,
scattered papers and a coffee mug, metal folding desk
```

### 3-4. 司令官

```
a middle-aged military commander standing, arms crossed,
black tactical uniform without insignia, headset, short gray hair,
stern tired face, full body, standing pose
```

---

## 4. 装備・強化・変更（`FirstRoom (2)`）

Notion：「ラックにかかってる武器を選択すると、作業台に置かれ強化・装備を選択」。
武器はラックにかけた状態と、作業台に置いた状態の両方で使うので、**武器は1丁ずつ別に生成** します。

| モデル | 大きさ（m） | Unityでの置き場所 |
|---|---|---|
| ウェポンラック（有孔ボード） | 幅3.6 × 高さ1.2 × 厚さ0.1 | `Rack` を差し替え |
| 作業台 | 幅3.6 × 奥行1.3 × 高さ1.0 | `Table` / `Table1` を差し替え |
| 武器（各種） | 下の表 | ラックのフック・作業台の上 |

### 4-1. ウェポンラック

```
a wall-mounted pegboard weapon rack, dark gray metal pegboard with many holes,
empty hooks and brackets for rifles and pistols, a few hanging tools
(wrenches, screwdrivers), no weapons on it
```

### 4-2. 作業台

```
a long heavy-duty gunsmith workbench, thick worn wooden top on a steel frame,
a vise clamp on one end, small parts trays, cleaning rods and a rag,
drawers underneath, no weapons on it
```

### 4-3. 武器

Notion「武器・ウルト」の一覧です。`<WEAPON>` を置き換えて使います。

| 武器 | `<WEAPON>` | 長さ（m） |
|---|---|---|
| アサルトライフル | `modern assault rifle with a short scope and a foregrip` | 0.9 |
| サブマシンガン | `compact submachine gun with a folding stock` | 0.6 |
| スナイパーライフル | `bolt-action sniper rifle with a long scope and a bipod` | 1.2 |
| ショットガン | `pump-action combat shotgun` | 1.0 |
| ピストル | `modern semi-automatic pistol` | 0.2 |
| 近接武器（剣） | `military combat machete with a black blade` | 0.7 |

```
a single <WEAPON>, matte black and dark olive, realistic proportions,
side view, no attachments other than described, worn finish
```

> プレイヤーが持つ武器（`Models/Player/p_rifle.fbx` / `p_pistol.fbx`）がすでにある場合は、それを並べるだけでもよい。

### 4-4. 待機中のプレイヤー

プレイヤーは既存のリグ付きアバター（`Models/Player`）をそのまま置くので、生成は不要です。

---

## 5. 武器・作成・研究（`FirstRoom (3)`）

研究室だけは **不気味さ** を出します。共通のスタイル指定の `muted olive drab, dark gray and khaki palette` を、
`sterile white and steel, stained with grime, sickly green accents` に置き換えて使ってください。

| モデル | 大きさ（m） | 備考 |
|---|---|---|
| 標本カプセル | 直径1.4 × 高さ3 | 中の少女は別モデル（5-2） |
| 中に浮かぶ魔法少女の死骸 | 身長1.4 | 半透明の液体越しに見えるのでシルエット重視 |
| 標本ボード | 幅3 × 高さ1.5 | 壁に掛ける |
| 実験台 | 幅2 × 奥行0.8 × 高さ0.9 | フラスコ・試験管つき |
| ホログラム台 | 直径1 × 高さ0.9 | 設計図は Unity 側で光らせて表示 |
| 太いケーブル | 長さ4 | 床を這わせる |
| 研究素材（3種） | 0.2〜0.4 | 手前に並べて、クリックで選ぶ |

### 5-1. 標本カプセル

```
a tall cylindrical specimen containment tank, thick glass cylinder
filled with murky green liquid, heavy steel base and cap with pipes and valves,
thick black cables plugged into the base, laboratory equipment, empty inside
```

### 5-2. 中に浮かぶ魔法少女の死骸

ラフ（kenkyuusitu）の緑のカプセルの中の人影です。誰の死骸かは決まっていないので、シルエットだけにします。

```
the lifeless body of a young girl floating with arms slightly spread,
eyes closed, long hair drifting, simple tattered dress,
pale gray skin with dark cracks, eerie and sad, full body
```

### 5-3. 標本ボード

```
a laboratory wall board with pinned strange specimens,
twisted roots, a cracked wooden doll fragment, a shard of dark meteor rock,
torn ribbon, labeled with blank tags, metal frame
```

> ボードに並べる標本は、倒した魔法少女の素材です（根・木彫りの人形のかけら＝ステージ1、隕石のかけら＝ステージ7）。

### 5-4. 実験台

```
a laboratory bench with a stainless steel top, glass flasks,
test tubes in a rack, a microscope and sample jars, drawers below
```

### 5-5. ホログラム台

```
a round holographic projector table, dark metal base with a glass top plate,
small control panel on the side, emitter lens in the center, no hologram
```

### 5-6. 研究素材（手前に並べて選ぶ3つ）

ラフ（kenkyuusitu）の手前に並んでいる3つです。それぞれ **ガジェット / 武器 / ウルト** の研究に対応します。

ガジェット（グレネードの試作品）：

```
a prototype hand grenade wrapped in tape and wires, small sensor attached,
experimental military gadget
```

武器（魔法少女の素材を組み込んだ拳銃）：

```
a pistol with dark twisting roots growing around the barrel and grip,
faint sickly green veins in the roots, experimental weapon prototype
```

ウルト（対魔法少女兵器の核）：

```
a flat metal device with a small glowing blue screen in the center,
rugged gray casing with screws, experimental anti-magical-girl weapon core
```

---

## 6. Blenderでの整え方

キャラクターのパーツと違って骨に合わせる必要はないので、作業は少なめです。

1. GLB を読み込む
2. **大きさ** を上の表の実寸に合わせる（1 Blender単位 = 1m）
3. **原点を底面の中心** にする（壁掛けのもの＝ラック・ボード・モニター壁は **背面の中心**）
4. 正面が **Blender の -Y**（Unity の +Z）を向くように回転する
5. ポリゴン数の目安：大きい物 20,000 / 小物 5,000 / 武器 8,000 三角形
6. テクスチャ埋め込みの FBX で書き出す（`Tools/blender/process_part.py` と同じ軸設定：Unity の +Y 上 / +Z 前）

> `process_part.py` を拡張して「小物（prop）」の種類を足せば、手順2〜6を自動化できます。必要になったら言ってください。

---

## 7. Unityでの配置

1. 書き出した FBX を `Assets/AMG/Models/Basecamp/` に置く
2. Basecamp シーンの各部屋に置く。仮の箱（`MapTable` / `Rack` / `Table` / `Table1`）を差し替えるときは、**名前を同じにする** か、仮の箱の子に入れる
   - `BasecampMenu` は `MapTable` の子の球をステージの印として探すので、模型の地図に差し替えても **球は `MapTable` の子に残す**
3. 作戦開始・装備・研究でクリックさせたい物には **Collider** を付ける（床・壁の `Plane` はクリックされない）
4. カメラ位置の目印（`CameraFirstPOtison` / `CameaPotision` / `(1)` / `(2)`）は、置いた物がきれいに映るように調整する
