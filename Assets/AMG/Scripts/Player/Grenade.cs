using System.Collections.Generic;
using UnityEngine;

namespace AMG
{
    public class Grenade : MonoBehaviour
    {
        float damage;
        float radius;
        float fuse;

        public void Init(float damage, float radius, float fuse)
        {
            this.damage = damage;
            this.radius = radius;
            this.fuse = fuse;
        }

        void Update()
        {
            fuse -= Time.deltaTime;
            if (fuse <= 0f) Explode();
        }

        void Explode()
        {
            var done = new HashSet<IDamageable>();
            foreach (var col in Physics.OverlapSphere(transform.position, radius, Layers.ShootMask, QueryTriggerInteraction.Ignore))
            {
                var target = col.GetComponentInParent<IDamageable>();
                if (target != null && target.IsAlive && done.Add(target))
                    CombatEvents.ReportPlayerDamage(target.TakeDamage(damage, col.ClosestPoint(transform.position)));
            }
            if (VfxLibrary.I != null) VfxLibrary.Burst(transform.position, radius, VfxLibrary.I.explosion, 0.35f);
            Destroy(gameObject);
        }
    }
}
