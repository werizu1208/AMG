using UnityEngine;

namespace AMG
{
    /// ステージの一覧。星の落下地点を中心に広がるエリアを、外側（ステージ1）から中心（ステージ7）へ攻略していく。
    /// sceneName が空のステージはまだ作っていない（地図には「未調査」として出す）
    public static class StageCatalog
    {
        public const string BaseScene = "Basecamp";

        public class Stage
        {
            public int number;
            public string title;
            public string area;
            public string bossName;
            public string sceneName;
            /// 地図（ブリーフィングの机）の上の位置。0〜1、左上が原点
            public Vector2 mapPosition;

            public bool IsAvailable => !string.IsNullOrEmpty(sceneName);
            public bool IsCleared => GameProgress.IsCleared(number);
        }

        public static readonly Stage[] Stages =
        {
            new Stage { number = 1, title = "ステージ1", area = "植物に埋もれた村", bossName = "植物に埋もれた村の魔法少女", sceneName = "Alpha_Boss1", mapPosition = new Vector2(0.18f, 0.78f) },
            new Stage { number = 2, title = "ステージ2", area = "未調査", mapPosition = new Vector2(0.10f, 0.40f) },
            new Stage { number = 3, title = "ステージ3", area = "未調査", mapPosition = new Vector2(0.34f, 0.16f) },
            new Stage { number = 4, title = "ステージ4", area = "未調査", mapPosition = new Vector2(0.70f, 0.14f) },
            new Stage { number = 5, title = "ステージ5", area = "未調査", mapPosition = new Vector2(0.90f, 0.45f) },
            new Stage { number = 6, title = "ステージ6", area = "未調査", mapPosition = new Vector2(0.74f, 0.82f) },
            new Stage { number = 7, title = "ステージ7", area = "隕石落下後のクレーター（星の落下地点）", bossName = "原初の魔法少女", sceneName = "Stage7_FinalBoss", mapPosition = new Vector2(0.52f, 0.50f) },
        };

        public static Stage FindByScene(string sceneName)
        {
            foreach (var s in Stages)
                if (s.sceneName == sceneName) return s;
            return null;
        }
    }

    /// 進行状況（どのステージをクリアしたか）。PlayerPrefs に保存するので、ゲームを閉じても残る
    public static class GameProgress
    {
        static string Key(int stage) => $"AMG.Cleared.Stage{stage}";

        public static bool IsCleared(int stage) => PlayerPrefs.GetInt(Key(stage), 0) == 1;

        public static void MarkCleared(int stage)
        {
            PlayerPrefs.SetInt(Key(stage), 1);
            PlayerPrefs.Save();
        }

        public static void ResetAll()
        {
            foreach (var s in StageCatalog.Stages) PlayerPrefs.DeleteKey(Key(s.number));
            PlayerPrefs.Save();
        }
    }
}
