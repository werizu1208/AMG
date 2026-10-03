using UnityEngine;
using UnityEngine.InputSystem;

namespace AMG
{
    /// α版の画面表示（IMGUI）。出撃前のウルト選択・戦闘中HUD・結果画面
    public class HUD : MonoBehaviour
    {
        PlayerHealth player;
        PlayerController controller;
        WeaponSystem weapons;
        GadgetSystem gadgets;
        UltSystem ult;
        BossController boss;

        float hitMarkerUntil;
        float weakPointMarkerUntil;
        bool hitEffective;
        GUIStyle label, labelRight, center, title, subtitle, button, dangerMark;

        void OnEnable()
        {
            CombatEvents.PlayerDealtDamage += OnPlayerHit;
            CombatEvents.PlayerHitWeakPoint += OnWeakPointHit;
        }

        void OnDisable()
        {
            CombatEvents.PlayerDealtDamage -= OnPlayerHit;
            CombatEvents.PlayerHitWeakPoint -= OnWeakPointHit;
        }

        void OnPlayerHit(float dealt)
        {
            hitMarkerUntil = Time.time + 0.12f;
            hitEffective = dealt > 0.01f;
        }

        void OnWeakPointHit()
        {
            weakPointMarkerUntil = Time.time + 0.15f;
        }

        void Start()
        {
            player = PlayerHealth.I;
            if (player != null)
            {
                controller = player.GetComponent<PlayerController>();
                weapons = player.GetComponent<WeaponSystem>();
                gadgets = player.GetComponent<GadgetSystem>();
                ult = player.GetComponent<UltSystem>();
            }
            boss = FindFirstObjectByType<BossController>();
        }

        void Update()
        {
            var gm = GameManager.I;
            var kb = Keyboard.current;
            if (gm == null || kb == null || gm.State != GameState.Loadout) return;
            if (kb.digit1Key.wasPressedThisFrame) gm.BeginBattle(UltType.AirLayer);
            else if (kb.digit2Key.wasPressedThisFrame) gm.BeginBattle(UltType.HumanWisdom);
        }

        float S => Screen.height / 1080f;

        void BuildStyles()
        {
            int size = Mathf.RoundToInt(22 * S);
            label = new GUIStyle(GUI.skin.label) { fontSize = size, wordWrap = false };
            label.normal.textColor = Color.white;
            labelRight = new GUIStyle(label) { alignment = TextAnchor.MiddleRight };
            center = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            title = new GUIStyle(center) { fontSize = Mathf.RoundToInt(64 * S), fontStyle = FontStyle.Bold };
            subtitle = new GUIStyle(center) { fontSize = Mathf.RoundToInt(30 * S) };
            button = new GUIStyle(GUI.skin.button) { fontSize = size, wordWrap = true, alignment = TextAnchor.MiddleCenter };
            dangerMark = new GUIStyle(center) { fontSize = Mathf.RoundToInt(40 * S), fontStyle = FontStyle.Bold };
        }

        void OnGUI()
        {
            var gm = GameManager.I;
            if (gm == null || player == null) return;
            BuildStyles();

            switch (gm.State)
            {
                case GameState.Loadout:
                    DrawLoadout(gm);
                    break;
                case GameState.Playing:
                    DrawBattle();
                    break;
                case GameState.Dead:
                    DrawBattle();
                    DrawResult("K.I.A.", "部隊は全滅した", new Color(0.6f, 0.05f, 0.05f, 0.6f));
                    break;
                case GameState.Victory:
                    DrawBattle();
                    DrawResult("撃破", $"魔法少女を殲滅した　討伐時間 {FormatTime(gm.BattleTime)}", new Color(0.05f, 0.1f, 0.05f, 0.6f));
                    break;
            }
        }

        // ---------- 出撃前 ----------

