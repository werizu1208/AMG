using UnityEngine;

namespace AMG
{
    /// 銃ごとの握り方。手首の位置・向き（銃から見た値）と、指の角度を持つ。
    /// メニュー「A・M・G > 握り方エディタ」で、シーンを見ながら調整できる。
    /// アセットなので、再生中に変えた値もそのまま残る
    [CreateAssetMenu(menuName = "A・M・G/握り方（GripPose）", fileName = "GripPose")]
    public class GripPose : ScriptableObject
    {
        /// 指1本の角度（度）。軸は指ごとの向きで決まるので、左右の手で同じ値が同じ形になる
        ///   X：曲げ（＋で手のひら側へ。3つの関節に配分）
        ///   Y：開き（＋で親指側へ。付け根の関節）
        ///   Z：ひねり（指の向きを軸に回す。付け根の関節）
        /// joints は関節ごと（付け根・中・先）の追加の角度で、同じ X/Y/Z の軸で上乗せする
        [System.Serializable]
        public class Finger
        {
            public Vector3 angle;
            public Vector3[] joints = new Vector3[3];

            public Vector3 Joint(int j) => joints != null && j < joints.Length ? joints[j] : Vector3.zero;

            public void CopyFrom(Finger src)
            {
                angle = src.angle;
                joints = new Vector3[3];
                for (int j = 0; j < 3; j++) joints[j] = src.Joint(j);
            }
        }

        [System.Serializable]
        public class HandGrip : ISerializationCallbackReceiver
        {
            public static readonly string[] FingerNames = { "親指", "人差し指", "中指", "薬指", "小指" };

            [Tooltip("オフにすると、この手は銃を持たずに体の横へ下ろす（片手持ち）")]
            public bool holding = true;
            [Tooltip("銃の原点（後端）から見た手首の位置（銃のローカル座標・m）")]
            public Vector3 position;
            [Tooltip("銃に対する手の向き（オイラー角）")]
            public Vector3 rotation;

            [Tooltip("親指・人差し指・中指・薬指・小指の角度（X 曲げ / Y 開き / Z ひねり）")]
            public Finger[] fingers = NewFingers();

            // 以前の形式（曲げだけ）の値。読み込み時に fingers[].angle.x へ移す
            [SerializeField, HideInInspector] float thumb, index, middle, ring, pinky;
            [SerializeField, HideInInspector] int version;

            public Finger Get(int finger)
            {
                if (fingers == null || fingers.Length != 5) fingers = NewFingers();
                return fingers[finger] ??= new Finger();
            }

            /// 曲げ（X）だけをまとめて設定する
            public void SetCurl(float t, float i, float m, float r, float p)
            {
                float[] c = { t, i, m, r, p };
                for (int f = 0; f < 5; f++)
                {
                    var a = Get(f).angle;
                    Get(f).angle = new Vector3(c[f], a.y, a.z);
                }
            }

            static Finger[] NewFingers()
            {
                var a = new Finger[5];
                for (int f = 0; f < 5; f++) a[f] = new Finger();
                return a;
            }

            public void OnBeforeSerialize() { version = 1; }

            public void OnAfterDeserialize()
            {
                if (version >= 1) return;
                if (fingers == null || fingers.Length != 5) fingers = NewFingers();
                float[] c = { thumb, index, middle, ring, pinky };
                for (int f = 0; f < 5; f++)
                {
                    fingers[f] ??= new Finger();
                    fingers[f].angle = new Vector3(c[f], 0f, 0f);
                }
                version = 1;
            }
        }

        public HandGrip right = new HandGrip();
        public HandGrip left = new HandGrip();
    }
}
