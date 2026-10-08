using UnityEngine;

namespace AMG
{
    /// ボスの弱点部位（ヘッドショット判定）。銃弾が直接当たったときだけ被ダメージが倍率つきでボスに届く。
    /// 爆発や範囲ダメージは Resolve でボス本体に置き換え、倍率なし・二重ヒットなしで受ける
    public class WeakPoint : MonoBehaviour, IDamageable
    {
        public float damageMultiplier = 1.2f;

        BossBase owner;

        public bool IsAlive => owner != null && owner.IsAlive;

        /// 範囲攻撃用：弱点ならボス本体を返す
        public static IDamageable Resolve(IDamageable target) => target is WeakPoint wp && wp.owner != null ? wp.owner : target;

        public void Init(BossBase boss, float multiplier)
        {
            owner = boss;
            damageMultiplier = multiplier;
        }

        public float TakeDamage(float amount, Vector3 hitPoint)
        {
            if (owner == null) return 0f;
            float dealt = owner.TakeDamage(amount * damageMultiplier, hitPoint);
            if (dealt > 0f) CombatEvents.ReportWeakPointHit();
            return dealt;
        }
    }
}
