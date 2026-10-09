using UnityEngine;
using UnityEngine.SceneManagement;

namespace AMG
{
    /// シーンをまたぐゲームの流れ：拠点 → 作戦ブリーフィングでステージを選ぶ → ステージ（ボス戦）→ 結果 → 拠点へ戻る / リトライ
    public static class GameFlow
    {
        /// いま遊んでいるステージ（ステージのシーンを直接再生したときも、シーン名から決まる）
        public static StageCatalog.Stage CurrentStage => StageCatalog.FindByScene(SceneManager.GetActiveScene().name);

        public static bool InBase => SceneManager.GetActiveScene().name == StageCatalog.BaseScene;

        public static void StartStage(StageCatalog.Stage stage)
        {
            if (stage == null || !stage.IsAvailable) return;
            Load(stage.sceneName);
        }

        public static void Retry() => Load(SceneManager.GetActiveScene().name);

        public static void GoToBase() => Load(StageCatalog.BaseScene);

        /// ボスを倒したときに呼ぶ
        public static void OnStageCleared()
        {
            var stage = CurrentStage;
            if (stage != null) GameProgress.MarkCleared(stage.number);
        }

        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        static void Load(string sceneName)
        {
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[A・M・G] シーン「{sceneName}」がビルド設定（File > Build Profiles の Scene List）に入っていません。");
                return;
            }
            Time.timeScale = 1f;
            GameManager.SetCursorLocked(false);
            SceneManager.LoadScene(sceneName);
        }

        /// 拠点のシーンに、メニュー（BasecampMenu）が置かれていなければ自動で用意する
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            SceneManager.sceneLoaded += (scene, mode) => EnsureBaseMenu();
            EnsureBaseMenu();
        }

        static void EnsureBaseMenu()
        {
            if (!InBase || Object.FindFirstObjectByType<BasecampMenu>() != null) return;
            new GameObject("BasecampMenu").AddComponent<BasecampMenu>();
        }
    }
}
