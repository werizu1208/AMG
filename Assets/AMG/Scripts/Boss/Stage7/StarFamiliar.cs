using UnityEngine;

namespace AMG
{
    /// 使い魔：キラキラした星。空中で一瞬きらめいてから、プレイヤーのいた地面へ突撃する。
    /// 地面に落ちるか、プレイヤーに当たるか、撃たれると消滅する
    public class StarFamiliar : MonoBehaviour, IDamageable
    {
        public float hp = 12f;
        public float damage = 8f;
        public float hoverTime = 0.7f;
        public float diveSpeed = 20f;
        public float hitRadius = 1.1f;
        public float lifetime = 7f;

        public bool IsAlive => hp > 0f;

        Vector3 diveDir;
        float age;
        bool diving;
        Vector3 spin;

        public static StarFamiliar Spawn(Vector3 pos, Material mat, Material trail)
        {
            var go = Stage7Fx.Star("StarFamiliar", pos, 0.45f, mat);
            Prim.SetLayerRecursive(go, Layers.Enemy);
            var col = go.AddComponent<SphereCollider>();
            col.radius = 0.4f;
            Stage7Fx.AddTrail(go, trail, 0.35f, 0.35f);
            Stage7Fx.Spawned.Add(go);
            var f = go.AddComponent<StarFamiliar>();
            f.spin = Random.onUnitSphere * 360f;
            return f;
        }

        void Update()
        {
            if (!GameManager.IsPlaying) return;
            float dt = Time.deltaTime;
            age += dt;
            transform.Rotate(spin * dt, Space.World);

            var player = PlayerHealth.I;
            if (!diving)
            {
                // 空中でふわりと揺れて、きらめく
                transform.position += Vector3.up * Mathf.Sin(age * 12f) * 0.6f * dt;
                if (age >= hoverTime && player != null)
                {
                    diving = true;
                    // プレイヤーの今いる場所へ（少しだけ先読み）
                    Vector3 target = player.Center + Vector3.down * 0.4f;
                    diveDir = (target - transform.position).normalized;
                }
                return;
            }

            transform.position += diveDir * diveSpeed * dt;
            if (player != null && player.IsAlive && Vector3.Distance(transform.position, player.Center) <= hitRadius)
            {
                player.TakeHit(damage);
                Vanish();
                return;
            }
            if (transform.position.y <= Stage7Fx.GroundHeight(transform.position) + 0.2f || age > lifetime) Vanish();
        }

        public float TakeDamage(float amount, Vector3 hitPoint)
        {
            if (!IsAlive) return 0f;
            float dealt = Mathf.Min(hp, amount);
            hp -= amount;
            if (hp <= 0f) Vanish();
            return dealt;
        }

        void Vanish()
        {
            hp = 0f;
            var boss = PrimordialBoss.I;
            if (boss != null && VfxLibrary.I != null) VfxLibrary.Burst(transform.position, 0.8f, boss.burstMaterial, 0.25f);
            Destroy(gameObject);
        }
    }
}
