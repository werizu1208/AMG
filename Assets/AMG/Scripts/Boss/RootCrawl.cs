using System.Collections.Generic;
using UnityEngine;

namespace AMG
{
    /// 「地面から突き出す根」の予兆演出。ボスの足元から目標地点へ、根が土を割りながら這い進む。
    /// 本体の根と同じ樹皮の、つなぎ目のない筒状メッシュで作る（先端は細く尖る）。
    /// 見た目だけでダメージ判定はない（コライダーなし）
    public class RootCrawl : MonoBehaviour
    {
        const int Sides = 8;
        const float PointSpacing = 0.35f;
        const float TipLength = 0.8f;      // 先端が細くなっていく長さ
        const float TextureLength = 1.5f;  // 樹皮テクスチャが1回繰り返す長さ（TendrilTube と同じ）

        Vector3[] path;
        float[] distance;
        float totalLength;
        float thickBase, thickTip;
        float duration;
        float elapsed;

        Mesh mesh;
        Vector3[] vertices;
        Vector3[] normals;
        Vector2[] uvs;

        /// duration 秒かけて from から to まで這い進む
        public static RootCrawl Create(Vector3 from, Vector3 to, float duration, float thickBase, float thickTip, float waveAmplitude, Material mat)
        {
            var go = new GameObject("RootCrawl");
            var crawl = go.AddComponent<RootCrawl>();
            crawl.Build(from, to, duration, thickBase, thickTip, waveAmplitude, mat);
            return crawl;
        }

        void Build(Vector3 from, Vector3 to, float duration, float thickBase, float thickTip, float waveAmplitude, Material mat)
        {
            this.duration = Mathf.Max(0.05f, duration);
            this.thickBase = thickBase;
            this.thickTip = thickTip;
            from.y = 0f;
            to.y = 0f;
            Vector3 d = to - from;
            float dist = d.magnitude;
            if (dist < 0.5f) return;

            // 両端ほど揺れの小さい、蛇行した経路（地面に沿わせ、半分ほど埋める）
            Vector3 dir = d / dist;
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            int n = Mathf.Max(3, Mathf.CeilToInt(dist / PointSpacing));
            float phase = Random.Range(0f, Mathf.PI * 2f);
            float waves = Random.Range(1.5f, 2.5f);
            path = new Vector3[n + 1];
            distance = new float[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                float wave = Mathf.Sin(t * waves * Mathf.PI * 2f + phase) * waveAmplitude * Mathf.Sin(t * Mathf.PI);
                float radius = Mathf.Lerp(thickBase, thickTip, t) * 0.5f;
                path[i] = Ground(from + dir * dist * t + side * wave) + Vector3.up * radius * 0.35f;
                if (i > 0) distance[i] = distance[i - 1] + Vector3.Distance(path[i], path[i - 1]);
            }
            totalLength = distance[n];

            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh = new Mesh { name = "RootCrawl" };
            var mr = gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mesh.MarkDynamic();

            int rings = path.Length;
            vertices = new Vector3[rings * (Sides + 1)];
            normals = new Vector3[vertices.Length];
            uvs = new Vector2[vertices.Length];
            var tris = new List<int>((rings - 1) * Sides * 6);
            for (int i = 0; i < rings - 1; i++)
            {
                for (int j = 0; j < Sides; j++)
                {
                    int a = i * (Sides + 1) + j, b = a + Sides + 1;
                    tris.Add(a); tris.Add(a + 1); tris.Add(b);
                    tris.Add(a + 1); tris.Add(b + 1); tris.Add(b);
                }
            }
            UpdateMesh(0f);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
        }

