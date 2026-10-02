# ステージ1ボス「植物に埋もれた村の魔法少女」AI生成プロンプト集

ゲーム内のボスは「骨（Transform）をIKで動かし、見た目は骨ごとのパーツ」という作りです。
そのため **1体まるごとではなく、パーツごとに生成** します（球体関節人形として作る）。
スキニング（ウェイト調整）は不要で、生成したパーツを各骨の `VisualSlot` に入れるだけで動きます。

AI生成は英語のほうが安定するので、プロンプトは英語、説明は日本語にしています。

---

## 進め方

1. **デザイン画を作る**（画像生成AI）… 「0. デザイン画」のプロンプト
2. **パーツを3D生成する**（Meshy / Tripo / Rodin など）… 「1. パーツ」のプロンプト
   - できればテキストだけでなく、手順1のデザイン画の該当部分を切り抜いて **Image to 3D** に使うと、パーツ同士の統一感が出ます
3. **Blenderで整える** … 「2. Blenderでの整え方」
4. **Unityで差し替える** … 「3. Unityでの差し替え」

---

## 共通のスタイル指定

すべてのプロンプトの末尾に付ける共通部分です。

```
folk horror, eerie, carved pale birch wood doll, ball-jointed doll construction,
dry withered grass and straw, gnarled dark roots, muted earthy colors,
stylized realistic, game-ready asset, PBR textures, clean topology, low poly,
single object, centered, neutral lighting, plain background
```

共通のネガティブプロンプト（使えるツールのみ）：

```
flowers, petals, bright colors, glossy plastic, cute anime, chibi, armor, weapons, gun,
multiple objects, base, pedestal, ground plane, text, watermark, blurry, extra limbs
```

> 花は「見た目が綺麗すぎて序盤の敵に合わない」ため使わない（Notion STAGE 1 の決定事項）。

---

## 0. デザイン画（画像生成AI用）

### 0-1. フェーズ1：少女（三面図）

```
character design sheet, turnaround, front view, side view, back view, full body,
a creepy wooden doll girl, magical girl silhouette made of carved pale birch wood,
visible ball joints at shoulders, elbows, hips and knees, long thin arms,
expressionless carved face with small black eyes, head slightly tilted,
long hair made of dry grass and thin roots hanging down to the waist,
dress woven from dry grass and vines, frayed straw hem,
dark roots wrapped around the torso, cracks in the wood skin,
village scarecrow and sacred old tree motif, folk horror,
muted earthy palette, beige wood, olive straw, dark brown roots,
plain light background, concept art
```

### 0-2. フェーズ1：倒木が右腕から生えるポーズ

```
concept art, a creepy wooden doll girl raising her right arm,
a thick fallen tree log growing out of her forearm like a club,
roots twisting around the arm where the log grows,
left arm hanging limp and long, head tilted unnaturally,
dry grass hair and dress, folk horror, muted earthy colors,
plain light background, full body
```

### 0-3. フェーズ2：巨木と一体化した怪物

```
concept art, a giant withered tree monster merged with a girl,
massive dark gnarled trunk, dead dark olive canopy,
a pale carved wooden girl face embedded in the bark at the center, eyes hollow,
dry grass hair hanging around the face,
two enormous root arms made of twisting roots, roots crawling on the ground,
folk horror, eerie, muted earthy palette, plain light background, full view
```

---

## 1. パーツ（3D生成用）

表の「大きさ」はゲーム内の実寸（メートル）です。生成後にBlenderでこの大きさに合わせます。
「骨の向き」は、そのパーツの付け根から先端に向かう方向です。

| パーツ | Unityの差し替え先（VisualSlot） | 大きさ（m） | 付け根（原点にする位置） |
|---|---|---|---|
| 頭（髪込み） | `Girl/Hips/Spine/Chest/Head` | 幅0.30 × 高さ0.34（髪は背中側に0.5垂れる） | 首の付け根 |
| 胴（服の上半分込み） | `Girl/Hips/Spine` | 幅0.36 × 高さ0.55 × 奥行0.26 | 腰（胴の下端） |
| 骨盤＋スカート | `Girl/Hips` | 直径0.7 × 高さ0.4 | 腰の中心 |
| 上腕（左右共用） | `Arm*_Upper` | 長さ0.40 × 太さ0.06 | 肩の関節 |
| 前腕（左右共用） | `Arm*_Lower` | 長さ0.40 × 太さ0.05 | 肘の関節 |
| 手（左右共用） | `Arm*_End` | 0.10程度 | 手首 |
| 腿（左右共用） | `Leg*_Upper` | 長さ0.44 × 太さ0.09 | 股関節 |
| すね（左右共用） | `Leg*_Lower` | 長さ0.44 × 太さ0.07 | 膝の関節 |
| 足（左右共用） | `Leg*_End` | 長さ0.20 × 幅0.09 | 足首 |
| 倒木 | `Girl/Log` | 長さ6.0 × 太さ0.32 | 腕から生える側の端 |
| 巨木の胴体 | `TreeForm` | 幹：直径5.5 × 高さ10、樹冠：直径8 | 根元の中心 |
| 巨木の顔 | `TreeForm/Face` | 幅1.6 × 高さ2.0 | 顔の中心 |

### 1-1. 頭

