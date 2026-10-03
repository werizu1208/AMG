using UnityEngine;
using UnityEngine.InputSystem;

namespace AMG
{
    /// 肩越しのTPSカメラ。常にプレイヤーの右後方・上部に固定される。
    /// マウス左右でプレイヤーごと旋回し、マウス上下ではプレイヤーの頭を中心にカメラが回る（頭は画面の同じ位置に映る）。
    /// 右クリックでエイム（カメラ位置はそのままで、画角だけ狭めてズーム）、壁めり込み防止つき。
    /// 位置はプレイヤーの頭の骨を基準にするので、歩いて上下しても、かがんでも、上半身を傾けても、
    /// 頭は画面の同じ位置に映る（位置合わせはアニメーションが終わった後＝描画直前に行う）
    [DefaultExecutionOrder(-50)] // プレイヤーの移動より先にマウス入力を反映し、本体とカメラの向きがずれないようにする
    [RequireComponent(typeof(Camera))]
    public class TPSCamera : MonoBehaviour
    {
        public static TPSCamera I { get; private set; }

        public Transform target;
        [Tooltip("プレイヤーの立ち姿勢での、足元から見た回転の中心の高さ")]
        public Vector3 pivotOffset = new Vector3(0f, 1.55f, 0f);
        [Tooltip("この骨を追う（空ならプレイヤーの PlayerRig の頭を使う）。頭が画面の同じ位置に映る")]
        public Transform followBone;
        public float sensitivity = 0.1f;

        [Header("位置（プレイヤーから見た右後方・上部）")]
        public float distance = 3.6f;
        public float shoulder = 0.8f;
        public float height = 0.55f;

        [Header("画角（エイム中は位置を変えず、画角だけ狭める）")]
        public float fov = 60f;
        public float aimFov = 45f;

        [Header("視線の上下")]
        public float minPitch = -40f;
        public float maxPitch = 60f;
        public float defaultPitch = 8f;
        public float collisionRadius = 0.25f;

        /// 旋回角。プレイヤー本体の向きはこれに合わせる
        public float Yaw => yaw;
        public Vector3 PlanarForward => Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

        Camera cam;
        PlayerController player;
        float yaw;
        float pitch;
        Vector3 boneToPivot;   // 立ち姿勢での「骨 → 回転の中心」（旋回角から見た向き）
        bool boneOffsetReady;

        void Awake()
        {
            I = this;
            cam = GetComponent<Camera>();
            pitch = defaultPitch;
        }

        void Start()
        {
            if (target != null)
            {
                yaw = target.eulerAngles.y;
                player = target.GetComponent<PlayerController>();
                if (followBone == null)
                {
                    var rig = target.GetComponentInChildren<PlayerRig>();
                    if (rig != null) followBone = rig.head;
                }
            }
            Application.onBeforeRender += Follow;
        }

        void OnDestroy()
        {
            Application.onBeforeRender -= Follow;
        }

        public void AddRecoil(float up, float side)
        {
            pitch -= up;
            yaw += side;
        }

        void Update()
        {
            var mouse = Mouse.current;
            if (mouse != null && GameManager.IsPlaying && GameManager.CursorLocked && PlayerHealth.I != null && PlayerHealth.I.IsAlive)
            {
                Vector2 delta = mouse.delta.ReadValue() * sensitivity;
                yaw += delta.x;
                pitch -= delta.y;
            }
            // 頭を中心に回るので、真上・真下を越えないようにする
            pitch = Mathf.Clamp(pitch, Mathf.Max(minPitch, -85f), Mathf.Min(maxPitch, 85f));
        }

        void LateUpdate()
        {
            if (target == null) return;

            bool aiming = player != null && player.IsAiming;
            float k = 1f - Mathf.Exp(-14f * Time.deltaTime);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, aiming ? aimFov : fov, k);
            Follow();
        }

        /// 体のアニメーション（PlayerRig）が終わった後にもう一度呼ばれ、その時点の頭の位置へ合わせる
        void Follow()
        {
            if (target == null || this == null) return;

            // 頭（骨）を中心に、旋回角と上下の傾きでカメラを回す。
            // 頭からカメラへのずれを「カメラの向き」で回すので、上下を向いても頭は画面の同じ位置に映る
            Quaternion yawRot = Quaternion.Euler(0f, yaw, 0f);
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 center = Center(yawRot, out Vector3 toPivot);
            Vector3 side = toPivot + new Vector3(shoulder, height, 0f);   // 頭から見た肩の位置（カメラの向きの座標）
            Vector3 shoulderPos = Cast(center, center + rot * side);
            Vector3 pos = Cast(shoulderPos, shoulderPos + rot * Vector3.back * distance);
            transform.SetPositionAndRotation(pos, rot);
        }

        /// 回転の中心（頭の骨）と、そこから従来の回転の中心（足元＋pivotOffset）へのずれ。
        /// ずれは最初のフレーム（立ち姿勢）で覚えて固定する
        Vector3 Center(Quaternion yawRot, out Vector3 toPivot)
        {
            Vector3 standing = target.position + pivotOffset;
            if (followBone == null)
            {
                toPivot = Vector3.zero;
                return standing;
            }
            if (!boneOffsetReady)
            {
                boneToPivot = Quaternion.Inverse(yawRot) * (standing - followBone.position);
                boneOffsetReady = true;
            }
            toPivot = boneToPivot;
            return followBone.position;
        }

        Vector3 Cast(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            float len = d.magnitude;
            if (len < 0.001f) return to;
            Vector3 dir = d / len;
            if (Physics.SphereCast(from, collisionRadius, dir, out var hit, len, Layers.CameraMask, QueryTriggerInteraction.Ignore))
                return from + dir * Mathf.Max(0f, hit.distance - 0.05f);
            return to;
        }
    }
}