        static Vector3 Ground(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out var hit, 6f, Layers.EnvironmentMask, QueryTriggerInteraction.Ignore))
                return hit.point;
            p.y = 0f;
            return p;
        }

        void Update()
        {
            if (mesh == null) return;
            elapsed += Time.deltaTime;
            float grown = Mathf.Pow(Mathf.Clamp01(elapsed / duration), 0.85f) * totalLength;
            UpdateMesh(grown);
            mesh.RecalculateBounds();
        }

        /// grown（根元からの長さ）まで伸びた根の形にする。先は先端に向かって細く尖る
        void UpdateMesh(float grown)
        {
            int rings = path.Length;
            Vector3 head = PointAt(grown);
            Vector3 normal = Vector3.up;
            for (int i = 0; i < rings; i++)
            {
                Vector3 tangent = (i < rings - 1 ? path[i + 1] - path[i] : path[i] - path[i - 1]).normalized;
                normal = Vector3.ProjectOnPlane(i == 0 ? Vector3.up : normal, tangent).normalized;
                if (normal.sqrMagnitude < 1e-6f) normal = Vector3.up;
                Vector3 binormal = Vector3.Cross(tangent, normal);

                float s = distance[i];
                bool beyond = s > grown;
                Vector3 center = beyond ? head : path[i];
                float u = totalLength > 0f ? s / totalLength : 0f;
                float radius = Mathf.Lerp(thickBase, thickTip, u) * 0.5f;
                radius *= beyond ? 0f : Mathf.Clamp01((grown - s) / TipLength);   // 先端ほど細く

                for (int j = 0; j <= Sides; j++)
                {
                    float a = j / (float)Sides * Mathf.PI * 2f;
                    Vector3 dir = normal * Mathf.Cos(a) + binormal * Mathf.Sin(a);
                    int v = i * (Sides + 1) + j;
                    vertices[v] = center + dir * radius;
                    normals[v] = dir;
                    uvs[v] = new Vector2(j / (float)Sides, s / TextureLength);
                }
            }
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
        }

        Vector3 PointAt(float s)
        {
            if (s <= 0f) return path[0];
            for (int i = 1; i < path.Length; i++)
            {
                if (distance[i] >= s)
                    return Vector3.Lerp(path[i - 1], path[i], Mathf.InverseLerp(distance[i - 1], distance[i], s));
            }
            return path[path.Length - 1];
        }

        void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }

        /// 地面から突き出す1本の根。根元が太く先が尖り、bend の方向へ少し曲がる（地面の下から生えて見えるよう根元は埋める）
        public static GameObject CreateSpike(Transform parent, Vector3 localBase, float height, float radius, Vector3 bend, Material mat)
        {
            const int rings = 8;
            var go = new GameObject("RootSpike");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localBase;
            var spikeMesh = new Mesh { name = "RootSpike" };
            go.AddComponent<MeshFilter>().sharedMesh = spikeMesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            go.AddComponent<MeshAutoDestroy>().mesh = spikeMesh;

            // 根元(地下)→先端の二次曲線
            Vector3 p0 = Vector3.down * radius, p2 = Vector3.up * height + bend, p1 = Vector3.up * height * 0.5f;
            var verts = new Vector3[rings * (Sides + 1)];
            var norms = new Vector3[verts.Length];
            var uv = new Vector2[verts.Length];
            var tris = new List<int>();
            float length = 0f;
            Vector3 prev = p0;
            for (int i = 0; i < rings; i++)
            {
                float t = i / (float)(rings - 1);
                Vector3 c = (1 - t) * (1 - t) * p0 + 2 * (1 - t) * t * p1 + t * t * p2;
                Vector3 tangent = (2 * (1 - t) * (p1 - p0) + 2 * t * (p2 - p1)).normalized;
                length += Vector3.Distance(c, prev);
                prev = c;
                Vector3 n = Vector3.ProjectOnPlane(Vector3.forward, tangent).normalized;
                Vector3 b = Vector3.Cross(tangent, n);
                float r = radius * (1f - t) * (1f - t * 0.3f);
                for (int j = 0; j <= Sides; j++)
                {
                    float a = j / (float)Sides * Mathf.PI * 2f;
                    Vector3 dir = n * Mathf.Cos(a) + b * Mathf.Sin(a);
                    int v = i * (Sides + 1) + j;
                    verts[v] = c + dir * r;
                    norms[v] = dir;
                    uv[v] = new Vector2(j / (float)Sides, length / TextureLength);
                    if (i < rings - 1 && j < Sides)
                    {
                        int bv = v + Sides + 1;
                        tris.Add(v); tris.Add(v + 1); tris.Add(bv);
                        tris.Add(v + 1); tris.Add(bv + 1); tris.Add(bv);
                    }
                }
            }
            spikeMesh.vertices = verts;
            spikeMesh.normals = norms;
            spikeMesh.uv = uv;
            spikeMesh.SetTriangles(tris, 0);
            spikeMesh.RecalculateBounds();
            return go;
        }
    }

    /// 実行中に作ったメッシュを、オブジェクトと一緒に破棄する
    public class MeshAutoDestroy : MonoBehaviour
    {
        public Mesh mesh;

        void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }
    }
}
