using UnityEngine;

namespace AMG
{
    /// プレイヤーのHP。ソロ（AI仲間なし）なのでHP0で即死
    public class PlayerHealth : MonoBehaviour
    {
        public static PlayerHealth I { get; private set; }

        public float maxHp = 100f;
        public Transform visual;

        public float Hp { get; private set; }
        public float Shield { get; set; }
        public float MaxShield { get; set; }
        public float LastHitTime { get; private set; } = -10f;
        public bool IsAlive => Hp > 0f;
        public Vector3 Center => transform.position + Vector3.up * 0.9f;

        /// ウルトによる被ダメージ倍率
        [HideInInspector] public float damageTakenMultiplier = 1f;

        PlayerController controller;
        bool dodgeInvulnerable;

        void Awake()
        {
            I = this;
            Hp = maxHp;
            controller = GetComponent<PlayerController>();
        }

        public void SetDodgeInvulnerable(bool value)
        {
            dodgeInvulnerable = value;
        }

        /// 当たったらtrue（回避の無敵中などはfalse）
        public bool TakeHit(float damage, float stun = 0f)
        {
            if (!IsAlive || !GameManager.IsPlaying || dodgeInvulnerable) return false;

            damage *= damageTakenMultiplier;
            if (Shield > 0f)
            {
                float absorbed = Mathf.Min(Shield, damage);
                Shield -= absorbed;
                damage -= absorbed;
            }
            Hp = Mathf.Max(0f, Hp - damage);
            LastHitTime = Time.time;

            if (Hp <= 0f) Die();
            else if (stun > 0f) controller.Stun(stun);
            return true;
        }

        public void Heal(float amount)
        {
            if (!IsAlive) return;
            Hp = Mathf.Min(maxHp, Hp + amount);
        }

        void Die()
        {
            if (visual != null) visual.localRotation = Quaternion.Euler(-85f, 0f, 0f);
            if (GameManager.I != null) GameManager.I.OnPlayerDied();
        }
    }
}