        void DrawLoadout(GameManager gm)
        {
            float w = Screen.width, h = Screen.height;
            Fill(new Rect(0, 0, w, h), new Color(0f, 0f, 0f, 0.75f));
            GUI.Label(new Rect(0, h * 0.08f, w, 80 * S), "対魔法少女殲滅部隊 A・M・G", title);
            GUI.Label(new Rect(0, h * 0.17f, w, 40 * S), "α版　ステージ1：植物に埋もれた村", subtitle);
            GUI.Label(new Rect(0, h * 0.25f, w, 40 * S), "ウルトを選択して出撃（クリック または 1 / 2 キー）", center);

            float bw = 560 * S, bh = 220 * S, gap = 40 * S;
            float x = (w - bw * 2 - gap) * 0.5f, y = h * 0.32f;
            if (GUI.Button(new Rect(x, y, bw, bh), $"[1] {UltSystem.DisplayName(UltType.AirLayer)}\n\n{UltSystem.Description(UltType.AirLayer)}", button))
                gm.BeginBattle(UltType.AirLayer);
            if (GUI.Button(new Rect(x + bw + gap, y, bw, bh), $"[2] {UltSystem.DisplayName(UltType.HumanWisdom)}\n\n{UltSystem.Description(UltType.HumanWisdom)}", button))
                gm.BeginBattle(UltType.HumanWisdom);

            string controls =
                "WASD 移動　Shift ダッシュ　Space ジャンプ　Ctrl 回避\n" +
                "右クリック エイム　左クリック 射撃　R リロード　1/2 武器切替\n" +
                "E 長押しでグレネードを構え、離して投擲　F 回復キット　Q ウルト　Esc カーソル解放\n\n" +
                "瘤（こぶ）が残っている限り、魔法少女は再生し続ける。\nソロ出撃：HPが0になると即死。";
            GUI.Label(new Rect(0, y + bh + 40 * S, w, 260 * S), controls, center);
        }

        // ---------- 戦闘中 ----------

        void DrawBattle()
        {
            float w = Screen.width, h = Screen.height, s = S;
            DrawWorldHealthBars(s);
            DrawCrosshair(w * 0.5f, h * 0.5f, s);

            // 被弾時に画面の縁を赤く
            float hurt = Mathf.Clamp01(1f - (Time.time - player.LastHitTime) / 0.4f);
            if (hurt > 0f) Fill(new Rect(0, 0, w, h), new Color(0.6f, 0f, 0f, 0.25f * hurt));

            // プレイヤーHP・シールド
            Rect hpRect = new Rect(40 * s, h - 90 * s, 420 * s, 26 * s);
            Bar(hpRect, player.Hp / player.maxHp, new Color(0.75f, 0.15f, 0.12f));
            if (player.MaxShield > 0f)
                Bar(new Rect(hpRect.x, hpRect.y - 14 * s, hpRect.width, 10 * s), player.Shield / player.MaxShield, new Color(0.5f, 0.85f, 1f));
            GUI.Label(new Rect(hpRect.x, hpRect.y - 44 * s, 400 * s, 30 * s), $"HP {Mathf.CeilToInt(player.Hp)} / {player.maxHp:0}", label);

            // 武器・ガジェット
            var wpn = weapons.Current;
            string ammo = wpn.infiniteAmmo ? "∞"
                : weapons.IsReloading ? $"リロード中 {weapons.ReloadProgress * 100f:0}%" : $"{weapons.Ammo} / {wpn.magSize}";
            Rect right = new Rect(w - 520 * s, h - 150 * s, 480 * s, 30 * s);
            GUI.Label(right, $"[{weapons.CurrentIndex + 1}] {wpn.name}", labelRight);
            GUI.Label(new Rect(right.x, right.y + 34 * s, right.width, 30 * s), ammo, labelRight);
            string heal = gadgets.IsHealing ? "（回復中）" : "";
            string grenade = gadgets.GrenadeReady ? "OK" : $"{gadgets.GrenadeCooldownRemaining:0}s";
            string medkit = gadgets.MedkitReady ? "OK" : $"{gadgets.MedkitCooldownRemaining:0}s";
            GUI.Label(new Rect(right.x, right.y + 68 * s, right.width, 30 * s),
                $"[E] グレネード {grenade}　[F] 回復キット {medkit}{heal}", labelRight);

            // ウルト
            Rect ultRect = new Rect(w * 0.5f - 200 * s, h - 70 * s, 400 * s, 18 * s);
            string ultText;
            if (ult.IsActive)
            {
                Bar(ultRect, ult.ActiveRemaining / ult.ActiveDuration, new Color(1f, 0.8f, 0.3f));
                ultText = $"{UltSystem.DisplayName(ult.Type)} 発動中 {ult.ActiveRemaining:0.0}s";
            }
            else
            {
                Bar(ultRect, ult.Charge, ult.Charge >= 1f ? new Color(1f, 0.85f, 0.2f) : new Color(0.6f, 0.6f, 0.65f));
                ultText = ult.Charge >= 1f ? $"[Q] {UltSystem.DisplayName(ult.Type)} 使用可能" : $"{UltSystem.DisplayName(ult.Type)} {ult.Charge * 100f:0}%";
            }
            GUI.Label(new Rect(ultRect.x, ultRect.y - 34 * s, ultRect.width, 30 * s), ultText, center);

            if (controller.IsStunned)
                GUI.Label(new Rect(0, h * 0.58f, w, 40 * s), "スタン！", subtitle);

            DrawBoss(w, s);
            DrawDangerWarning(w, s);

            // ボスの片言
            var voice = BossVoice.I;
            string line = voice != null ? voice.Visible : null;
            if (!string.IsNullOrEmpty(line))
                GUI.Label(new Rect(0, h - 190 * s, w, 50 * s), $"「{line}」", subtitle);

            if (GameManager.I.State == GameState.Playing && !GameManager.CursorLocked)
                GUI.Label(new Rect(0, h * 0.4f, w, 40 * s), "クリックで操作に戻る", subtitle);
        }

