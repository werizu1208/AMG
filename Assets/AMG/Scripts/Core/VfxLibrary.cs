using System.Collections;
using UnityEngine;

namespace AMG
{
    /// 実行中に生成するエフェクトや予兆表示で使うマテリアル置き場と、簡易エフェクト
    public class VfxLibrary : MonoBehaviour
    {
        public static VfxLibrary I { get; private set; }

        public Material telegraph;
        public Material spike;
        public Material tracer;
        public Material leaf;
        public Material explosion;
        public Material rootLine;
        public Material airLayer;
        public Material overdrive;
        public Material wither;
        public Material scarecrow;
        public Material scarecrowHead;
        public Material grenade;

        void Awake()
        {
            I = this;
        }

        public static void Tracer(Vector3 from, Vector3 to)
        {
            if (I != null) I.StartCoroutine(I.LineRoutine(from, to, I.tracer, 0.04f, 0.06f));
        }

        public static void RootLine(Vector3 from, Vector3 to)
        {
            if (I != null) I.StartCoroutine(I.LineRoutine(from, to, I.rootLine, 0.5f, 1.2f));
        }

        public static void Burst(Vector3 pos, float radius, Material mat, float life)
        {
            if (I != null) I.StartCoroutine(I.BurstRoutine(pos, radius, mat, life));
        }

        IEnumerator LineRoutine(Vector3 a, Vector3 b, Material mat, float width, float life)
        {
            var go = new GameObject("Line");
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = mat;
            lr.positionCount = 2;
            lr.SetPosition(0, a);
            lr.SetPosition(1, b);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            for (float t = 0f; t < life; t += Time.deltaTime)
            {
                float w = width * (1f - t / life);
                lr.startWidth = w;
                lr.endWidth = w;
                yield return null;
            }
            Destroy(go);
        }

        IEnumerator BurstRoutine(Vector3 pos, float radius, Material mat, float life)
        {
            var go = Prim.Create(PrimitiveType.Sphere, "Burst", null, pos, Vector3.one * radius, mat);
            var rend = go.GetComponent<Renderer>();
            var instance = rend.material;
            Color baseColor = instance.HasProperty("_BaseColor") ? instance.GetColor("_BaseColor") : Color.white;
            for (float t = 0f; t < life; t += Time.deltaTime)
            {
                float k = t / life;
                go.transform.localScale = Vector3.one * Mathf.Lerp(radius * 0.6f, radius * 2f, k);
                Color c = baseColor;
                c.a = baseColor.a * (1f - k);
                instance.SetColor("_BaseColor", c);
                yield return null;
            }
            Destroy(instance);
            Destroy(go);
        }
    }
}
