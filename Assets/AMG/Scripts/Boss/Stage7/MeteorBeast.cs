using System.Collections.Generic;
using UnityEngine;

namespace AMG
{
    /// 化け物：人間ほどの直径がある隕石。魔法少女の魔法陣から生まれ、空中からプレイヤーのいる地面へゆっくり突撃する。
    /// 地面に着くと赤くなり、数秒後に爆発する。爆発前に撃破すれば、爆発せずに消滅する
    public class MeteorBeast : MonoBehaviour, IDamageable, IHealthBarTarget
    {
        public static readonly List<MeteorBeast> Alive = new List<MeteorBeast>();

        public float maxHp = 160f;
        public float speed = 4.5f;
        public float fuse = 3f;
        public float blastRadius = 4.5f;
        public float blastDamage = 35f;
        public float radius = 0.9f;

        public bool IsAlive => hp > 0f;
        public float Hp01 => hp / maxHp;
        public Vector3 BarAnchor => transform.position + Vector3.up * (radius + 0.6f);

        float hp;
        bool landed;
        float fuseLeft;
        Renderer body;
        GameObject warning;
        Vector3 spin;

        public static MeteorBeast Spawn(Vector3 pos, PrimordialBoss boss)
        {
            Stage7Fx.MagicCircle(pos + Vector3.up * 0.6f, 2.2f, boss.circleMaterial, 1.6f);
            var go = Prim.Create(PrimitiveType.Sphere, "MeteorBeast", null, pos, Vector3.one * 1.8f, boss.meteorMaterial, true);
            go.layer = Layers.Enemy;
            Stage7Fx.AddTrail(go, boss.cometMaterial, 1.2f, 0.5f);
            Stage7Fx.Spawned.Add(go);
            var m = go.AddComponent<MeteorBeast>();
            m.body = go.GetComponent<Renderer>();
            return m;
        }

        void Awake()
        {
            hp = maxHp;
            spin = Random.onUnitSphere * 40f;
        }

        void OnEnable()
        {
            Alive.Add(this);
            HealthBars.Register(this);
        }

        void OnDisable()
        {
            Alive.Remove(this);
            HealthBars.Unregister(this);
        }

        void Update()
        {
            if (!GameManager.IsPlaying) return;
            float dt = Time.deltaTime;
            transform.Rotate(spin * dt, Space.World);
            var boss = PrimordialBoss.I;

            if (!landed)
            {
                // プレイヤーのいる地面へ、斜めにゆっくり降りてくる
                var player = PlayerHealth.I;
                Vector3 target = player != null ? player.transform.position : transform.position + Vector3.down;
                target.y = Stage7Fx.GroundHeight(target) + radius;
                transform.position = Vector3.MoveTowards(transform.position, target, speed * dt);
                if (transform.position.y <= Stage7Fx.GroundHeight(transform.position) + radius + 0.05f)
                {
                    landed = true;
                    fuseLeft = fuse;
                    if (boss != null) body.sharedMaterial = boss.meteorHotMaterial;
                    Vector3 ground = transform.position;
                    ground.y = Stage7Fx.GroundHeight(ground) + 0.03f;
                    if (boss != null)
                    {
                        warning = Prim.Create(PrimitiveType.Cylinder, "Telegraph", null, ground, new Vector3(blastRadius * 2f, 0.01f, blastRadius * 2f), boss.telegraphMaterial);
                        DangerZone.AddCircle(warning, ground, blastRadius);
                    }
                }
                return;
            }

            // 赤く脈打ちながら爆発を待つ
            fuseLeft -= dt;
            float pulse = 1f + 0.12f * Mathf.Sin(Time.time * Mathf.Lerp(8f, 30f, 1f - fuseLeft / fuse));
            transform.localScale = Vector3.one * 1.8f * pulse;
            if (fuseLeft <= 0f)
            {
                Stage7Fx.Blast(transform.position, blastRadius, blastDamage, 9f, 5f);
                if (boss != null && VfxLibrary.I != null) VfxLibrary.Burst(transform.position, blastRadius, boss.burstMaterial, 0.45f);
                Remove();
            }
        }

        public float TakeDamage(float amount, Vector3 hitPoint)
        {
            if (!IsAlive) return 0f;
            float dealt = Mathf.Min(hp, amount);
            hp -= amount;
            if (hp <= 0f)
            {
                // 爆発せずに砕ける
                var boss = PrimordialBoss.I;
                if (boss != null && VfxLibrary.I != null) VfxLibrary.Burst(transform.position, 1.5f, boss.burstMaterial, 0.3f);
                Remove();
            }
            return dealt;
        }

        void Remove()
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