        void DrawBoss(float w, float s)
        {
            if (boss == null) return;
            Rect bar = new Rect(w * 0.5f - 450 * s, 70 * s, 900 * s, 22 * s);
            GUI.Label(new Rect(bar.x, bar.y - 36 * s, bar.width, 32 * s), boss.bossName, center);

            Color hpColor = boss.IsReviving ? new Color(0.9f, 0.9f, 0.9f) : new Color(0.45f, 0.6f, 0.25f);
            Bar(bar, boss.Hp / boss.maxHp, hpColor);
            // フェーズ2移行ラインの目盛り
            Fill(new Rect(bar.x + bar.width * boss.phase2Threshold - 1 * s, bar.y - 4 * s, 2 * s, bar.height + 8 * s), new Color(1f, 1f, 1f, 0.6f));

            string status = $"フェーズ{boss.Phase}　ダメージ軽減 {boss.DamageReduction * 100f:0}%　瘤 残り {boss.KnotsRemaining}/{boss.KnotsTotal}";
            if (boss.IsRegenerating) status += "　<再生中>";
            if (boss.IsReviving) status += "　<無敵：再生>";
            if (boss.IsTransforming) status += "　<変異中>";
            if (boss.IsBurrowed) status += "　<地中>";
            GUI.Label(new Rect(bar.x, bar.y + 26 * s, bar.width, 30 * s), status, center);
        }

        /// ボスの攻撃予兆の範囲内にいる間、画面中央上部（ボスHPバーの下）に点滅する危険マークを出す
        void DrawDangerWarning(float w, float s)
        {
            if (GameManager.I.State != GameState.Playing || !player.IsAlive) return;
            if (!DangerZone.IsInside(player.transform.position)) return;

            float blink = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 10f));
            float size = 46f * s;
            float cx = w * 0.5f, cy = 175f * s;

