using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AMG
{
    /// ステージ7（原初の魔法少女）の演出と、出現中の雑魚・隕石の管理
    public static class Stage7Fx
    {
        /// 出現中の雑魚・隕石。ボス撃破時にまとめて消す
        public static readonly List<GameObject> Spawned = new List<GameObject>();

        public static void ClearSpawned()
        {
            foreach (var go in Spawned)
                if (go != null) Object.Destroy(go);
            Spawned.Clear();
        }

        /// 流れる光の尾（彗星の軌道）
        public static TrailRenderer AddTrail(GameObject go, Material mat, float width, float time)
        {
            var trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = mat;
            trail.time = time;
            trail.startWidth = width;
            trail.endWidth = 0f;
            trail.minVertexDistance = 0.1f;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            return trail;
        }

        /// 地面（または空中）に広がって消える魔法陣
        public static GameObject MagicCircle(Vector3 center, float radius, Material mat, float life, Quaternion? rotation = null)
        {
            var go = Prim.Create(PrimitiveType.Cylinder, "MagicCircle", null, center, new Vector3(radius * 2f, 0.01f, radius * 2f), mat, false, rotation);
            if (life > 0f && VfxLibrary.I != null) VfxLibrary.I.StartCoroutine(SpinAndFade(go, life));
            return go;
        }

        static IEnumerator SpinAndFade(GameObject go, float life)
        {
            Vector3 baseScale = go.transform.localScale;
            for (float t = 0f; t < life && go != null; t += Time.deltaTime)
            {
                float k = t / life;
                go.transform.Rotate(0f, 180f * Time.deltaTime, 0f, Space.Self);
                go.transform.localScale = new Vector3(baseScale.x * (1f + 0.15f * k), baseScale.y, baseScale.z * (1f + 0.15f * k));
                yield return null;
            }
            if (go != null) Object.Destroy(go);
        }

        /// 星の形（球の芯＋十字の光）
        public static GameObject Star(string name, Vector3 pos, float size, Material mat)
        {
            var root = new GameObject(name);
            root.transform.position = pos;
            Prim.Create(PrimitiveType.Sphere, "Core", root.transform, Vector3.zero, Vector3.one * size, mat);
            Prim.Create(PrimitiveType.Cube, "RayA", root.transform, Vector3.zero, new Vector3(size * 2.2f, size * 0.25f, size * 0.25f), mat, false, Quaternion.Euler(0f, 0f, 45f));
            Prim.Create(PrimitiveType.Cube, "RayB", root.transform, Vector3.zero, new Vector3(size * 2.2f, size * 0.25f, size * 0.25f), mat, false, Quaternion.Euler(0f, 0f, -45f));
            return root;
        }

        public static float GroundHeight(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out var hit, 200f, Layers.EnvironmentMask, QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return 0f;
        }

        /// 範囲内のプレイヤーにダメージと吹き飛ばし
        public static void Blast(Vector3 center, float radius, float damage, float knockback, float knockUp)
        {
            var player = PlayerHealth.I;
            if (player == null || !player.IsAlive) return;
            Vector3 d = player.transform.position - center;
            d.y = 0f;
            if (d.magnitude > radius || player.transform.position.y > center.y + 3f) return;
            if (player.TakeHit(damage) && knockback > 0f)
            {
                Vector3 away = d.sqrMagnitude > 0.01f ? d.normalized : Vector3.forward;
                player.GetComponent<PlayerController>().Knockback(away * knockback + Vector3.up * knockUp);
            }
        }
    }
}
