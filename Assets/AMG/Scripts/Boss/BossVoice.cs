using UnityEngine;

namespace AMG
{
    /// ボスの台詞（字幕）。HUDが表示する。
    /// ステージ1の少女は、村の住人がいたころの言葉を意味もわからず繰り返す（自動）
    public class BossVoice : MonoBehaviour
    {
        public static BossVoice I { get; private set; }

        public string[] calmLines = { "オカエリ", "ゴハン…", "オカエリ…オカエリ…", "ダレ…？" };
        public string[] brokenLines = { "タスケテ", "イタイ…", "オカーサーン", "ドコ…？", "タスケテ…タスケテ…" };

        /// 表示中の台詞（なければnull）
        public string Visible => Time.time < until ? line : null;

        BossController boss;
        string line;
        float until;
        float nextIdle;
        bool started;

        void Awake()
        {
            I = this;
            boss = GetComponent<BossController>();
        }

        public void Say(string text, float duration = 2.5f)
        {
            line = text;
            until = Time.time + duration;
            nextIdle = Mathf.Max(nextIdle, until + 4f);
        }

        void Update()
        {
            // ステージ1の少女だけが自分で片言を繰り返す（ほかのボスは本体が Say を呼ぶ）
            if (boss == null || !GameManager.IsPlaying || boss.IsDead) return;
            if (!started)
            {
                started = true;
                Say("オカエリ…", 3f);
                nextIdle = Time.time + 14f;
            }
            if (Time.time < nextIdle) return;

            // 壊れていくほど、助けを求める言葉が増える
            bool broken = boss.Phase == 2 || boss.KnotsRemaining * 2 <= boss.KnotsTotal;
            var pool = broken ? brokenLines : calmLines;
            Say(pool[Random.Range(0, pool.Length)]);
            nextIdle = Time.time + Random.Range(12f, 20f);
        }
    }
}
