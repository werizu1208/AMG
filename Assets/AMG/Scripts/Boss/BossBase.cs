using UnityEngine;

namespace AMG
{
    /// ボスの共通部分。HUD（HPバー・状態表示）と弱点（WeakPoint）は、この型を通してボスを扱う
    public abstract class BossBase : MonoBehaviour, IDamageable
    {
        public string bossName = "";
        public float maxHp = 5000f;

        [Header("出撃前の画面")]
        public string stageTitle = "";
        [TextArea] public string loadoutHint = "";

        public float Hp { get; protected set; }
        public int Phase { get; protected set; } = 1;
        public bool IsDead { get; protected set; }
        public bool IsAlive => !IsDead;

        public abstract float TakeDamage(float amount, Vector3 hitPoint);

        /// HPバーの下に出す状態の説明
        public virtual string StatusText => $"フェーズ{Phase}";
        public virtual Color HpBarColor => new Color(0.45f, 0.6f, 0.25f);
        /// HPバーに目盛りを入れる位置（最大HPに対する割合）
        public virtual float[] PhaseMarkers => null;
    }
}
