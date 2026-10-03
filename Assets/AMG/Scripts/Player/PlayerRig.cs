using UnityEngine;

namespace AMG
{
    /// プレイヤー（特殊部隊員）のプロシージャルアニメーション。ボスと同じく、基本形状の骨を二関節IKで動かす。
    /// ・足は地面に接地し、移動に合わせて一歩ずつ踏み出す。ジャンプ中は足を引き上げる
    /// ・両手は小銃をIKで保持する。構え中（エイム・射撃直後）は画面中央の照準点へ銃口を向け、
    ///   普段は銃口を下げたローレディ姿勢、ダッシュ中は銃を体に引き寄せる
    /// ・上半身は照準の上下に合わせて傾き、回避中はかがむ
    /// ・銃ごとにモデルと握り方（GripPose：手首の位置・向き、指の曲げ）を持つ。握り方は「A・M・G > 握り方エディタ」で調整する
    [DefaultExecutionOrder(50)] // プレイヤーの移動とカメラの後に姿勢を決める
    public class PlayerRig : MonoBehaviour
    {
        /// 銃1種類ぶんの見た目と握り方（WeaponSystem の武器と同じ順番）
        [System.Serializable]
        public class WeaponVisual
        {
            public string label;
            [Tooltip("銃のモデル（原点＝後端、+Z が銃口方向）")]
            public GameObject model;
            [Tooltip("握り方。空なら従来の簡易な持ち方")]
            public GripPose grip;
            [Tooltip("構えたとき、胸から見た銃の原点の位置（体の右・上・前、m）")]
            public Vector3 holdOffset = new Vector3(0.14f, 0.04f, 0.06f);
            [Tooltip("銃口の位置（銃のローカル座標）。弾道エフェクトの出る場所")]
            public Vector3 muzzle = new Vector3(0f, 0.03f, 0.9f);
        }

        [Header("骨")]
        public Transform hips;
        public Transform spine;
        public Transform chest;
        public Transform head;
        public Transform legLUpper, legLLower, footL;
        public Transform legRUpper, legRLower, footR;
        public Transform armLUpper, armLLower, handL;
        public Transform armRUpper, armRLower, handR;

        [Header("小銃（原点＝床尾。+Z が銃口方向）")]
        public Transform rifle;
        public float rightHandOnRifle = 0.24f;   // 床尾から右手（グリップ）まで
        public float leftHandOnRifle = 0.50f;    // 床尾から左手（ハンドガード）まで

        [Header("銃ごとの見た目と握り方（WeaponSystem の武器の順番）")]
        public WeaponVisual[] weaponVisuals = new WeaponVisual[0];

        [Header("歩行")]
        public float hipHeight = 0.95f;
        [Tooltip("立っているときに腰を下げる量（膝を少し曲げて、脚が伸び切らないようにする）")]
        public float hipDrop = 0.05f;
        [Tooltip("しゃがんだときに腰を下げる量（m）")]
        public float crouchDepth = 0.38f;
        [Tooltip("しゃがんだときに上半身を前へ傾ける角度（度）")]
        public float crouchLean = 15f;
        [Tooltip("全力疾走のときにさらに腰を下げる量")]
        public float runHipDrop = 0.07f;
        public float footSpacing = 0.12f;
        [Tooltip("1秒あたりの歩数（歩き → 全力疾走）。脚が届かない速さのときは自動で増える")]
        public float walkCadence = 2.2f;
        public float sprintCadence = 3.4f;
        public float maxCadence = 6f;
        [Tooltip("1歩のうち足が地面についている割合（歩き → 全力疾走）")]
        [Range(0.2f, 0.8f)] public float walkStanceRatio = 0.6f;
        [Range(0.2f, 0.8f)] public float sprintStanceRatio = 0.32f;
        public float stepHeight = 0.12f;
        public float runStepHeight = 0.25f;
        [Tooltip("脚の長さに対して、どこまで前後に踏み出してよいか")]
        [Range(0.5f, 1f)] public float reachMargin = 0.85f;
        [Tooltip("止まっているとき、足がこれ以上ずれたら（位置・向き）踏み直す")]
        public float settleDistance = 0.1f;
        public float settleAngle = 35f;

