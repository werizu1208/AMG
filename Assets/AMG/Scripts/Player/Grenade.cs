using System.Collections.Generic;
using UnityEngine;

namespace AMG
{
    public class Grenade : MonoBehaviour
    {
        float damage;
        float radius;
        float fuse;
        float groundDrag;
        Rigidbody rb;
        bool landed;

        public void Init(float damage, float radius, float fuse, float groundDrag)
        {
            this.damage = damage;
            this.radius = radius;
            this.fuse = fuse;
            this.groundDrag = groundDrag;
            rb = GetComponent<Rigidbody>();
        }

        void Update()
        {
            fuse -= Time.deltaTime;
            if (fuse <= 0f) Explode();
        }

        /// 最初に何かに触れたら減速を強めて、転がりすぎないようにする（空中は抵抗なし＝予測線どおりに飛ぶ）
        void OnCollisionEnter(Collision collision)
        {
            if (landed || rb == null) return;
            landed = true;
            rb.linearDamping = groundDrag;
            rb.angularDamping = groundDrag;
        }

        void Explode()
        {
            var done = new HashSet<IDamageable>();
            foreach (var col in Physics.OverlapSphere(transform.position, radius, Layers.ShootMask, QueryTriggerInteraction.Ignore))
            {
                var target = WeakPoint.Resolve(col.GetComponentInParent<IDamageable>());
                if (target != null && target.IsAlive && done.Add(target))
                    CombatEvents.ReportPlayerDamage(target.TakeDamage(damage, col.ClosestPoint(transform.position)));
            }
            if (VfxLibrary.I != null) VfxLibrary.Burst(transform.position, radius, VfxLibrary.I.explosion, 0.35f);
            Destroy(gameObject);
        }
    }
}
