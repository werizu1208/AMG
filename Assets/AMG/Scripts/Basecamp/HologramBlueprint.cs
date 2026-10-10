using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AMG
{
    /// 研究室の「ホログラムで動く設計図」。投影機から青いホログラムが浮かび上がり、
    /// 下から組み上がる → 回転しながら部品が分解・組み立てを繰り返す → グリッチで消えて次の設計図へ、をくり返す。
    /// 表示するモデル（FBX・プレハブ）は models に並べる。見た目は AMG/Hologram シェーダー
    /// このオブジェクトの位置が投影機（床側）で、設計図はその上 hoverHeight に浮かぶ
    public class HologramBlueprint : MonoBehaviour
    {
        [Header("表示する設計図（順番に切り替わる）")]
        public GameObject[] models;
        public string[] titles;

        [Header("見た目")]
        public Material hologramMaterial;
        [Tooltip("設計図のいちばん長い辺をこの大きさ（m）にそろえる")]
        public float modelSize = 1.1f;
        public float hoverHeight = 0.75f;
        public float rotateSpeed = 22f;
        [Tooltip("ワイヤーフレーム：この角度より折れている辺だけ線にする")]
        public float featureAngle = 32f;
        public int wireframeMaxTriangles = 80000;

        [Header("動き")]
        public float buildTime = 2f;
        public float showTime = 8f;
        public float glitchTime = 0.4f;
        [Tooltip("部品が分解するときに離れる距離（部品が2つ以上あるモデルだけ）")]
        public float explodeDistance = 0.22f;

        Transform pivot;
        GameObject current;
        readonly List<Renderer> renderers = new List<Renderer>();
        readonly List<(Transform part, Vector3 basePos, Vector3 dir)> parts = new List<(Transform, Vector3, Vector3)>();
        readonly List<(LineRenderer line, Transform anchor, Vector3 labelOffset, TextMesh text)> callouts = new List<(LineRenderer, Transform, Vector3, TextMesh)>();
        readonly List<Transform> rings = new List<Transform>();
        static readonly Dictionary<Mesh, Mesh> wireCache = new Dictionary<Mesh, Mesh>();
        Material surfaceMat, lineMat, beamMat;
        MaterialPropertyBlock block;
        float reveal = 1.05f, glitch;
        TextMesh header;
        Font font;

        void Start()
        {
            if (hologramMaterial == null)
            {
                var shader = Shader.Find("AMG/Hologram");
                if (shader == null)
                {
                    Debug.LogWarning("[A・M・G] ホログラムのシェーダー（AMG/Hologram）が見つかりません。");
                    enabled = false;
                    return;
                }
                hologramMaterial = new Material(shader);
            }
            surfaceMat = hologramMaterial;
            lineMat = new Material(hologramMaterial);
            lineMat.SetFloat("_IsLine", 1f);
            beamMat = new Material(hologramMaterial);
            beamMat.SetFloat("_Alpha", 0.22f);
            beamMat.SetFloat("_GridAlpha", 0f);
            beamMat.SetFloat("_FillAlpha", 0.02f);
            block = new MaterialPropertyBlock();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            pivot = new GameObject("BlueprintPivot").transform;
            pivot.SetParent(transform, false);
            pivot.localPosition = Vector3.up * hoverHeight;

            BuildProjector();
            if (models != null && models.Length > 0) StartCoroutine(Loop());
        }

        // ---------- 投影機（光の円錐・回転するリング・見出し） ----------

        void BuildProjector()
        {
            // 投影機から設計図へ広がる光の円錐
            var beam = new GameObject("Beam");
            beam.transform.SetParent(transform, false);
            beam.AddComponent<MeshFilter>().sharedMesh = ConeMesh(0.12f, modelSize * 0.55f, hoverHeight - modelSize * 0.25f, 32);
            beam.AddComponent<MeshRenderer>().sharedMaterial = beamMat;

            // 床の円と、設計図の下で逆向きに回るリング（目盛りつき）
            rings.Add(Ring("Ring_Base", 0.02f, 0.18f, 48, 0));
            rings.Add(Ring("Ring_Inner", hoverHeight - modelSize * 0.45f, modelSize * 0.48f, 64, 24));
            rings.Add(Ring("Ring_Outer", hoverHeight - modelSize * 0.42f, modelSize * 0.62f, 72, 36));

            header = Label("Header", "", 0.055f);
            header.transform.SetParent(transform, false);
            header.transform.localPosition = Vector3.up * (hoverHeight + modelSize * 0.62f);
        }

        Transform Ring(string name, float height, float radius, int segments, int ticks)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * height;
            var lr = NewLine(go, 0.006f);
            lr.loop = true;
            lr.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius);
            }
            // 目盛り（内側へ短い線）
            for (int i = 0; i < ticks; i++)
            {
                float a = i * Mathf.PI * 2f / ticks;
                var t = new GameObject("Tick");
                t.transform.SetParent(go.transform, false);
                var tl = NewLine(t, 0.004f);
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                tl.positionCount = 2;
                tl.SetPosition(0, d * radius);
                tl.SetPosition(1, d * radius * (i % 4 == 0 ? 0.88f : 0.94f));
            }
            return go.transform;
        }

        LineRenderer NewLine(GameObject go, float width)
        {
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.sharedMaterial = lineMat;
            lr.widthMultiplier = width;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return lr;
        }

        TextMesh Label(string name, string text, float size)
        {
            var go = new GameObject(name);
            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.fontSize = 64;
            tm.characterSize = size * 0.15f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = new Color(0.55f, 0.9f, 1f, 0.9f);
            tm.text = text;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            return tm;
        }

        static Mesh ConeMesh(float bottomRadius, float topRadius, float height, int segments)
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                verts.Add(d * bottomRadius);
                verts.Add(d * topRadius + Vector3.up * height);
                normals.Add(d);
                normals.Add(d);
            }
            for (int i = 0; i < segments; i++)
            {
                int a = i * 2;
                tris.AddRange(new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2 });
            }
            var m = new Mesh { name = "HologramBeam" };
            m.SetVertices(verts);
            m.SetNormals(normals);
            m.SetTriangles(tris, 0);
            return m;
        }

        // ---------- 設計図の切り替え ----------

        IEnumerator Loop()
        {
            int index = 0;
            while (true)
            {
                var model = models[index % models.Length];
                if (model != null)
                {
                    Show(model, titles != null && index % models.Length < titles.Length ? titles[index % models.Length] : model.name);

                    // 下から組み上がる
                    for (float t = 0f; t < buildTime; t += Time.deltaTime)
                    {
                        reveal = Mathf.Lerp(0f, 1.05f, t / buildTime);
                        yield return null;
                    }
                    reveal = 1.05f;

                    // 回転しながら、分解と組み立てをくり返す
                    for (float t = 0f; t < showTime; t += Time.deltaTime)
                    {
                        float k = Mathf.Sin(Mathf.Clamp01(t / showTime) * Mathf.PI);   // 0 → 1 → 0
                        Explode(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k * 1.6f - 0.3f)));
                        yield return null;
                    }
                    Explode(0f);

                    // グリッチで消える
                    for (float t = 0f; t < glitchTime; t += Time.deltaTime)
                    {
                        glitch = t / glitchTime;
                        yield return null;
                    }
                    glitch = 0f;
                    Clear();
                }
                index++;
                yield return null;
            }
        }

        void Show(GameObject source, string title)
        {
            Clear();
            current = Instantiate(source, pivot);
            current.name = "Blueprint_" + source.name;
            current.transform.localPosition = Vector3.zero;
            current.transform.localRotation = Quaternion.identity;

            // 見た目だけ残す（スクリプト・当たり判定・アニメーションは外し、スキンメッシュは形を焼き付ける）
            foreach (var smr in current.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var baked = new Mesh();
                smr.BakeMesh(baked);
                var go = smr.gameObject;
                Object.DestroyImmediate(smr);
                go.AddComponent<MeshFilter>().sharedMesh = baked;
                go.AddComponent<MeshRenderer>();
            }
            foreach (var c in current.GetComponentsInChildren<Component>(true))
                if (c is MonoBehaviour || c is Collider || c is Animator || c is Rigidbody) Object.Destroy(c);

            // 大きさをそろえ、中心を浮かぶ位置に合わせる
            var bounds = WorldBounds(current);
            float longest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (longest > 0.0001f) current.transform.localScale *= modelSize / longest;
            bounds = WorldBounds(current);
            current.transform.position += pivot.position - bounds.center;
            bounds = WorldBounds(current);

            // 材質をホログラムにし、ワイヤーフレームを重ねる
            var filters = current.GetComponentsInChildren<MeshFilter>(true);
            // 三角形の数（Read/Write がオフのモデルでも数えられる方法で）
            int totalTris = 0;
            foreach (var f in filters)
                if (f.sharedMesh != null)
                    for (int s = 0; s < f.sharedMesh.subMeshCount; s++) totalTris += (int)f.sharedMesh.GetIndexCount(s) / 3;
            foreach (var f in filters)
            {
                var r = f.GetComponent<MeshRenderer>();
                if (r == null || f.sharedMesh == null) continue;
                var mats = new Material[Mathf.Max(1, f.sharedMesh.subMeshCount)];
                for (int i = 0; i < mats.Length; i++) mats[i] = surfaceMat;
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                renderers.Add(r);

                // ワイヤーフレームは形のデータを読むので、モデルの Read/Write がオンのときだけ（配置メニューでオンにする）
                if (totalTris <= wireframeMaxTriangles && f.sharedMesh.isReadable)
                {
                    var wire = new GameObject("Wireframe");
                    wire.transform.SetParent(f.transform, false);
                    wire.AddComponent<MeshFilter>().sharedMesh = Wireframe(f.sharedMesh);
                    var wr = wire.AddComponent<MeshRenderer>();
                    wr.sharedMaterial = lineMat;
                    wr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderers.Add(wr);
                }

                // 分解するときの向き：部品の中心から、全体の中心の外側へ
                Vector3 dir = r.bounds.center - bounds.center;
                parts.Add((f.transform, f.transform.localPosition, f.transform.parent != null ? f.transform.parent.InverseTransformDirection(dir) : dir));
            }
            if (parts.Count < 2) parts.Clear();   // 部品が1つなら分解しない

            header.text = $"BLUEPRINT  //  {title.ToUpper()}";
            BuildCallouts(bounds);
        }

        void Clear()
        {
            foreach (var c in callouts)
            {
                if (c.line != null) Destroy(c.line.gameObject);
                if (c.text != null) Destroy(c.text.gameObject);
            }
            callouts.Clear();
            renderers.Clear();
            parts.Clear();
            if (current != null) Destroy(current);
            current = null;
        }

        void Explode(float amount)
        {
            foreach (var p in parts)
                if (p.part != null) p.part.localPosition = p.basePos + p.dir.normalized * (explodeDistance * amount / Mathf.Max(0.0001f, current.transform.lossyScale.x));
        }

        static Bounds WorldBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        // ---------- 注釈（部品を指す線と数値） ----------

        static readonly string[] CalloutTexts =
        {
            "STRUCT INTEGRITY  98.2%", "ANTI-MG ALLOY  Lv.3", "OUTPUT  4.7 kJ", "LOAD  0.82 / 1.00",
            "RESONANCE  -12 dB", "PROTOTYPE  REV.07", "THERMAL  312 K", "SIGNATURE  MASKED",
        };

        void BuildCallouts(Bounds b)
        {
            var targets = new List<Transform>();
            foreach (var r in renderers)
                if (r != null && r.name != "Wireframe") targets.Add(r.transform);
            if (targets.Count == 0) return;

            for (int i = 0; i < 3; i++)
            {
                var anchor = new GameObject("CalloutAnchor").transform;
                var target = targets[i % targets.Count];
                anchor.SetParent(target, false);
                // 部品の表面あたり（境界の中からばらけた点）を指す
                var rb = target.GetComponent<Renderer>().bounds;
                Vector3 p = rb.center + Vector3.Scale(rb.extents * 0.7f, new Vector3(i == 1 ? -1f : 1f, i == 2 ? -0.6f : 0.5f, 0f));
                anchor.position = p;

                var lineGo = new GameObject("Callout");
                lineGo.transform.SetParent(transform, false);
                var lr = NewLine(lineGo, 0.004f);
                lr.useWorldSpace = true;
                lr.positionCount = 3;

                float side = i == 1 ? -1f : 1f;
                Vector3 offset = new Vector3(side * modelSize * 0.55f, (0.25f - i * 0.22f) * modelSize, 0f);
                var text = Label("CalloutText", CalloutTexts[Random.Range(0, CalloutTexts.Length)], 0.035f);
                text.transform.SetParent(transform, false);
                text.anchor = side > 0 ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
                callouts.Add((lr, anchor, offset, text));
            }
        }

        // ---------- 毎フレーム ----------

        void LateUpdate()
        {
            if (pivot == null) return;
            pivot.Rotate(0f, rotateSpeed * Time.deltaTime, 0f, Space.Self);
            for (int i = 0; i < rings.Count; i++)
                rings[i].Rotate(0f, (i % 2 == 0 ? 1f : -1f) * rotateSpeed * (0.6f + i * 0.4f) * Time.deltaTime, 0f, Space.Self);

            // 組み上がり・グリッチを、設計図の高さの範囲で渡す
            float minY = pivot.position.y - modelSize * 0.5f, maxY = pivot.position.y + modelSize * 0.5f;
            foreach (var r in renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetFloat("_Reveal", reveal);
                block.SetFloat("_Glitch", glitch);
                block.SetFloat("_BoundsMinY", minY);
                block.SetFloat("_BoundsMaxY", maxY);
                r.SetPropertyBlock(block);
            }

            // 注釈の線は、部品から折れ曲がってラベルへ。ラベルはカメラの方を向く
            var cam = Camera.main;
            Vector3 right = cam != null ? cam.transform.right : transform.right;
            bool visible = reveal >= 1f && glitch <= 0f;
            foreach (var c in callouts)
            {
                if (c.anchor == null) continue;
                Vector3 a = c.anchor.position;
                Vector3 label = pivot.position + right * c.labelOffset.x + Vector3.up * c.labelOffset.y;
                Vector3 elbow = new Vector3(Mathf.Lerp(a.x, label.x, 0.35f), label.y, Mathf.Lerp(a.z, label.z, 0.35f));
                c.line.enabled = visible;
                c.line.SetPosition(0, a);
                c.line.SetPosition(1, elbow);
                c.line.SetPosition(2, label);
                c.text.gameObject.SetActive(visible);
                c.text.transform.position = label + right * Mathf.Sign(c.labelOffset.x) * 0.03f;
                if (cam != null) c.text.transform.rotation = Quaternion.LookRotation(c.text.transform.position - cam.transform.position);
            }
            if (header != null && cam != null)
                header.transform.rotation = Quaternion.LookRotation(header.transform.position - cam.transform.position);
        }

        // ---------- ワイヤーフレーム（折れている辺と縁だけを線にする） ----------

        Mesh Wireframe(Mesh source)
        {
            if (wireCache.TryGetValue(source, out var cached)) return cached;
            var verts = source.vertices;
            var tris = source.triangles;

            // 位置が同じ頂点を1つにまとめる（UVの継ぎ目で分かれている頂点を同じ辺として扱う）
            var weld = new Dictionary<Vector3Int, int>();
            var welded = new int[verts.Length];
            var positions = new List<Vector3>();
            for (int i = 0; i < verts.Length; i++)
            {
                var key = Vector3Int.RoundToInt(verts[i] * 10000f);
                if (!weld.TryGetValue(key, out int id))
                {
                    id = positions.Count;
                    weld[key] = id;
                    positions.Add(verts[i]);
                }
                welded[i] = id;
            }

            var edgeFaces = new Dictionary<long, (Vector3 n1, int count, Vector3 n2)>();
            for (int t = 0; t + 2 < tris.Length; t += 3)
            {
                int a = welded[tris[t]], b = welded[tris[t + 1]], c = welded[tris[t + 2]];
                Vector3 n = Vector3.Cross(positions[b] - positions[a], positions[c] - positions[a]).normalized;
                AddEdge(edgeFaces, a, b, n);
                AddEdge(edgeFaces, b, c, n);
                AddEdge(edgeFaces, c, a, n);
            }

            var indices = new List<int>();
            float cosLimit = Mathf.Cos(featureAngle * Mathf.Deg2Rad);
            foreach (var kv in edgeFaces)
            {
                var e = kv.Value;
                bool feature = e.count == 1 || (e.count == 2 && Vector3.Dot(e.n1, e.n2) < cosLimit);
                if (!feature) continue;
                indices.Add((int)(kv.Key >> 32));
                indices.Add((int)(kv.Key & 0xffffffff));
            }

            var mesh = new Mesh { name = source.name + "_Wire" };
            if (positions.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(positions);
            mesh.SetIndices(indices, MeshTopology.Lines, 0);
            wireCache[source] = mesh;
            return mesh;
        }

        static void AddEdge(Dictionary<long, (Vector3 n1, int count, Vector3 n2)> edges, int a, int b, Vector3 n)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (edges.TryGetValue(key, out var e))
                edges[key] = (e.n1, e.count + 1, e.count == 1 ? n : e.n2);
            else
                edges[key] = (n, 1, Vector3.zero);
        }
    }
}
