using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AMG
{
    public enum UltType { AirLayer, HumanWisdom }

    /// ウルト（Q）。チャージは時間経過＋与ダメージで加速
    public class UltSystem : MonoBehaviour
    {
        [Header("チャージ")]
        public float baseChargeTime = 90f;
        public float damageForFullCharge = 2500f;

        [Header("① エアバック（空気の層をまとう）")]
        public float airDuration = 10f;
        public float airShield = 60f;
        public float airDamageTaken = 0.6f;
        public float airAuraRadius = 2.6f;
        public float airAuraDps = 35f;

        [Header("② 潜在能力解放")]
        public float wisdomDuration = 8f;
        public float wisdomDamage = 1.8f;
        public float wisdomFireRate = 1.5f;
        public float wisdomMoveSpeed = 1.35f;
        public float wisdomDamageTaken = 0.8f;
        public float wisdomRegen = 6f;

        public UltType Type { get; private set; }
        public float Charge { get; private set; }
        public bool IsActive => activeTimer > 0f;
        public float ActiveRemaining => activeTimer;
        public float ActiveDuration => Type == UltType.AirLayer ? airDuration : wisdomDuration;

        PlayerController controller;
        PlayerHealth health;
        WeaponSystem weapons;
        GameObject effect;
        float activeTimer;
        float auraTick;

        public static string DisplayName(UltType t) => t == UltType.AirLayer ? "エアバック" : "潜在能力解放";

        public static string Description(UltType t) => t == UltType.AirLayer
            ? "人類が唯一、魔法少女に勝利して得た力。\n体に空気の層をまとい、シールド付与・被ダメージ軽減・触れた敵に継続ダメージ。"
            : "人類の英知を集めた汎用兵装。\n全ステータスが一時的に高倍率でアップし、HPが継続回復する。";

        void Awake()
        {
            controller = GetComponent<PlayerController>();
            health = GetComponent<PlayerHealth>();
            weapons = GetComponent<WeaponSystem>();
        }

        void OnEnable() => CombatEvents.PlayerDealtDamage += OnDealtDamage;
        void OnDisable() => CombatEvents.PlayerDealtDamage -= OnDealtDamage;

        public void SetUlt(UltType type) => Type = type;

        void OnDealtDamage(float amount)
        {
            if (!IsActive) Charge = Mathf.Min(1f, Charge + amount / damageForFullCharge);
        }

        void Update()
        {
            if (!health.IsAlive)
            {
                if (IsActive) End();
                return;
            }
            if (!GameManager.IsPlaying) return;

            float dt = Time.deltaTime;
            if (IsActive)
            {
                activeTimer -= dt;
                Tick(dt);
                if (activeTimer <= 0f) End();
                return;
            }

            Charge = Mathf.Min(1f, Charge + dt / baseChargeTime);
            var kb = Keyboard.current;
            if (kb != null && kb.qKey.wasPressedThisFrame && Charge >= 1f && !controller.IsStunned) Activate();
        }

        void Activate()
        {
            Charge = 0f;
            activeTimer = ActiveDuration;
            var lib = VfxLibrary.I;

            if (Type == UltType.AirLayer)
            {
                health.MaxShield = airShield;
                health.Shield = airShield;
                health.damageTakenMultiplier = airDamageTaken;
                effect = Prim.Create(PrimitiveType.Sphere, "AirLayer", transform, Vector3.up * 0.9f,
                    Vector3.one * airAuraRadius * 2f, lib != null ? lib.airLayer : null);
            }
            else
            {
                weapons.damageMultiplier = wisdomDamage;
                weapons.fireRateMultiplier = wisdomFireRate;
                controller.moveSpeedMultiplier = wisdomMoveSpeed;
                health.damageTakenMultiplier = wisdomDamageTaken;
                effect = Prim.Create(PrimitiveType.Sphere, "HumanWisdom", transform, Vector3.up * 0.9f,
                    new Vector3(1.4f, 2.2f, 1.4f), lib != null ? lib.overdrive : null);
            }
        }

        void Tick(float dt)
        {
            if (Type == UltType.HumanWisdom)
            {
                health.Heal(wisdomRegen * dt);
                return;
            }

            // エアバック：範囲内の敵に継続ダメージ（チャージには含めない）
            auraTick -= dt;
            if (auraTick > 0f) return;
            auraTick = 0.25f;
            var done = new HashSet<IDamageable>();
            foreach (var col in Physics.OverlapSphere(health.Center, airAuraRadius, Layers.ShootMask, QueryTriggerInteraction.Ignore))
            {
                var target = WeakPoint.Resolve(col.GetComponentInParent<IDamageable>());
                if (target != null && target.IsAlive && done.Add(target))
                    target.TakeDamage(airAuraDps * 0.25f, col.ClosestPoint(health.Center));
            }
        }

        void End()
        {
            activeTimer = 0f;
            weapons.damageMultiplier = 1f;
            weapons.fireRateMultiplier = 1f;
            controller.moveSpeedMultiplier = 1f;
            health.damageTakenMultiplier = 1f;
            health.Shield = 0f;
            health.MaxShield = 0f;
            if (effect != null) Destroy(effect);
        }
    }
}
