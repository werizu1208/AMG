using UnityEngine;

namespace AMG
{
    /// HUD用の簡易アイコン（ドット絵を実行時にテクスチャ化）。白で作るので GUI.color で色を付けて使う。
    /// 本番のアイコン画像ができたら差し替える
    public static class HudIcons
    {
        static Texture2D rifle, pistol, grenade, medkit;

        public static Texture2D Rifle => rifle != null ? rifle : rifle = Build(
            "..............##..........#...",
            "#######################.......",
            "##############################",
            "#######################.......",
            "###....###############........",
            "##.......##...####............",
            ".........##....####...........",
            "................####..........",
            ".................###..........");

        public static Texture2D Pistol => pistol != null ? pistol : pistol = Build(
            "............#.",
            "##############",
            "##############",
            "##############",
            "..########....",
            "..####..#.....",
            "..####........",
            "..####........",
            "..####........");

        public static Texture2D Grenade => grenade != null ? grenade : grenade = Build(
            "...###...",
            "....#.##.",
            "..#####..",
            ".#######.",
            "#########",
            "#########",
            "#########",
            "#########",
            ".#######.",
            "..#####..");

        public static Texture2D Medkit => medkit != null ? medkit : medkit = Build(
            "...###...",
            "...###...",
            "...###...",
            "#########",
            "#########",
            "#########",
            "...###...",
            "...###...",
            "...###...");

        /// '#' が塗り、それ以外が透明。1行目が上
        static Texture2D Build(params string[] rows)
        {
            int h = rows.Length, w = rows[0].Length;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    pixels[(h - 1 - y) * w + x] = x < rows[y].Length && rows[y][x] == '#'
                        ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }
    }
}
