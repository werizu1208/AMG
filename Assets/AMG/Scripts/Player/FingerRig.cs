using UnityEngine;

namespace AMG
{
    /// グローブのモデル（Tools/blender/rig_hand.py でボーンを付けたもの）の指を動かす。
    /// ボーン名：Thumb1〜3 / Index1〜3 / Middle1〜3 / Ring1〜3 / Pinky1〜3。
    /// 回す軸は「指の向き」「手の甲の向き」「親指のある側」から指ごとに求めるので、
    /// 左右反転したモデルでも、同じ値で同じ形（＋X は手のひら側、＋Y は親指側。親指は人差し指から離れる側）になる
    public class FingerRig : MonoBehaviour
    {
        static readonly string[] Names = { "Thumb", "Index", "Middle", "Ring", "Pinky" };
        /// 曲げ（X）の関節ごとの配分（付け根・中・先）
        static readonly float[] JointShare = { 1f, 1.1f, 0.8f };

        readonly Transform[,] bones = new Transform[5, 3];
        readonly Quaternion[,] rest = new Quaternion[5, 3];
        // 関節ごとのローカルの軸：曲げ・開き・ひねり
        readonly Vector3[,] curlAxis = new Vector3[5, 3];
        readonly Vector3[,] spreadAxis = new Vector3[5, 3];
        readonly Vector3[,] twistAxis = new Vector3[5, 3];
        bool ready;

        /// モデルの見た目（VisualSlot が作ったインスタンス）から FingerRig を取得する。指のボーンがなければ null
        public static FingerRig For(GameObject model)
        {
            if (model == null) return null;
            var rig = model.GetComponent<FingerRig>();
            if (rig == null)
            {
                rig = model.AddComponent<FingerRig>();
                rig.hideFlags = model.hideFlags;
            }
            if (!rig.ready) rig.Init();
            return rig.ready ? rig : null;
        }

        void Init()
        {
            var all = GetComponentsInChildren<Transform>(true);
            for (int f = 0; f < 5; f++)
                for (int j = 0; j < 3; j++)
                    foreach (var t in all)
                        if (t.name == Names[f] + (j + 1)) bones[f, j] = t;
            for (int f = 0; f < 5; f++)
                for (int j = 0; j < 3; j++)
                    if (bones[f, j] == null) return;

            // 手の甲の向き＝モデルの上方向（グローブは手の甲が +Y）。親指のある側は中指の付け根から見た親指の付け根
            Vector3 back = transform.up;
            Vector3 thumbSide = bones[0, 0].position - bones[2, 0].position;
            for (int f = 0; f < 5; f++)
            {
                for (int j = 0; j < 3; j++)
                {
                    var b = bones[f, j];
                    Vector3 next = j < 2 ? bones[f, j + 1].position : b.position + (b.position - bones[f, j - 1].position);
                    Vector3 dir = (next - b.position).normalized;
                    Vector3 palm = -Vector3.ProjectOnPlane(back, dir).normalized;
                    // 親指は「人差し指から離れる向き」を＋Y（開く）にする
                    Vector3 sideRef = f == 0 ? b.position - bones[1, 0].position : thumbSide;
                    Vector3 side = Vector3.ProjectOnPlane(sideRef, dir);
                    side = (side - Vector3.Project(side, palm)).normalized;
                    rest[f, j] = b.localRotation;

                    // 指先が palm へ動く軸・side へ動く軸・手のひら側の点が side へ動く軸
                    curlAxis[f, j] = LocalAxis(b, next, palm);
                    spreadAxis[f, j] = LocalAxis(b, next, side);
                    twistAxis[f, j] = LocalAxisTwist(b, dir, palm, side);
                }
            }
            ready = true;
        }

        /// 「ローカルでこの軸を＋に回すと、点 target が向き want へ動く」ローカル軸を求める。
        /// 親が左右反転していても正しい向きになるよう、実際に少し回して確かめる
        static Vector3 LocalAxis(Transform b, Vector3 target, Vector3 want)
        {
            Vector3 dir = (target - b.position).normalized;
            Vector3 worldAxis = Vector3.Cross(dir, want).normalized;
            Vector3 local = Quaternion.Inverse(b.rotation) * worldAxis;
            return Verify(b, b.InverseTransformPoint(target), local, want);
        }

        static Vector3 LocalAxisTwist(Transform b, Vector3 dir, Vector3 palm, Vector3 side)
        {
            Vector3 local = Quaternion.Inverse(b.rotation) * dir;
            return Verify(b, b.InverseTransformPoint(b.position + palm * 0.01f), local, side);
        }

        static Vector3 Verify(Transform b, Vector3 localPoint, Vector3 localAxis, Vector3 want)
        {
            var saved = b.localRotation;
            Vector3 before = b.TransformPoint(localPoint);
            b.localRotation = saved * Quaternion.AngleAxis(5f, localAxis);
            Vector3 after = b.TransformPoint(localPoint);
            b.localRotation = saved;
            return Vector3.Dot(after - before, want) >= 0f ? localAxis : -localAxis;
        }

        /// 握り方の指の角度を当てる
        public void Apply(GripPose.HandGrip grip)
        {
            if (!ready || grip == null) return;
            for (int f = 0; f < 5; f++)
            {
                var finger = grip.Get(f);
                for (int j = 0; j < 3; j++)
                {
                    // 曲げは3関節に配分、開き・ひねりは付け根だけ。関節ごとの値はそのまま上乗せ
                    Vector3 a = finger.Joint(j);
                    a.x += finger.angle.x * JointShare[j];
                    if (j == 0) { a.y += finger.angle.y; a.z += finger.angle.z; }
                    bones[f, j].localRotation = rest[f, j]
                        * Quaternion.AngleAxis(a.x, curlAxis[f, j])
                        * Quaternion.AngleAxis(a.y, spreadAxis[f, j])
                        * Quaternion.AngleAxis(a.z, twistAxis[f, j]);
                }
            }
        }

        /// 指を伸ばした元の形に戻す
        public void Relax()
        {
            for (int f = 0; f < 5; f++)
                for (int j = 0; j < 3; j++)
                    if (bones[f, j] != null) bones[f, j].localRotation = rest[f, j];
        }
    }
}
