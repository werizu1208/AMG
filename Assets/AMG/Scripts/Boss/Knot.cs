using System.Collections;
using UnityEngine;

namespace AMG
{
    /// 瘤（こぶ）。樹液が脈打つ木の塊。根でボスとつながっている。
    /// 壊すと生っている木が枯れ、根がボスに吸収される
    public class Knot : MonoBehaviour, IDamageable, IHealthBarTarget
    {
        public float maxHp = 350f;
        /// 壊したときに枯れる木（幹・葉）
        public Renderer[] witherTargets;

        public bool IsAlive => hp > 0f;
        public float Hp01 => hp / maxHp;
        public Vector3 BarAnchor => new Vector3(transform.position.x, barTopY + 0.5f, transform.position.z);

        BossController boss;
        float hp;
        float barTopY;
        Vector3 baseScale;
        float seed;

        void Awake()
        {
            hp = maxHp;
            baseScale = transform.localScale;
            seed = Random.Range(0f, 10f);
            // 脈動でバーが揺れないよう、初期の大きさで高さを決めておく
            var r = GetComponent<Renderer>();
            barTopY = r != null ? r.bounds.max.y : transform.position.y + 0.5f;
        }

        void OnEnable() => HealthBars.Register(this);
        void OnDisable() => HealthBars.Unregister(this);

        public void Bind(BossController owner)
        {
            boss = owner;
        }

        void Update()
        {
            if (!IsAlive) return;
            float pulse = 1f + 0.07f * Mathf.Sin((Time.time + seed) * 3f);
            transform.localScale = baseScale * pulse;
        }

        public float TakeDamage(float amount, Vector3 hitPoint)
        {
            if (!IsAlive || !GameManager.IsPlaying) return 0f;
            float before = hp;
            hp = Mathf.Max(0f, hp - amount);
            if (hp <= 0f) Break();
            return before - hp;
        }

        void Break()
        {
            GetComponent<Collider>().enabled = false;
            var wither = VfxLibrary.I != null ? VfxLibrary.I.wither : null;
            if (wither != null)
            {
                foreach (var r in witherTargets) r.sharedMaterial = wither;
                GetComponent<Renderer>().sharedMaterial = wither;
            }
            StartCoroutine(Shrivel());
            if (boss != null) boss.OnKnotDestroyed(this);
        }

        IEnumerator Shrivel()
        {
            Vector3 from = transform.localScale;
            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                transform.localScale = Vector3.Lerp(from, baseScale * 0.35f, t / 0.6f);
                yield return null;
            }
        }
    }
}
