using System.Collections;
using UnityEngine;

namespace AMG
{
    /// ボスが召喚する案山子のような木の雑魚。ぎこちなく跳ねながら近づいて殴る
    [RequireComponent(typeof(CharacterController))]
    public class Scarecrow : MonoBehaviour, IDamageable, IHealthBarTarget
    {
        public static int Alive { get; private set; }

        public float maxHp = 60f;
        public float speed = 3.2f;
        public float attackRange = 1.7f;
        public float damage = 10f;
        public float attackWindup = 0.45f;
        public float attackCooldown = 1.3f;

        public bool IsAlive => hp > 0f;
        public float Hp01 => hp / maxHp;
        public Vector3 BarAnchor => transform.position + Vector3.up * 2.75f;

        Transform body;
        CharacterController cc;
        float hp;
        float cooldown;
        float verticalVelocity;
        float hopPhase;
        bool attacking;

        public static Scarecrow Spawn(Vector3 pos, Quaternion rot)
        {
            var lib = VfxLibrary.I;
            var go = new GameObject("Scarecrow");
            go.layer = Layers.Enemy;
            go.transform.SetPositionAndRotation(pos, rot);
            var cc = go.AddComponent<CharacterController>();
            cc.height = 2f;
            cc.radius = 0.4f;
            cc.center = new Vector3(0f, 1f, 0f);

            var body = Prim.Empty("Body", go.transform, Vector3.zero);
            Prim.Create(PrimitiveType.Cylinder, "Post", body, new Vector3(0f, 1f, 0f), new Vector3(0.15f, 1f, 0.15f), lib.spike);
            Prim.Create(PrimitiveType.Cube, "Arms", body, new Vector3(0f, 1.5f, 0f), new Vector3(1.6f, 0.12f, 0.12f), lib.spike);
            Prim.Create(PrimitiveType.Cube, "Straw", body, new Vector3(0f, 1.3f, 0f), new Vector3(0.6f, 0.7f, 0.3f), lib.scarecrow);
            Prim.Create(PrimitiveType.Sphere, "Head", body, new Vector3(0f, 2.1f, 0f), Vector3.one * 0.5f, lib.scarecrowHead);

            var s = go.AddComponent<Scarecrow>();
            s.body = body;
            return s;
        }

        void Awake()
        {
            hp = maxHp;
            cc = GetComponent<CharacterController>();
            hopPhase = Random.Range(0f, 10f);
        }

        void OnEnable()
        {
            Alive++;
            HealthBars.Register(this);
        }

        void OnDisable()
        {
            Alive--;
            HealthBars.Unregister(this);
        }

        void Update()
        {
            if (!GameManager.IsPlaying || !IsAlive) return;
            var player = PlayerHealth.I;
            if (player == null || !player.IsAlive) return;

            float dt = Time.deltaTime;
            cooldown -= dt;
            Vector3 to = player.transform.position - transform.position;
            to.y = 0f;
            float dist = to.magnitude;
            if (to.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), 360f * dt);

            Vector3 move = Vector3.zero;
            if (!attacking)
            {
                if (dist > attackRange) move = to.normalized * speed;
                else if (cooldown <= 0f) StartCoroutine(Attack());
            }

            // 一本足で跳ねるように揺れる
            hopPhase += dt * (move.sqrMagnitude > 0f ? 9f : 2f);
            if (!attacking) body.localRotation = Quaternion.Euler(Mathf.Sin(hopPhase) * 8f, 0f, Mathf.Sin(hopPhase * 0.5f) * 12f);

            if (cc.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity -= 20f * dt;
            cc.Move((move + Vector3.up * verticalVelocity) * dt);
        }

        IEnumerator Attack()
        {
            attacking = true;
            for (float t = 0f; t < attackWindup; t += Time.deltaTime)
            {
                body.localRotation = Quaternion.Euler(-25f * (t / attackWindup), 0f, 0f);
                yield return null;
            }
            body.localRotation = Quaternion.Euler(35f, 0f, 0f);
            var player = PlayerHealth.I;
            if (player != null && Vector3.Distance(player.transform.position, transform.position) <= attackRange + 0.4f)
                player.TakeHit(damage);
            yield return new WaitForSeconds(0.3f);
            cooldown = attackCooldown;
            attacking = false;
        }

        public float TakeDamage(float amount, Vector3 hitPoint)
        {
            if (!IsAlive) return 0f;
            float before = hp;
            hp = Mathf.Max(0f, hp - amount);
            if (hp <= 0f) Destroy(gameObject);
            return before - hp;
        }
    }
}
