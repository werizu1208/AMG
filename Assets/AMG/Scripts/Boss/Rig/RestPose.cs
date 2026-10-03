using UnityEngine;

namespace AMG
{
    /// 再生していないときの基本姿勢（Aポーズ／Tポーズ）を作るヘルパー。
    /// 規約は TwoBoneIK と同じ：骨はローカル+Z方向に伸び、lower は upper の+Z上、end は lower の+Z上にある
    public static class RestPose
    {
        /// 二関節の手足を、根元から dir の方向へまっすぐ伸ばす。bendSide は関節が曲がる側（骨のローカル+Y）
        public static void Straight(Transform upper, Transform lower, Transform end, Vector3 dir, Vector3 bendSide)
        {
            Quaternion rot = Quaternion.LookRotation(dir.normalized, bendSide);
            upper.rotation = rot;
            lower.rotation = rot;
            end.rotation = rot;
        }

        /// 腕を下ろす方向。armAngle は真下からの開き角（45 で Aポーズ、90 で Tポーズ）。side は左 -1 / 右 +1
        public static Vector3 ArmDirection(Transform body, float side, float armAngle)
        {
            float a = armAngle * Mathf.Deg2Rad;
            return (-body.up * Mathf.Cos(a) + body.right * (side * Mathf.Sin(a))).normalized;
        }

        /// 足の骨（足首）が地面から ankleHeight の高さに来るよう、腰を上下させる
        public static void PlantFeet(Transform hips, Transform foot, Transform body, float ankleHeight)
        {
            float lift = body.position.y + ankleHeight - foot.position.y;
            hips.position += Vector3.up * lift;
        }
    }
}
