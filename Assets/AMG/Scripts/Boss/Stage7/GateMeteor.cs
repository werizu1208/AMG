using UnityEngine;

namespace AMG
{
    /// フェーズ4で空からゆっくり降ってくる巨大な隕石。これを壊すまで、原初の魔法少女にはダメージが通らない。
    /// 壊せば本体のすべてが弱点になる。地面に落ちると大爆発する
    public class GateMeteor : MonoBehaviour, IDamageable, IHealthBarTarget
    {
        public float maxHp = 650f;
        public float fallTime = 24f;
        public float blastRadius = 10f;
        public float blastDamage = 60f;
        public float size = 5f;

        public bool IsAlive => hp > 0f;
        public float Hp01 => hp / maxHp;
        public Vector3 BarAnchor => transform.position + Vector3.up * (size * 0.5f + 1f);
        /// 壊されたか落ちたかで決着がついた
        public bool Resolved { get; private set; }
        public bool WasDestroyed { get; private set; }
        /// 落ちるまでの残り時間（0〜1）
        public float Remaining01 => Mathf.Clamp01(1f - elapsed / fallTime);

        float hp;
        float elapsed;
        Vector3 start;
        Vector3 ground;
        GameObject warning;
        bool warned;
        PrimordialBoss boss;

        public static GateMeteor Spawn(Vector3 landing, float height, PrimordialBoss boss)
        {
            Vector3 start = landing + Vector3.up * height + new Vector3(Random.Range(-8f, 8f), 0f, Random.Range(-8f, 8f));
            var go = Prim.Create(PrimitiveType.Sphere, "GateMeteor", null, start, Vector3.one * 5f, boss.meteorMaterial, true);
            go.layer = Layers.Enemy;
            Stage7Fx.AddTrail(go, boss.cometMaterial, 4f, 1.6f);
            Stage7Fx.Spawned.Add(go);
            var m = go.AddComponent<GateMeteor>();
            m.boss = boss;
            m.start = start;
            m.ground = landing;
            m.maxHp = boss.gateMeteorHp;
            m.fallTime = boss.gateFallTime;
            m.blastRadius = boss.gateBlastRadius;
            m.blastDamage = boss.gateBlastDamage;
            m.hp = m.maxHp;
            // 落下地点の予兆（最初は薄く、落ちる直前に危険表示にする）
            m.warning = Prim.Create(PrimitiveType.Cylinder, "GateLanding", null, landing + Vector3.up * 0.04f,
                new Vector3(m.blastRadius * 2f, 0.01f, m.blastRadius * 2f), boss.circleMaterial);
            return m;
        }

        void OnEnable() => HealthBars.Register(this);
        void OnDisable() => HealthBars.Unregister(this);

        void Update()
        {
            if (!GameManager.IsPlaying || Resolved) return;
            elapsed += Time.deltaTime;
            float k = Mathf.Clamp01(elapsed / fallTime);
            // 最初はゆっくり、最後に一気に落ちてくる
            float fall = k * k * (0.35f + 0.65f * k);
            transform.position = Vector3.Lerp(start, ground + Vector3.up * size * 0.4f, fall);
            transform.Rotate(new Vector3(8f, 20f, 5f) * Time.deltaTime, Space.World);

            if (!warned && fallTime - elapsed <= 5f && warning != null)
            {
                warned = true;
                warning.GetComponent<Renderer>().sharedMaterial = boss.telegraphMaterial;
                DangerZone.AddCircle(warning, ground, blastRadius);
            }

            if (k >= 1f) Land();
        }

        void Land()
        {
            Resolved = true;
            Stage7Fx.Blast(ground, blastRadius, blastDamage, 14f, 7f);
            if (VfxLibrary.I != null) VfxLibrary.Burst(ground, blastRadius, boss.burstMaterial, 0.8f);
            Cleanup();
        }

        public float TakeDamage(float amount, Vector3 hitPoint)
        {
            if (Resolved || !GameManager.IsPlaying) return 0f;
            float dealt = Mathf.Min(hp, amount);
            hp -= amount;
            if (hp <= 0f)
            {
                Resolved = true;
                WasDestroyed = true;
                if (VfxLibrary.I != null) VfxLibrary.Burst(transform.position, size, boss.burstMaterial, 0.6f);
                Cleanup();
            }
            return dealt;
        }

        void Cleanup()
        {
            hp = 0f;
            if (warning != null) Destroy(warning);
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (warning != null) Destroy(warning);
        }
    }
}
