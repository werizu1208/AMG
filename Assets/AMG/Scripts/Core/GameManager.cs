using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace AMG
{
    public enum GameState { Loadout, Playing, Dead, Victory }

    /// 出撃前のウルト選択 → 戦闘 → 死亡/撃破 → リトライ の流れを管理する
    public class GameManager : MonoBehaviour
    {
        public static GameManager I { get; private set; }

        public GameState State { get; private set; } = GameState.Loadout;
        public float BattleTime { get; private set; }

        public static bool IsPlaying => I != null && I.State == GameState.Playing;
        /// ウルト選択に使ったキー入力が戦闘に流れ込まないよう、開始フレームを記録する
        public static bool AcceptsBattleInput => IsPlaying && Time.frameCount > I.battleStartFrame;

        int battleStartFrame;
        public static bool CursorLocked => Cursor.lockState == CursorLockMode.Locked;

        void Awake()
        {
            I = this;
        }

        void Start()
        {
            SetCursorLocked(false);
        }

        public void BeginBattle(UltType ult)
        {
            if (State != GameState.Loadout) return;
            if (PlayerHealth.I != null) PlayerHealth.I.GetComponent<UltSystem>().SetUlt(ult);
            State = GameState.Playing;
            battleStartFrame = Time.frameCount;
            SetCursorLocked(true);
        }

        public void OnPlayerDied()
        {
            if (State != GameState.Playing) return;
            State = GameState.Dead;
            SetCursorLocked(false);
        }

        public void OnBossDefeated()
        {
            if (State != GameState.Playing) return;
            State = GameState.Victory;
            SetCursorLocked(false);
            GameFlow.OnStageCleared();
        }

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null) return;

            switch (State)
            {
                case GameState.Loadout:
                    // 出撃前なら拠点へ戻れる
                    if (kb.bKey.wasPressedThisFrame) GameFlow.GoToBase();
                    break;
                case GameState.Playing:
                    BattleTime += Time.deltaTime;
                    if (kb.escapeKey.wasPressedThisFrame) SetCursorLocked(false);
                    // カーソルを解放している間（一時停止のつもりのとき）だけ、B で拠点へ撤退できる
                    else if (!CursorLocked && kb.bKey.wasPressedThisFrame) GameFlow.GoToBase();
                    else if (!CursorLocked && mouse != null && mouse.leftButton.wasPressedThisFrame) SetCursorLocked(true);
                    break;
                case GameState.Dead:
                case GameState.Victory:
                    if (kb.rKey.wasPressedThisFrame) GameFlow.Retry();
                    else if (kb.bKey.wasPressedThisFrame) GameFlow.GoToBase();
                    break;
            }
        }

        public static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