        [Header("姿勢")]
        public float headYawLimit = 70f;
        [Range(0f, 1f)] public float spinePitchFollow = 0.5f;   // 照準の上下を上半身がどれだけ追うか
        [Tooltip("再生していないときの腕の開き（真下からの角度。45 で Aポーズ、90 で Tポーズ）")]
        [Range(0f, 90f)] public float restArmAngle = 45f;

        PlayerController controller;
        PlayerHealth health;
        WeaponSystem weapons;
        GameObject[] weaponModels = new GameObject[0];
        int shownWeapon = -1;
        int previewIndex = -1;   // 握り方エディタで編集中の武器（再生していないとき）

        const float AnkleHeight = 0.08f;
        const float MoveThreshold = 0.25f;   // これより遅ければ「止まっている」

        // 足：接地中は planted に固定、振り出し中は swingFrom → swingTo へ弧を描いて移動
        readonly Vector3[] planted = new Vector3[2];
        readonly float[] plantedYaw = new float[2];
        readonly bool[] swinging = new bool[2];
        readonly float[] swingT = new float[2];
        readonly Vector3[] swingFrom = new Vector3[2];
        readonly float[] swingFromYaw = new float[2];
        readonly Vector3[] swingTo = new Vector3[2];
        readonly bool[] wasSwingPhase = new bool[2];

        /// 歩行周期（0〜1で2歩）。左足は0から、右足は0.5から振り出し始める
        float gaitPhase;
        float speed01;        // 全力疾走に対する速さ（0〜1）
        float moveBlend;      // 止まっている 0 → 動いている 1（なめらかに変化）
        float stanceRatio = 0.6f;
        float cadence = 2f;
        float legLength;

        Vector3 lastPos;
        Vector3 velocity;
        float crouch;
        Quaternion spineRot = Quaternion.identity;
        Vector3 aimDir;
        float airTuck;

        Transform Body => controller != null ? controller.transform : transform;

        void Start()
        {
            controller = GetComponentInParent<PlayerController>();
            health = GetComponentInParent<PlayerHealth>();
            weapons = GetComponentInParent<WeaponSystem>();
            EnsureWeaponModels();
            ResetFeet();
            lastPos = Body.position;
            aimDir = Body.forward;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || controller == null) return;
            if (health != null && !health.IsAlive) return;   // 倒れたら姿勢を止める

            velocity = Vector3.Lerp(velocity, (Body.position - lastPos) / dt, 12f * dt);
            lastPos = Body.position;

            if (CurrentIndex != shownWeapon) ShowWeapon(CurrentIndex);
            UpdateFeet(dt);
            UpdateBody(dt);
            UpdateRifle(dt);
            UpdateArms();
            UpdateLegs();
            UpdateHead();
        }

#if UNITY_EDITOR
        /// シーンを開いたとき・スクリプトの再コンパイル後・Inspectorで値を変えたときに、基本姿勢へ戻す
        void OnValidate()
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null && hips != null && !UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) ApplyRestPose();
            };
        }
