using UnityEngine;

namespace AMG
{
    /// FABRIKの関節を滑らかな曲線（Catmull-Rom）でつなぎ、樹皮の筒状メッシュを毎フレーム生成する。
    /// 円柱と球をつないだ仮の見た目を隠し、つなぎ目のない根にする。
    /// UVは周方向がU、長さ方向がV（textureLength メートルごとに1周）なので、樹皮テクスチャを貼ればそのまま使える
    [DefaultExecutionOrder(100)] // TreeRig がチェーンを解いた後に作る
    [RequireComponent(typeof(FabrikChain))]
    public class TendrilTube : MonoBehaviour
    {
        [Tooltip("空なら仮の形状のマテリアルを使う")]
        public Material material;
        [Tooltip("負の値なら仮の形状の太さから自動で決める")]
        public float baseRadius = -1f;
        public float tipRadius = -1f;
        [Tooltip("先端を閉じる円錐の長さ。負の値なら先端の太さ×tipLengthScale")]
        public float tipLength = -1f;
        public float tipLengthScale = 4f;
        public int sides = 10;
        public int subdivisions = 4;      // 関節と関節の間を何分割するか
        public float textureLength = 1.5f;
        public bool hidePlaceholders = true;

        FabrikChain chain;
        Mesh mesh;
        Transform tube;
        Vector3[] points;
        Vector3[] vertices;
        Vector3[] normals;
        Vector2[] uvs;

        void Start()
        {
            chain = GetComponent<FabrikChain>();
            var joints = chain.joints;

            // 太さとマテリアルを仮の形状（関節の球）から引き継ぐ
            var firstKnuckle = joints[0].Find("Knuckle");
            var lastKnuckle = joints[joints.Length - 1].Find("Knuckle");
            if (baseRadius < 0f) baseRadius = firstKnuckle != null ? firstKnuckle.localScale.x / 2.2f : 0.4f;
            if (tipRadius < 0f) tipRadius = lastKnuckle != null ? lastKnuckle.localScale.x / 2.2f : 0.1f;
            if (tipLength < 0f) tipLength = tipRadius * tipLengthScale;
            if (material == null)
            {
                var r = joints[0].GetComponentInChildren<Renderer>(true);
                if (r != null) material = r.sharedMaterial;
            }
            if (hidePlaceholders)
                foreach (var j in joints)
                    foreach (var r in j.GetComponentsInChildren<Renderer>(true)) r.enabled = false;

            var go = new GameObject("Tube");
            go.layer = gameObject.layer;
            tube = go.transform;
            tube.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh = new Mesh { name = name + "_Tube" };
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            mesh.MarkDynamic();

            int rings = (joints.Length - 1) * subdivisions + 1;
            points = new Vector3[rings];
            // 筒の輪 + 先端の円錐（法線を変えるため最後の輪を複製した底面の輪と、頂点の輪）
            vertices = new Vector3[(rings + 2) * (sides + 1)];
            normals = new Vector3[vertices.Length];
            uvs = new Vector2[vertices.Length];
            var tris = new int[rings * sides * 6];
            int t = 0;
            for (int i = 0; i < rings + 1; i++)
            {
                if (i == rings - 1) continue;   // 筒の最後の輪と円錐の底面の輪の間は面を張らない
                for (int j = 0; j < sides; j++)
                {
                    int a = i * (sides + 1) + j, b = a + sides + 1;
                    // 外側が表になる向き（Unityは時計回りが表）
                    tris[t++] = a; tris[t++] = a + 1; tris[t++] = b;
                    tris[t++] = a + 1; tris[t++] = b + 1; tris[t++] = b;
                }
            }
            Rebuild();
            mesh.triangles = tris;
            mesh.RecalculateBounds();
        }

        void LateUpdate()
        {
            if (mesh == null) return;
            Rebuild();
            mesh.RecalculateBounds();
        }

        void Rebuild()
        {
            var joints = chain.joints;
            int n = joints.Length;

            // 関節を通る滑らかな曲線上の点（筒のローカル座標）
            int k = 0;
            for (int i = 0; i < n - 1; i++)
            {
                Vector3 p0 = tube.InverseTransformPoint(joints[Mathf.Max(0, i - 1)].position);
                Vector3 p1 = tube.InverseTransformPoint(joints[i].position);
                Vector3 p2 = tube.InverseTransformPoint(joints[i + 1].position);
                Vector3 p3 = tube.InverseTransformPoint(joints[Mathf.Min(n - 1, i + 2)].position);
                for (int s = 0; s < subdivisions; s++) points[k++] = CatmullRom(p0, p1, p2, p3, s / (float)subdivisions);
            }
            points[k] = tube.InverseTransformPoint(joints[n - 1].position);

            // 平行移動フレームでねじれを抑えながら輪を並べる
            int rings = points.Length;
            Vector3 tangent = (points[1] - points[0]).normalized;
            Vector3 normal = Vector3.Cross(tangent, Mathf.Abs(tangent.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
            float length = 0f;
            for (int i = 0; i < rings; i++)
            {
                if (i > 0)
                {
                    length += Vector3.Distance(points[i], points[i - 1]);
                    Vector3 next = (i < rings - 1 ? points[i + 1] - points[i] : points[i] - points[i - 1]).normalized;
                    normal = Vector3.ProjectOnPlane(normal, next).normalized;
                    if (normal.sqrMagnitude < 1e-6f) normal = Vector3.Cross(next, Vector3.up).normalized;
                    tangent = next;
                }
                Vector3 binormal = Vector3.Cross(tangent, normal);
                float radius = Mathf.Lerp(baseRadius, tipRadius, i / (float)(rings - 1));
                for (int j = 0; j <= sides; j++)
                {
                    float a = j / (float)sides * Mathf.PI * 2f;
                    Vector3 dir = normal * Mathf.Cos(a) + binormal * Mathf.Sin(a);
                    int v = i * (sides + 1) + j;
                    vertices[v] = points[i] + dir * radius;
                    normals[v] = dir;
                    uvs[v] = new Vector2(j / (float)sides, length / textureLength);
                }
            }

            // 先端を円錐で閉じて、筒の裏側が見えないようにする
            {
                Vector3 binormal = Vector3.Cross(tangent, normal);
                Vector3 tipBase = points[rings - 1];
                Vector3 apex = tipBase + tangent * tipLength;
                int baseRing = rings * (sides + 1), apexRing = baseRing + sides + 1;
                for (int j = 0; j <= sides; j++)
                {
                    float a = j / (float)sides * Mathf.PI * 2f;
                    Vector3 dir = normal * Mathf.Cos(a) + binormal * Mathf.Sin(a);
                    // 円錐の側面に垂直な法線（傾きは長さと太さの比で決まる）
                    Vector3 n = (dir * tipLength + tangent * tipRadius).normalized;
                    vertices[baseRing + j] = tipBase + dir * tipRadius;
                    vertices[apexRing + j] = apex;
                    normals[baseRing + j] = n;
                    normals[apexRing + j] = n;
                    uvs[baseRing + j] = new Vector2(j / (float)sides, length / textureLength);
                    uvs[apexRing + j] = new Vector2(j / (float)sides, (length + tipLength) / textureLength);
                }
            }

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
        }

        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3);
        }
    }
}