```
a carved pale birch wood doll head, expressionless girl face,
small black glossy eyes, tiny carved mouth line, subtle cracks in the wood,
long hair made of dry grass and thin roots, bangs of straw,
ball-joint socket at the neck, folk horror
```

### 1-2. 胴

```
a carved pale birch wood doll torso of a slender girl,
upper part of a dress woven from dry grass and vines,
ball-joint sockets at the shoulders and waist, cracks in the wood,
dark thin roots wrapped around the chest, folk horror
```

### 1-3. 骨盤＋スカート

```
a doll pelvis with a skirt woven from dry grass and straw,
frayed uneven straw hem hanging down, vines woven in,
ball-joint sockets for the legs, folk horror
```

### 1-4. 上腕・前腕・腿・すね（共通の作り方で4回）

`<PART>` を `upper arm` / `forearm` / `thigh` / `shin` に置き換えて使います。

```
a single ball-jointed doll <PART>, carved pale birch wood,
long and thin, smooth rounded ball joint at one end, socket at the other end,
subtle wood grain and small cracks, folk horror doll part
```

### 1-5. 手

```
a carved pale birch wood doll hand, long thin fingers slightly curled,
ball joint at the wrist, subtle cracks, folk horror doll part
```

### 1-6. 足

```
a carved pale birch wood doll foot, simple rounded shape,
ball joint at the ankle, slightly worn, folk horror doll part
```

### 1-7. 倒木（右腕から生えるハンマー）

```
a thick fallen tree log used as a club, rough dark bark, broken branch stubs,
one end tapers into twisting roots as if growing out of something,
the other end is heavy and splintered, moss patches, folk horror
```

### 1-8. 巨木の胴体（フェーズ2）

顔は別パーツにするので、正面の顔の位置は「えぐれた空洞」にしておくと差し込みやすいです。

```
a giant withered tree trunk monster body, massive gnarled dark bark,
a hollow cavity on the front at mid height, dead sparse dark olive canopy on top,
thick roots spreading at the base, eerie, folk horror
```

### 1-9. 巨木の顔（フェーズ2）

```
a pale carved wooden girl face mask embedded in bark, hollow black eyes,
expressionless, cracked wood, dry grass hair framing the face,
bark edges around the face, eerie, folk horror
```

### 1-10. 瘤（こぶ）

ステージ上の木に生る弱点。今の仕様では差し替え口がないので、使う場合は言ってください（`Knot` に差し替え口を付けます）。

```
a pulsating tree burl, swollen knot of wood on a tree trunk,
dark reddish brown, glowing amber sap seeping from cracks, veins of roots, organic, folk horror
```

### 1-11. 召喚される案山子（ボス戦版）

```
a creepy village scarecrow on a single wooden pole, straw body, cross arms,
sack cloth head with no face, tattered dry grass, folk horror
```

### 1-12. 根の腕・地面の根のテクスチャ（3Dではなく画像）

フェーズ2の根は、コード（`TendrilTube`）が筒状のメッシュを作るので、モデルは不要です。
**繰り返し貼れる樹皮テクスチャ** だけ用意します（画像生成AI、またはCC0素材の ambientCG / Poly Haven）。

```
seamless tileable texture, dark gnarled root bark, twisted fibers, top-down flat view,
even lighting, no shadows, PBR albedo
```

---

## 2. Blenderでの整え方

AI生成モデルは、そのままだと大きさ・向き・原点がバラバラなので、パーツごとに次を行います。

1. **大きさを合わせる**：上の表の実寸に合わせる（Unity側の基準は、メニュー「A・M・G > 選択中のオブジェクトをOBJで書き出し」で今の仮モデルを書き出し、Blenderに読み込んで重ねると分かりやすい。再生中に一時停止して書き出すと、立ちポーズのまま書き出せる）
2. **原点を付け根に置く**：表の「付け根」に原点（Origin）を移動する
3. **向きをそろえる**：腕・脚・倒木は、付け根から先端の方向が「前方」になるようにする。合わなくても、Unity側の `rotationOffset` で直せるので厳密でなくてよい
4. **軽くする**：Decimate（ポリゴン削減）で目安まで落とす
   - 頭・胴・スカート：各3,000〜6,000三角形
   - 腕・脚・手・足：各500〜1,500三角形
   - 倒木：2,000〜4,000三角形
   - 巨木の胴体：8,000〜15,000三角形、顔：2,000〜4,000三角形
5. **テクスチャ**：1,024〜2,048px。髪やスカートの裾の枯れ草は、透過する板ポリゴン（アルファカード）にすると軽くて見栄えがよい
6. **FBXで書き出す**：「Apply Transform」にチェック

---

## 3. Unityでの差し替え

1. FBXを `Assets/AMG/Models/Stage1Boss/` に入れる
2. メニュー「**A・M・G > ボスを本番モデルの差し替えに対応させる**」を一度だけ実行する（各骨に `VisualSlot`、根に `TendrilTube` が付く）
3. ヒエラルキーで差し替え先の骨（例：`Girl/Hips/Spine/Chest/Head`）を選び、`VisualSlot` の **Replacement** にFBXをドラッグする
4. 再生して位置・向き・大きさを `Position Offset` / `Rotation Offset` / `Scale` で合わせる
5. 合ったら、コンポーネントを右クリック → **Copy Component** → 再生を止めて → **Paste Component Values** で保存する

左右共用のパーツ（腕・脚・手・足）は、左右の骨それぞれに同じFBXを入れます。
