using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AMG
{
    /// 拠点のメニュー。左に縦並びのボタン（作戦開始 / 装備・強化・変更 / 武器・作成・研究 / やめる）。
    /// ・ボタンにカーソルを合わせると、その部屋の「合わせたとき」のカメラ位置に切り替わる（どれにも合っていないときは拠点を見下ろす位置）
    /// ・ボタンを押すと「クリックしたとき」のカメラ位置へ移ってその部屋に入り、3Dオブジェクトをクリックして選ぶ
    ///   （同じ部屋の中ならなめらかに動き、部屋をまたぐときは一瞬暗転して切り替える）
    ///   - 作戦開始：模型の地図の上の印（ステージ）をクリック → 出撃
    ///   - 装備・強化・変更 / 武器・作成・研究：まだ中身はなく「準備中」
    /// ・やめる：画面が切れる演出のあと、ゲームを終了する
    /// カメラ位置・地図などは、シーン上のオブジェクトを名前で自動的に探す（Inspector で指定すればそちらを使う）
    public class BasecampMenu : MonoBehaviour
    {
        enum Page { Main, Operation, Equipment, Research, Quitting }

        [Header("カメラ位置（空なら名前で探す）")]
        public Camera mainCamera;
        [Tooltip("CameraFirstPOtison：拠点を見下ろす（最初の位置・やめる）")]
        public Transform overviewPoint;

        [Header("ボタンにカーソルを合わせたとき")]
        [Tooltip("CameaPotision：作戦室")] public Transform operationHover;
        [Tooltip("CameaPotision (1)：ウェポンラックと作業台")] public Transform equipmentHover;
        [Tooltip("CameaPotision (2)：研究室")] public Transform researchHover;

        [Header("ボタンをクリックしたとき（空ならカーソルを合わせたときと同じ位置）")]
        [Tooltip("CameraEnter_Operation")] public Transform operationEnter;
        [Tooltip("CameraEnter_Equipment")] public Transform equipmentEnter;
        [Tooltip("CameraEnter_Research")] public Transform researchEnter;

        [Header("作戦室の地図（空なら MapTable の子の球をステージ1〜7として使う）")]
        public Transform mapTable;
        public Transform[] stageMarkers;

        [Header("演出")]
        public float fadeTime = 0.15f;
        public float quitFadeTime = 0.8f;
        [Tooltip("この距離より近い位置へはなめらかに動く（遠いときは別の部屋とみなして暗転して切り替える）")]
        public float glideDistance = 8f;
        public float glideTime = 0.6f;

        Page page = Page.Main;
        StageCatalog.Stage selected;
        StageCatalog.Stage hovered;
        string objectMessage;
        Transform currentPoint;
        Transform pendingPoint;
        float fade;            // 0=見える 1=真っ暗
        bool fadingOut;
        float quitTimer;
        Vector3 glideFromPos;
        Quaternion glideFromRot;
        float glideT = 1f;
        readonly Dictionary<Transform, StageCatalog.Stage> markerStage = new Dictionary<Transform, StageCatalog.Stage>();
        readonly Dictionary<Transform, Vector3> markerScale = new Dictionary<Transform, Vector3>();
        MaterialPropertyBlock block;
        GUIStyle title, subtitle, label, labelSmall, button, menuButton;

        void Start()
        {
            GameManager.SetCursorLocked(false);
            if (mainCamera == null) mainCamera = Camera.main;
            // シーンの目印：
            //   合わせたとき … CameraSelect の下の CameraRoom / CameraRoom (1) / CameraRoom (2) の子（作戦開始・装備・研究の順）
            //   クリックしたとき … 各部屋の中の CameaPotision（StageSelectRoom の子）/ CameaPotision (1) / CameaPotision (2)
            // 同じ名前（CameaPotision）が複数あるので、親の名前で区別して探す
            if (overviewPoint == null) overviewPoint = FindNamed("CameraFirstPOtison");
            if (operationHover == null) operationHover = FindChildOf("CameraRoom");
            if (equipmentHover == null) equipmentHover = FindChildOf("CameraRoom (1)");
            if (researchHover == null) researchHover = FindChildOf("CameraRoom (2)");
            if (operationEnter == null) operationEnter = FindNamed("CameraEnter_Operation") ?? FindChildOf("StageSelectRoom", "CameaPotision");
            if (equipmentEnter == null) equipmentEnter = FindNamed("CameraEnter_Equipment") ?? FindNamed("CameaPotision (1)");
            if (researchEnter == null) researchEnter = FindNamed("CameraEnter_Research") ?? FindNamed("CameaPotision (2)");
            if (mapTable == null) mapTable = FindNamed("MapTable");
            SetupMarkers();
            MoveTo(overviewPoint);
        }

        static Transform FindNamed(string name)
        {
            var go = GameObject.Find(name);
            return go != null ? go.transform : null;
        }

        /// parentName という名前のオブジェクトの子を探す（childName が空なら最初の子。子がなければそのオブジェクト自身）
        static Transform FindChildOf(string parentName, string childName = null)
        {
            foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name != parentName) continue;
                if (childName == null) return t.childCount > 0 ? t.GetChild(0) : t;
                var c = t.Find(childName);
                if (c != null) return c;
            }
            return null;
        }

        // ---------- 地図の印（ステージ） ----------

        void SetupMarkers()
        {
            block = new MaterialPropertyBlock();
            if ((stageMarkers == null || stageMarkers.Length == 0) && mapTable != null)
            {
                var list = new List<Transform>();
                foreach (Transform c in mapTable)
                    if (c.GetComponent<Collider>() != null) list.Add(c);
                // 「Sphere」「Sphere (1)」…「Sphere (6)」の順に並べて、ステージ1〜7にする
                list.Sort((a, b) => NameOrder(a.name).CompareTo(NameOrder(b.name)));
                stageMarkers = list.ToArray();
            }
            if (stageMarkers == null) return;
            for (int i = 0; i < stageMarkers.Length && i < StageCatalog.Stages.Length; i++)
            {
                var m = stageMarkers[i];
                if (m == null) continue;
                markerStage[m] = StageCatalog.Stages[i];
                markerScale[m] = m.localScale;
                Tint(m, MarkerColor(StageCatalog.Stages[i]));
            }
        }

        static int NameOrder(string name)
        {
            int open = name.LastIndexOf('('), close = name.LastIndexOf(')');
            if (open >= 0 && close > open && int.TryParse(name.Substring(open + 1, close - open - 1), out int n)) return n + 1;
            return 0;
        }

        static Color MarkerColor(StageCatalog.Stage s) =>
            !s.IsAvailable ? new Color(0.45f, 0.43f, 0.40f)
            : s.IsCleared ? new Color(0.30f, 0.65f, 0.30f)
            : s.number == 7 ? new Color(0.95f, 0.35f, 0.65f) : new Color(0.85f, 0.2f, 0.15f);

        void Tint(Transform t, Color c)
        {
            var r = t.GetComponent<Renderer>();
            if (r == null) return;
            r.GetPropertyBlock(block);
            block.SetColor("_BaseColor", c);
            block.SetColor("_Color", c);
            r.SetPropertyBlock(block);
        }

        // ---------- 毎フレーム ----------

        void Update()
        {
            UpdateFade();
            UpdateGlide();
            if (page == Page.Quitting)
            {
                quitTimer += Time.deltaTime;
                if (quitTimer >= quitFadeTime + 0.3f) GameFlow.Quit();
                return;
            }

            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && page != Page.Main) Back();

            if (page == Page.Operation) UpdateMapPicking();
            else if (page == Page.Equipment || page == Page.Research) UpdateObjectPicking();

            // 地図の印：カーソルが乗っている・選んでいるものを大きく脈打たせる
            foreach (var kv in markerStage)
            {
                bool on = kv.Value == hovered || kv.Value == selected;
                float pulse = on ? 1.25f + 0.08f * Mathf.Sin(Time.time * 6f) : 1f;
                kv.Key.localScale = markerScale[kv.Key] * pulse;
            }
        }

        Transform RaycastMouse()
        {
            var mouse = Mouse.current;
            if (mouse == null || mainCamera == null || fade > 0.01f) return null;
            Vector2 mp = mouse.position.ReadValue();
            if (IsOverPanel(mp)) return null;
            var ray = mainCamera.ScreenPointToRay(mp);
            return Physics.Raycast(ray, out var hit, 100f, ~0, QueryTriggerInteraction.Ignore) ? hit.transform : null;
        }

        void UpdateMapPicking()
        {
            var t = RaycastMouse();
            hovered = t != null && markerStage.TryGetValue(t, out var s) ? s : null;
            var mouse = Mouse.current;
            if (hovered != null && mouse != null && mouse.leftButton.wasPressedThisFrame) selected = hovered;
        }

        void UpdateObjectPicking()
        {
            var mouse = Mouse.current;
            var t = RaycastMouse();
            if (t == null || mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            // 床や壁（Plane）は選べない
            if (t.name.StartsWith("Plane")) return;
            objectMessage = page == Page.Equipment
                ? $"「{t.name}」を選択\nラックの武器を選ぶと作業台に置かれ、強化・装備を選べるようになる予定です（準備中）。"
                : $"「{t.name}」を選択\nガジェット・武器・ウルトを選ぶと、新しい装備の作成・研究ができる予定です（準備中）。";
        }

        void Enter(Page p)
        {
            page = p;
            hovered = null;
            objectMessage = null;
            MoveTo(EnterPoint(p));
            if (p == Page.Quitting)
            {
                quitTimer = 0f;
                fadingOut = true;
            }
        }

        void Back()
        {
            page = Page.Main;
            hovered = null;
            objectMessage = null;
        }

        /// ボタンにカーソルを合わせたときの位置（用意していなければ、クリックしたときと同じ）
        Transform HoverPoint(Page p)
        {
            Transform hover = p switch
            {
                Page.Operation => operationHover,
                Page.Equipment => equipmentHover,
                Page.Research => researchHover,
                _ => overviewPoint,
            };
            return hover != null ? hover : RawEnter(p);
        }

        /// ボタンをクリックしたときの位置（用意していなければ、合わせたときと同じ）
        Transform EnterPoint(Page p)
        {
            var enter = RawEnter(p);
            return enter != null ? enter : HoverPoint(p);
        }

        Transform RawEnter(Page p) => p switch
        {
            Page.Operation => operationEnter,
            Page.Equipment => equipmentEnter,
            Page.Research => researchEnter,
            _ => overviewPoint,
        };

        // ---------- カメラ ----------

        /// 近い位置（同じ部屋の中）へはなめらかに動き、遠い位置（別の部屋）へは一瞬暗転して切り替える
        void MoveTo(Transform point)
        {
            if (point == null || point == currentPoint || point == pendingPoint) return;
            if (currentPoint == null)
            {
                Place(point);
                return;
            }
            if (mainCamera != null && Vector3.Distance(mainCamera.transform.position, point.position) <= glideDistance)
            {
                pendingPoint = null;
                fadingOut = false;
                glideFromPos = mainCamera.transform.position;
                glideFromRot = mainCamera.transform.rotation;
                currentPoint = point;
                glideT = 0f;
                return;
            }
            pendingPoint = point;
            fadingOut = true;
        }

        void Place(Transform point)
        {
            currentPoint = point;
            glideT = 1f;
            if (mainCamera != null && point != null) mainCamera.transform.SetPositionAndRotation(point.position, point.rotation);
        }

        void UpdateGlide()
        {
            if (glideT >= 1f || mainCamera == null || currentPoint == null) return;
            glideT = Mathf.Min(1f, glideT + Time.deltaTime / Mathf.Max(0.01f, glideTime));
            float k = Mathf.SmoothStep(0f, 1f, glideT);
            mainCamera.transform.SetPositionAndRotation(
                Vector3.Lerp(glideFromPos, currentPoint.position, k),
                Quaternion.Slerp(glideFromRot, currentPoint.rotation, k));
        }

        void UpdateFade()
        {
            if (page == Page.Quitting)
            {
                fade = Mathf.MoveTowards(fade, 1f, Time.deltaTime / quitFadeTime);
                return;
            }
            if (fadingOut)
            {
                fade = Mathf.MoveTowards(fade, 1f, Time.deltaTime / fadeTime);
                if (fade >= 1f)
                {
                    Place(pendingPoint);
                    pendingPoint = null;
                    fadingOut = false;
                }
            }
            else fade = Mathf.MoveTowards(fade, 0f, Time.deltaTime / fadeTime);
        }

        // ---------- 画面（IMGUI） ----------

        float S => Screen.height / 1080f;
        Rect menuArea, infoArea;

        bool IsOverPanel(Vector2 screenPos)
        {
            Vector2 gui = new Vector2(screenPos.x, Screen.height - screenPos.y);
            return (page != Page.Main && infoArea.Contains(gui)) || menuArea.Contains(gui);
        }

        void BuildStyles()
        {
            float s = S;
            label = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(24 * s), wordWrap = true };
            label.normal.textColor = Color.white;
            labelSmall = new GUIStyle(label) { fontSize = Mathf.RoundToInt(19 * s) };
            title = new GUIStyle(label) { fontSize = Mathf.RoundToInt(44 * s), fontStyle = FontStyle.Bold, wordWrap = false };
            subtitle = new GUIStyle(label) { fontSize = Mathf.RoundToInt(30 * s) };
            button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(24 * s), alignment = TextAnchor.MiddleCenter };
            menuButton = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(30 * s), alignment = TextAnchor.MiddleLeft, padding = new RectOffset(Mathf.RoundToInt(24 * s), 10, 0, 0) };
        }

        void OnGUI()
        {
            BuildStyles();
            float w = Screen.width, h = Screen.height, s = S;
            menuArea = Rect.zero;
            infoArea = Rect.zero;

            if (page == Page.Main) DrawMenu(w, h, s);
            else if (page == Page.Operation) DrawOperation(w, h, s);
            else if (page == Page.Equipment) DrawRoom(w, h, s, "装備・強化・変更", "ウェポンラックの武器をクリック");
            else if (page == Page.Research) DrawRoom(w, h, s, "武器・作成・研究", "研究室の装備をクリック");

            if (fade > 0.001f) Fill(new Rect(0, 0, w, h), new Color(0f, 0f, 0f, fade));
        }

        void DrawMenu(float w, float h, float s)
        {
            GUI.Label(new Rect(70 * s, 40 * s, w, 60 * s), "対魔法少女殲滅部隊 A・M・G", title);

            float x = 70 * s, y = 150 * s, bw = 480 * s, bh = 80 * s, gap = 18 * s;
            var buttons = new (string text, Page page, float width)[]
            {
                ("作戦開始", Page.Operation, 1f),
                ("装備・強化・変更", Page.Equipment, 0.92f),
                ("武器・作成・研究", Page.Research, 0.88f),
            };
            Vector2 mouse = Event.current.mousePosition;
            Page? hover = null;
            for (int i = 0; i < buttons.Length; i++)
            {
                var r = new Rect(x, y + i * (bh + gap), bw * buttons[i].width, bh);
                if (r.Contains(mouse)) hover = buttons[i].page;
                if (GUI.Button(r, buttons[i].text, menuButton)) Enter(buttons[i].page);
            }
            var quit = new Rect(x, y + 3 * (bh + gap) + 40 * s, bw * 0.45f, bh * 0.75f);
            if (quit.Contains(mouse)) hover = Page.Main;
            if (GUI.Button(quit, "やめる", menuButton)) Enter(Page.Quitting);
            menuArea = new Rect(x, y, bw, quit.yMax - y);

            // カーソルを合わせたボタンの部屋を映す（どれにも合っていないとき・やめるは、拠点を見下ろす位置）
            if (Event.current.type == EventType.Repaint) MoveTo(HoverPoint(hover ?? Page.Main));
        }

        void DrawOperation(float w, float h, float s)
        {
            GUI.Label(new Rect(60 * s, 36 * s, w, 60 * s), "作戦開始", title);
            GUI.Label(new Rect(64 * s, 96 * s, w * 0.6f, 36 * s), "模型の地図の印をクリックしてエリアを選ぶ（外側から中心の星の落下地点へ）", labelSmall);

            infoArea = new Rect(w - 520 * s, 140 * s, 460 * s, h - 260 * s);
            Fill(infoArea, new Color(0f, 0f, 0f, 0.55f));
            float x = infoArea.x + 26 * s, y = infoArea.y + 24 * s, iw = infoArea.width - 52 * s;
            var show = hovered ?? selected;
            if (show != null)
            {
                GUI.Label(new Rect(x, y, iw, 46 * s), show.title, subtitle);
                GUI.Label(new Rect(x, y + 52 * s, iw, 70 * s), $"エリア：{show.area}", label);
                if (show.IsAvailable)
                {
                    GUI.Label(new Rect(x, y + 126 * s, iw, 70 * s), $"目標：{show.bossName}の殲滅", label);
                    GUI.Label(new Rect(x, y + 200 * s, iw, 40 * s), show.IsCleared ? "状況：攻略済み" : "状況：未攻略", label);
                }
                else GUI.Label(new Rect(x, y + 126 * s, iw, 80 * s), "このエリアはまだ調査されていない。", label);
            }
            else GUI.Label(new Rect(x, y, iw, 80 * s), "地図の印にカーソルを合わせる", label);

            if (selected != null && selected.IsAvailable)
            {
                if (GUI.Button(new Rect(x, infoArea.yMax - 120 * s, iw, 90 * s), $"{selected.title} へ出撃", menuButton)) GameFlow.StartStage(selected);
            }
            if (GUI.Button(new Rect(60 * s, h - 84 * s, 220 * s, 56 * s), "戻る（Esc）", button)) Back();
            if (GUI.Button(new Rect(w - 360 * s, h - 84 * s, 300 * s, 56 * s), "進行状況をリセット", button))
            {
                GameProgress.ResetAll();
                foreach (var kv in markerStage) Tint(kv.Key, MarkerColor(kv.Value));
            }
        }

        void DrawRoom(float w, float h, float s, string head, string hint)
        {
            GUI.Label(new Rect(60 * s, 36 * s, w, 60 * s), head, title);
            GUI.Label(new Rect(64 * s, 96 * s, w * 0.6f, 36 * s), hint + "（準備中）", labelSmall);
            if (!string.IsNullOrEmpty(objectMessage))
            {
                infoArea = new Rect(w - 520 * s, 140 * s, 460 * s, 260 * s);
                Fill(infoArea, new Color(0f, 0f, 0f, 0.55f));
                GUI.Label(new Rect(infoArea.x + 24 * s, infoArea.y + 20 * s, infoArea.width - 48 * s, infoArea.height - 40 * s), objectMessage, label);
            }
            if (GUI.Button(new Rect(60 * s, h - 84 * s, 220 * s, 56 * s), "戻る（Esc）", button)) Back();
        }

        static void Fill(Rect r, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }
}
