using UnityEngine;

namespace AMG
{
    /// プレイヤーの攻撃を受けるもの（ボス、瘤、雑魚）
    public interface IDamageable
    {
        bool IsAlive { get; }

        /// 実際に与えたダメージ量を返す（ウルトのチャージやヒットマーカー用）
        float TakeDamage(float amount, Vector3 hitPoint);
    }

    /// 頭上にHPバーを出すもの（瘤、雑魚）。HUDが描画する
    public interface IHealthBarTarget
    {
        bool IsAlive { get; }
        float Hp01 { get; }
        /// HPバーを表示するワールド座標
        Vector3 BarAnchor { get; }
    }

    public static class HealthBars
    {
        public static readonly System.Collections.Generic.List<IHealthBarTarget> Targets = new();

        public static void Register(IHealthBarTarget t)
        {
            if (!Targets.Contains(t)) Targets.Add(t);
        }

        public static void Unregister(IHealthBarTarget t) => Targets.Remove(t);
    }

    public static class CombatEvents
    {
        /// プレイヤーの攻撃が何かに当たったとき。引数は実際に通ったダメージ（無効なら0）
        public static event System.Action<float> PlayerDealtDamage;

        public static void ReportPlayerDamage(float dealt)
        {
            PlayerDealtDamage?.Invoke(Mathf.Max(0f, dealt));
        }
    }

    public static class Layers
    {
        /// プレイヤーは「Ignore Raycast」レイヤーを流用（自分の弾やカメラ判定に当たらないように）
        public const int Player = 2;
        /// 敵（ボス・瘤・雑魚）。名前なしのユーザーレイヤーを使用
        public const int Enemy = 8;

        public const int ShootMask = ~(1 << Player);
        public const int CameraMask = ~((1 << Player) | (1 << Enemy));
        public const int EnvironmentMask = 1 << 0;
    }
}