#endif

        /// 再生していないときの基本姿勢（脚はまっすぐ、腕は restArmAngle だけ開く。小銃は背中に背負う）。
        /// エディタが編集モードで自動的に呼ぶ。再生中はIKがすべて上書きする
        public void ApplyRestPose()
        {
            controller = GetComponentInParent<PlayerController>();
            Transform body = Body;
            Vector3 up = body.up, fwd = body.forward, right = body.right;

            hips.localPosition = new Vector3(0f, hipHeight, 0f);
            hips.localRotation = Quaternion.identity;
            spine.localRotation = Quaternion.identity;
            chest.localRotation = Quaternion.identity;
            head.rotation = Quaternion.LookRotation(fwd, up);

            RestPose.Straight(legLUpper, legLLower, footL, -up, fwd);
            RestPose.Straight(legRUpper, legRLower, footR, -up, fwd);
            footL.rotation = footR.rotation = Quaternion.LookRotation(fwd, up);
            RestPose.PlantFeet(hips, footL, body, AnkleHeight);

            RestPose.Straight(armLUpper, armLLower, handL, RestPose.ArmDirection(body, -1f, restArmAngle), -fwd);
            RestPose.Straight(armRUpper, armRLower, handR, RestPose.ArmDirection(body, 1f, restArmAngle), -fwd);

            // 床尾を右腰の後ろに、銃口を左肩の上へ向けて斜めに背負う
            Vector3 stock = chest.position - fwd * 0.17f + right * 0.16f - up * 0.4f;
            rifle.SetPositionAndRotation(stock, Quaternion.LookRotation((up * 0.8f - right * 0.6f).normalized, -fwd));

            weapons = GetComponentInParent<WeaponSystem>();
            EnsureWeaponModels();
            ShowWeapon(CurrentIndex);
            FingerRig.For(HandModel(handL))?.Relax();
            FingerRig.For(HandModel(handR))?.Relax();
        }

        /// 握り方エディタ用：再生していないときに、指定した武器を正面へ構えた姿勢にする
        public void PreviewGrip(int index)
        {
            previewIndex = index;
            ApplyRestPose();
            aimDir = Body.forward;
            PlaceRifle(aimDir);
            UpdateArms();
            head.rotation = Quaternion.LookRotation(Body.forward, Vector3.up);
        }

        /// 握り方エディタを閉じたとき：通常の基本姿勢に戻す
        public void EndPreview()
        {
            previewIndex = -1;
            ApplyRestPose();
        }

        // ---------- 銃の見た目 ----------

        public int CurrentIndex
        {
            get
            {
                if (previewIndex >= 0) return previewIndex;
                return weapons != null ? weapons.CurrentIndex : 0;
            }
        }

        public WeaponVisual CurrentVisual => CurrentIndex >= 0 && CurrentIndex < weaponVisuals.Length ? weaponVisuals[CurrentIndex] : null;

        /// 銃のモデルを小銃の骨（rifle）の下に用意する。編集中のものはシーンに保存しない
        void EnsureWeaponModels()
        {
            if (rifle == null) return;
            if (weaponModels.Length != weaponVisuals.Length) weaponModels = new GameObject[weaponVisuals.Length];
            for (int i = 0; i < weaponVisuals.Length; i++)
            {
                string name = "WeaponModel_" + i;
                if (weaponModels[i] == null)
                {
                    var found = rifle.Find(name);
                    if (found != null)
                    {
                        // 再生開始時は、編集中に作った表示用のものを作り直す
                        if (Application.isPlaying && found.gameObject.hideFlags != HideFlags.None) DestroyImmediate(found.gameObject);
                        else weaponModels[i] = found.gameObject;
                    }
                }
                if (weaponModels[i] != null || weaponVisuals[i].model == null) continue;

                var go = Instantiate(weaponVisuals[i].model, rifle);
                go.name = name;
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                Prim.SetLayerRecursive(go, rifle.gameObject.layer);
                foreach (var col in go.GetComponentsInChildren<Collider>()) DestroyImmediate(col);
                if (!Application.isPlaying)
                    foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.HideAndDontSave;
                go.SetActive(false);
                weaponModels[i] = go;
            }
        }

        void ShowWeapon(int index)
        {
            EnsureWeaponModels();
            for (int i = 0; i < weaponModels.Length; i++)
                if (weaponModels[i] != null) weaponModels[i].SetActive(i == index);
            var v = CurrentVisual;
            if (v != null && weapons != null && weapons.muzzle != null) weapons.muzzle.localPosition = v.muzzle;
            shownWeapon = index;
        }

        static GameObject HandModel(Transform hand)
        {
            var slot = hand != null ? hand.GetComponent<VisualSlot>() : null;
            return slot != null ? slot.Instance : null;
        }

        // ---------- 足 ----------

        Vector3 FootHome(int i) => Body.TransformPoint(new Vector3(i == 0 ? -footSpacing : footSpacing, 0f, 0f));

        static Vector3 GroundPoint(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out var hit, 4f, Layers.EnvironmentMask, QueryTriggerInteraction.Ignore))
                return hit.point;
            p.y = Mathf.Min(p.y, 0f);
            return p;
        }

        void ResetFeet()
        {
            float yaw = Body.eulerAngles.y;
            for (int i = 0; i < 2; i++)
            {
                planted[i] = GroundPoint(FootHome(i));
                plantedYaw[i] = yaw;
                swinging[i] = false;
                wasSwingPhase[i] = false;
            }
            gaitPhase = 0f;
        }

        float LegLength
        {
            get
            {
                if (legLength <= 0f)
                    legLength = Vector3.Distance(legLUpper.position, legLLower.position) + Vector3.Distance(legLLower.position, footL.position);
                return legLength;
            }
        }

        /// 今の腰の高さで、足を真下から水平方向にどこまで離せるか（それ以上は脚が伸び切る）
        float MaxReach()
        {
            float h = legLUpper.position.y - (GroundPoint(Body.position).y + AnkleHeight);
            float l = LegLength * 0.98f;
            return Mathf.Sqrt(Mathf.Max(0f, l * l - h * h));
        }

        /// 接地中の足が脚の届かない位置に取り残されないよう、真下からの水平距離を制限する（届かない分は滑らせる）
        Vector3 ClampToReach(Vector3 p, int i, float reach)
        {
            Vector3 home = FootHome(i);
            Vector3 offset = Flat(p - home);
            if (offset.magnitude <= reach) return p;
            Vector3 clamped = home + offset.normalized * reach;
            clamped.y = p.y;
            return clamped;
        }

        bool NeedsSettle(int i)
        {
            return Flat(planted[i] - FootHome(i)).magnitude > settleDistance
                || Mathf.Abs(Mathf.DeltaAngle(plantedYaw[i], Body.eulerAngles.y)) > settleAngle;
        }

        /// 歩行周期に合わせて左右交互に足を振り出す。接地中の足は地面に固定（滑らない）。
        /// 歩幅と歩数は速さで変わり、着地点は脚の届く範囲に収める
        void UpdateFeet(float dt)
        {
            bool airborne = !controller.IsGrounded && Body.position.y > GroundPoint(Body.position).y + 0.15f;
            airTuck = Mathf.MoveTowards(airTuck, airborne ? 1f : 0f, dt * 6f);

            if (airborne)
            {
                // 空中では足を体についてこさせる（着地した場所から改めて接地する）
                ResetFeet();
                return;
            }

            Vector3 flatVel = Flat(velocity);
            float speed = flatVel.magnitude;
            bool moving = speed > MoveThreshold;
            speed01 = Mathf.Clamp01(speed / Mathf.Max(0.1f, controller.sprintSpeed));
            moveBlend = Mathf.MoveTowards(moveBlend, moving ? 1f : 0f, dt * 4f);
            stanceRatio = Mathf.Lerp(walkStanceRatio, sprintStanceRatio, speed01);

            // 着地点の前方距離 = 速さ × 接地割合 ÷ 歩数。これが脚の届く範囲を超えないよう、速いときは歩数を増やす
            float reach = MaxReach();
            float maxLanding = Mathf.Max(0.05f, reach * reachMargin);
            cadence = Mathf.Lerp(walkCadence, sprintCadence, speed01);
            cadence = Mathf.Min(Mathf.Max(cadence, speed * stanceRatio / maxLanding), maxCadence);

            float cycleRate = cadence * 0.5f;   // 1周期 = 2歩
            float swingDuration = (1f - stanceRatio) / cycleRate;
            float stanceDuration = stanceRatio / cycleRate;

            bool settle = !moving && (NeedsSettle(0) || NeedsSettle(1));
            if (moving || settle || swinging[0] || swinging[1])
                gaitPhase = Mathf.Repeat(gaitPhase + cycleRate * dt, 1f);

            for (int i = 0; i < 2; i++)
            {
                float p = Mathf.Repeat(gaitPhase + i * 0.5f, 1f);
                bool swingPhase = p >= stanceRatio;
                // 周期が振り出しに入った瞬間に足を上げる（止まっていて踏み直しが不要なら上げない）
                if (swingPhase && !wasSwingPhase[i] && !swinging[i] && (moving || NeedsSettle(i)))
                {
                    swinging[i] = true;
                    swingT[i] = 0f;
                    swingFrom[i] = planted[i];
                    swingFromYaw[i] = plantedYaw[i];
                }
                wasSwingPhase[i] = swingPhase;

                if (swinging[i])
                {
                    swingT[i] += dt / swingDuration;
                    // 着地点は毎フレーム更新（曲がる・止まるに追従）。接地時間の真ん中で体の真下に来る位置へ
                    float remaining = Mathf.Max(0f, 1f - swingT[i]) * swingDuration;
                    Vector3 futureHome = FootHome(i) + flatVel * remaining;
                    Vector3 lead = Vector3.ClampMagnitude(flatVel * (stanceDuration * 0.5f), maxLanding);
                    swingTo[i] = GroundPoint(futureHome + lead);
                    if (swingT[i] >= 1f)
                    {
                        swinging[i] = false;
                        planted[i] = swingTo[i];
                        plantedYaw[i] = Body.eulerAngles.y;
                    }
                }
                else planted[i] = ClampToReach(planted[i], i, reach);
            }
        }

        Vector3 FootPosition(int i)
        {
            Vector3 p;
            if (!swinging[i]) p = planted[i];
            else
            {
                float t = Mathf.Clamp01(swingT[i]);
                float s = t * t * (3f - 2f * t);
                float lift = Mathf.Lerp(stepHeight, runStepHeight, speed01) * Mathf.Lerp(0.5f, 1f, moveBlend);
                p = Vector3.Lerp(swingFrom[i], swingTo[i], s) + Vector3.up * Mathf.Sin(t * Mathf.PI) * lift;
            }
            // ジャンプ中は膝を曲げて足を引き上げる
            return Vector3.Lerp(p, FootHome(i) + Vector3.up * (0.35f + i * 0.1f), airTuck);
        }

        /// 接地中は着地したときの向きのまま、振り出し中に体の向きへ回す
        float FootYaw(int i)
        {
            if (!swinging[i]) return plantedYaw[i];
            return Mathf.LerpAngle(swingFromYaw[i], Body.eulerAngles.y, Mathf.Clamp01(swingT[i]));
        }

        void UpdateLegs()
        {
            Vector3 fwd = Body.forward;
            Vector3 ankle = Vector3.up * AnkleHeight;
            SolveLeg(legLUpper, legLLower, footL, FootPosition(0) + ankle, fwd, FootYaw(0));
            SolveLeg(legRUpper, legRLower, footR, FootPosition(1) + ankle, fwd, FootYaw(1));
        }

        void SolveLeg(Transform upper, Transform lower, Transform foot, Vector3 target, Vector3 fwd, float yaw)
        {
            // 膝は常に体の前方へ曲げる
            TwoBoneIK.Solve(upper, lower, foot, target, upper.position + fwd + Vector3.down * 0.3f);
            foot.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        // ---------- 胴体 ----------

        void UpdateBody(float dt)
        {
            float targetCrouch = controller.IsCrouching ? crouchDepth : controller.IsDodging ? 0.28f : controller.IsAiming ? 0.05f : 0f;
            crouch = Mathf.Lerp(crouch, targetCrouch, 12f * dt);

            // 1周期（2歩）で腰が2回上下する。速いほど大きく、腰も低くして膝に余裕を持たせる
            float bob = (0.5f - 0.5f * Mathf.Cos(gaitPhase * Mathf.PI * 4f)) * Mathf.Lerp(0.015f, 0.045f, speed01) * moveBlend;
            float drop = hipDrop + runHipDrop * speed01 * moveBlend;
            hips.localPosition = new Vector3(0f, hipHeight - drop - crouch - bob, 0f);

            // 照準の上下に合わせて上半身を傾ける（構え中のみ）
            float pitch = 0f;
            if (controller.IsCombatReady && TPSCamera.I != null)
                pitch = -Mathf.Asin(Mathf.Clamp(TPSCamera.I.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg * spinePitchFollow;

            Vector3 localVel = Body.InverseTransformDirection(velocity);
            float lean = Mathf.Clamp(localVel.z * 1.5f, -6f, 12f) + (controller.IsDodging ? 20f : 0f);
            // しゃがみ：腰の下がり具合に合わせて上半身を前へ傾ける
            if (controller.IsCrouching && crouchDepth > 0.001f)
                lean += crouchLean * Mathf.Clamp01(crouch / crouchDepth);
            float side = Mathf.Clamp(-localVel.x * 2f, -8f, 8f);
            Quaternion target = Quaternion.Euler(lean + pitch, 0f, side);
            if (controller.IsStunned)
                target *= Quaternion.Euler(Random.Range(-6f, 6f), Random.Range(-6f, 6f), Random.Range(-6f, 6f));
            spineRot = Quaternion.Slerp(spineRot, target, 12f * dt);
            spine.localRotation = spineRot;
        }

        // ---------- 小銃 ----------

        Vector3 LowReadyDirection() => (Body.forward * 0.75f - Vector3.up * 0.65f - Body.right * 0.15f).normalized;

        Vector3 StockPosition()
        {
            Vector3 o = CurrentVisual != null ? CurrentVisual.holdOffset : new Vector3(0.14f, 0.04f, 0.06f);
            return chest.position + Body.right * o.x + Vector3.up * o.y + Body.forward * o.z;
        }

        void UpdateRifle(float dt)
        {
            Vector3 desired;
            if (controller.IsCombatReady && TPSCamera.I != null)
            {
                // 画面中央の照準点へ銃口を向ける
                var cam = TPSCamera.I.transform;
                Vector3 point = Physics.Raycast(cam.position, cam.forward, out var hit, 200f, Layers.ShootMask, QueryTriggerInteraction.Ignore)
                    ? hit.point
                    : cam.position + cam.forward * 200f;
                desired = (point - StockPosition()).normalized;
            }
            else if (controller.IsSprinting)
                desired = (Body.forward * 0.35f - Vector3.up * 0.8f - Body.right * 0.45f).normalized;
            else
                desired = LowReadyDirection();

            float follow = controller.IsCombatReady ? 25f : 8f;
            aimDir = Vector3.Slerp(aimDir, desired, 1f - Mathf.Exp(-follow * dt));
            PlaceRifle(aimDir);
        }

        void PlaceRifle(Vector3 dir)
        {
            Vector3 up = Vector3.ProjectOnPlane(Vector3.up, dir);
            if (up.sqrMagnitude < 1e-4f) up = Body.forward;
            rifle.SetPositionAndRotation(StockPosition(), Quaternion.LookRotation(dir, up));
        }

        // ---------- 腕 ----------

        void UpdateArms()
        {
            var grip = CurrentVisual != null ? CurrentVisual.grip : null;
            if (grip != null)
            {
                SolveHand(armRUpper, armRLower, handR, grip.right, 1f);
                SolveHand(armLUpper, armLLower, handL, grip.left, -1f);
                return;
            }

            Vector3 right = Body.right;
            Vector3 down = -rifle.up;
            Vector3 rightGrip = rifle.position + rifle.forward * rightHandOnRifle + down * 0.06f;
            Vector3 leftGrip = rifle.position + rifle.forward * leftHandOnRifle + down * 0.05f;
            // 肘は下・外へ
            TwoBoneIK.Solve(armRUpper, armRLower, handR, rightGrip, chest.position + right * 0.5f + Vector3.down * 0.6f);
            TwoBoneIK.Solve(armLUpper, armLLower, handL, leftGrip, chest.position - right * 0.45f + Vector3.down * 0.6f);
        }

        /// 握り方どおりに、手首を銃の決まった位置・向きへ合わせ、指を曲げる。持たない手は体の横へ下ろす
        void SolveHand(Transform upper, Transform lower, Transform hand, GripPose.HandGrip g, float side)
        {
            Vector3 right = Body.right;
            Vector3 pole = chest.position + right * side * 0.5f + Vector3.down * 0.6f;
            var fingers = FingerRig.For(HandModel(hand));
            if (g.holding)
            {
                TwoBoneIK.Solve(upper, lower, hand, rifle.TransformPoint(g.position), pole);
                hand.rotation = rifle.rotation * Quaternion.Euler(g.rotation);
                if (fingers != null) fingers.Apply(g);
            }
            else
            {
                TwoBoneIK.Solve(upper, lower, hand, chest.position + right * side * 0.24f + Vector3.down * 0.55f + Body.forward * 0.03f, pole);
                if (fingers != null) fingers.Relax();
            }
        }

        // ---------- 首 ----------

        void UpdateHead()
        {
            Vector3 look = controller.IsCombatReady ? aimDir : Body.forward;
            Vector3 flatBody = Flat(Body.forward);
            Vector3 flatLook = Flat(look);
            if (flatLook.sqrMagnitude > 1e-4f)
            {
                float angle = Vector3.SignedAngle(flatBody, flatLook, Vector3.up);
                float clamped = Mathf.Clamp(angle, -headYawLimit, headYawLimit);
                look = Quaternion.AngleAxis(clamped - angle, Vector3.up) * look;
            }
            head.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
