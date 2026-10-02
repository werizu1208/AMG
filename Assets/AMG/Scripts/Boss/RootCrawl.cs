using UnityEngine;

namespace AMG
{
    /// 「地面から突き出す根」の予兆演出。ボスの足元から目標地点へ、根が土を割りながら這い進む。
    /// 見た目だけでダメージ判定はない（コライダーなし）
    public class RootCrawl : MonoBehaviour
    {
        const float SegmentLength = 0.7f;
        const float RiseTime = 0.15f;

        struct Segment
        {
            public Transform transform;
            public Vector3 position;
            public float revealTime;
        }

        Segment[] segments = new Segment[0];
        float elapsed;

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
            from.y = 0f;
            to.y = 0f;
            Vector3 d = to - from;
            float dist = d.magnitude;
            if (dist < 0.5f) return;

            Vector3 dir = d / dist;
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            int n = Mathf.Max(2, Mathf.CeilToInt(dist / SegmentLength));
            float phase = Random.Range(0f, Mathf.PI * 2f);
            float waves = Random.Range(1.5f, 2.5f);

            // 両端ほど揺れの小さい、蛇行した経路
            var points = new Vector3[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                float wave = Mathf.Sin(t * waves * Mathf.PI * 2f + phase) * waveAmplitude * Mathf.Sin(t * Mathf.PI);
                points[i] = Ground(from + dir * dist * t + side * wave);
            }

            segments = new Segment[n];
            for (int i = 0; i < n; i++)
            {
                Vector3 a = points[i];
                Vector3 b = points[i + 1];
                Vector3 seg = b - a;
                float thick = Mathf.Lerp(thickBase, thickTip, i / (float)(n - 1));

                // 半分ほど地面に埋まった状態
                var root = new GameObject("Segment").transform;
                root.SetParent(transform, false);
                root.position = (a + b) * 0.5f + Vector3.up * thick * 0.2f;
                Prim.Create(PrimitiveType.Cylinder, "Root", root, Vector3.zero,
                    new Vector3(thick, seg.magnitude * 0.5f + thick * 0.3f, thick), mat, false, Quaternion.FromToRotation(Vector3.up, seg));
                Prim.Create(PrimitiveType.Sphere, "Knuckle", root, a - (a + b) * 0.5f, Vector3.one * thick * 1.15f, mat);
                root.gameObject.SetActive(false);

                segments[i] = new Segment
                {
                    transform = root,
                    position = root.position,
                    revealTime = duration * Mathf.Pow(i / (float)n, 0.85f),
                };
            }
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
            elapsed += Time.deltaTime;
            foreach (var s in segments)
            {
                if (elapsed < s.revealTime) continue;
                if (!s.transform.gameObject.activeSelf) s.transform.gameObject.SetActive(true);

                // 土の中から盛り上がるように現れる
                float k = Mathf.Clamp01((elapsed - s.revealTime) / RiseTime);
                s.transform.position = s.position + Vector3.down * (1f - k) * 0.6f;
                s.transform.localScale = Vector3.one * Mathf.Lerp(0.3f, 1f, k);
            }
        }
    }
}