            // 赤い菱形に「！」
            var prev = GUI.matrix;
            GUIUtility.RotateAroundPivot(45f, new Vector2(cx, cy));
            Fill(new Rect(cx - size * 0.5f - 3f * s, cy - size * 0.5f - 3f * s, size + 6f * s, size + 6f * s), new Color(0f, 0f, 0f, 0.6f * blink));
            Fill(new Rect(cx - size * 0.5f, cy - size * 0.5f, size, size), new Color(0.9f, 0.1f, 0.05f, blink));
            GUI.matrix = prev;
            GUI.Label(new Rect(cx - size, cy - size * 0.5f, size * 2f, size), "!", dangerMark);
            GUI.Label(new Rect(0, cy + size * 0.6f, w, 30 * s), "危険：攻撃範囲内", center);
        }

        const float WorldBarMaxDistance = 60f;
        /// HPが減っていない敵は、プレイヤーからこの距離以内のときだけバーを出す
        const float WorldBarFullHpDistance = 15f;

        /// 瘤・雑魚の頭上HPバー。遠いほど小さく、カメラの後ろにあるものは描かない
        void DrawWorldHealthBars(float s)
        {
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 playerPos = player.transform.position;
            foreach (var t in HealthBars.Targets)
            {
                if (!t.IsAlive) continue;
                Vector3 anchor = t.BarAnchor;
                bool damaged = t.Hp01 < 0.999f;
                if (!damaged && (anchor - playerPos).sqrMagnitude > WorldBarFullHpDistance * WorldBarFullHpDistance) continue;

                Vector3 sp = cam.WorldToScreenPoint(anchor);
                if (sp.z <= 0.1f || sp.z > WorldBarMaxDistance) continue;

                float scale = Mathf.Lerp(1f, 0.45f, sp.z / WorldBarMaxDistance) * s;
                float bw = 110f * scale, bh = Mathf.Max(4f, 11f * scale);
                // IMGUIは上が原点なのでyを反転
                var r = new Rect(sp.x - bw * 0.5f, Screen.height - sp.y - bh, bw, bh);
                Fill(new Rect(r.x - 1f, r.y - 1f, r.width + 2f, r.height + 2f), new Color(0f, 0f, 0f, 0.8f));
                Bar(r, t.Hp01, t is Knot ? new Color(0.95f, 0.65f, 0.15f) : new Color(0.85f, 0.2f, 0.15f));
            }
        }

        void DrawCrosshair(float cx, float cy, float s)
        {
            float gap = (controller.IsAiming ? 4f : 10f) * s, len = 8 * s, th = 2 * s;
            var c = new Color(1f, 1f, 1f, 0.85f);
            Fill(new Rect(cx - th * 0.5f, cy - gap - len, th, len), c);
            Fill(new Rect(cx - th * 0.5f, cy + gap, th, len), c);
            Fill(new Rect(cx - gap - len, cy - th * 0.5f, len, th), c);
            Fill(new Rect(cx + gap, cy - th * 0.5f, len, th), c);

            if (Time.time < hitMarkerUntil)
            {
                // 弱点に当たったときは赤く、少し大きく
                bool weak = Time.time < weakPointMarkerUntil;
                var hc = weak ? new Color(1f, 0.3f, 0.2f) : hitEffective ? Color.white : new Color(0.5f, 0.5f, 0.5f);
                float size = (weak ? 18f : 14f) * s;
                var prev = GUI.matrix;
                GUIUtility.RotateAroundPivot(45f, new Vector2(cx, cy));
                Fill(new Rect(cx - size, cy - th * 0.5f, size * 2f, th), hc);
                Fill(new Rect(cx - th * 0.5f, cy - size, th, size * 2f), hc);
                GUI.matrix = prev;
            }
        }

        // ---------- 結果 ----------

        void DrawResult(string head, string body, Color bg)
        {
            float w = Screen.width, h = Screen.height;
            Fill(new Rect(0, 0, w, h), bg);
            GUI.Label(new Rect(0, h * 0.35f, w, 90 * S), head, title);
            GUI.Label(new Rect(0, h * 0.47f, w, 50 * S), body, subtitle);
            GUI.Label(new Rect(0, h * 0.55f, w, 40 * S), "R キーでリトライ", center);
        }

        // ---------- 描画ヘルパー ----------

        static void Fill(Rect r, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        static void Bar(Rect r, float t, Color c)
        {
            Fill(r, new Color(0f, 0f, 0f, 0.6f));
            Fill(new Rect(r.x, r.y, r.width * Mathf.Clamp01(t), r.height), c);
        }

        static string FormatTime(float t) => $"{(int)(t / 60f):00}:{t % 60f:00.0}";
    }
}
