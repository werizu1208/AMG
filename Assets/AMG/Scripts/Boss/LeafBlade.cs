using UnityEngine;

namespace AMG
{
    /// 葉の刃。宙に舞ったあと一直線に飛ぶ。建物などの遮蔽物で止まる
    public class LeafBlade : MonoBehaviour
    {
        const float HitRadius = 0.6f;

        Vector3 dir;
        float speed;
        float damage;
        float life = 4f;
        bool launched;
        Vector3 hoverOffset;
        float spin;

        public static LeafBlade Create(Vector3 pos, float scale)
        {
            var go = Prim.Create(PrimitiveType.Cube, "LeafBlade", null, pos, new Vector3(0.6f, 0.04f, 0.3f) * scale,
                VfxLibrary.I != null ? VfxLibrary.I.leaf : null);
            var leaf = go.AddComponent<LeafBlade>();
            leaf.spin = Random.Range(300f, 700f);
            return leaf;
        }

        public void Hover(Vector3 center)
        {
            if (hoverOffset == Vector3.zero) hoverOffset = transform.position - center;
            transform.position = center + Quaternion.AngleAxis(Time.deltaTime * 90f, Vector3.up) * hoverOffset;
            hoverOffset = transform.position - center;
        }

        public void Launch(Vector3 direction, float speed, float damage)
        {
            dir = direction.normalized;
            this.speed = speed;
            this.damage = damage;
            launched = true;
            transform.rotation = Quaternion.LookRotation(dir);
        }

        void Update()
        {
            if (!launched)
            {
                transform.Rotate(Vector3.up, spin * Time.deltaTime, Space.World);
                return;
            }
            if (!GameManager.IsPlaying) return;

            float step = speed * Time.deltaTime;
            if (Physics.Raycast(transform.position, dir, step, Layers.EnvironmentMask, QueryTriggerInteraction.Ignore))
            {
                Destroy(gameObject);
                return;
            }
            transform.position += dir * step;
            transform.Rotate(Vector3.forward, spin * 2f * Time.deltaTime, Space.Self);

            var player = PlayerHealth.I;
            if (player != null && player.IsAlive)
            {
                Vector3 a = player.transform.position + Vector3.up * 0.3f;
                Vector3 b = player.transform.position + Vector3.up * 1.5f;
                Vector3 ab = b - a;
                float t = Mathf.Clamp01(Vector3.Dot(transform.position - a, ab) / ab.sqrMagnitude);
                if ((a + ab * t - transform.position).sqrMagnitude < HitRadius * HitRadius && player.TakeHit(damage))
                {
                    Destroy(gameObject);
                    return;
                }
            }

            life -= Time.deltaTime;
            if (life <= 0f) Destroy(gameObject);
        }
    }
}
