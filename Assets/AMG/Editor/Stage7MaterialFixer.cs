using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AMG.EditorTools
{
    /// ステージ7のモデルに付いてきたマテリアルは、別のプロジェクトのシェーダーを参照していて、
    /// このプロジェクトではエラー表示（ピンク）になる。テクスチャ・色・つや・発光を引き継いで URP の Lit に変換する。
    /// ・.mat ファイルのマテリアルはその場で変換する
    /// ・FBX に埋め込まれたマテリアルは編集できないので、変換したコピーを Materials/Converted に作って差し替える
    public static class Stage7MaterialFixer
    {
        const string ModelDir = "Assets/AMG/Models/Stage7Boss";
        const string ConvertedDir = ModelDir + "/Materials/Converted";

        [MenuItem("A・M・G/ステージ7のマテリアルをURPに変換")]
        static void FixFromMenu()
        {
            int n = FixAssets();
            // 開いているシーン（生成済みのステージ7）に置いたモデルの、埋め込みマテリアルも差し替える
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
                if (root.GetComponent<PrimordialBoss>() != null || root.name.StartsWith("Backdrop"))
                {
                    Undo.RegisterFullObjectHierarchyUndo(root, "ステージ7のマテリアルをURPに変換");
                    FixRenderers(root);
                }
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("A・M・G", n > 0 ? $"マテリアルを {n} 個、URP に変換しました。" : "変換が必要なマテリアルはありませんでした。", "OK");
        }

        /// フォルダ内の .mat を変換する。変換した数を返す
        public static int FixAssets()
        {
            int count = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { ModelDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".mat")) continue;
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null || !IsBroken(mat)) continue;
                Convert(mat, mat);
                count++;
            }
            return count;
        }

        /// シーンに置いたモデルのうち、まだエラーのマテリアル（FBX に埋め込まれたもの）を、変換したコピーに差し替える
        public static void FixRenderers(GameObject root)
        {
            var cache = new Dictionary<Material, Material>();
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null || !IsBroken(m)) continue;
                    if (!cache.TryGetValue(m, out var fixedMat))
                    {
                        fixedMat = ConvertedCopy(m);
                        cache[m] = fixedMat;
                    }
                    mats[i] = fixedMat;
                    changed = true;
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        static bool IsBroken(Material m) =>
            m.shader == null || m.shader.name == "Hidden/InternalErrorShader" || !m.shader.isSupported;

        static Material ConvertedCopy(Material src)
        {
            AlphaSceneBuilder.EnsureFolder(ConvertedDir);
            string path = $"{ConvertedDir}/{src.name}.mat";
            var dst = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (dst == null)
            {
                dst = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(dst, path);
            }
            Convert(src, dst);
            return dst;
        }

        /// src の保存されているプロパティ（シェーダーが無くてもファイルには残っている）を読み、dst を URP で作り直す
        static void Convert(Material src, Material dst)
        {
            var so = new SerializedObject(src);
            var tex = new Dictionary<string, (Texture tex, Vector2 scale, Vector2 offset)>();
            var colors = new Dictionary<string, Color>();
            var floats = new Dictionary<string, float>();

            var texEnvs = so.FindProperty("m_SavedProperties.m_TexEnvs");
            for (int i = 0; texEnvs != null && i < texEnvs.arraySize; i++)
            {
                var e = texEnvs.GetArrayElementAtIndex(i);
                tex[e.FindPropertyRelative("first").stringValue] = (
                    e.FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture,
                    e.FindPropertyRelative("second.m_Scale").vector2Value,
                    e.FindPropertyRelative("second.m_Offset").vector2Value);
            }
            var cols = so.FindProperty("m_SavedProperties.m_Colors");
            for (int i = 0; cols != null && i < cols.arraySize; i++)
            {
                var e = cols.GetArrayElementAtIndex(i);
                colors[e.FindPropertyRelative("first").stringValue] = e.FindPropertyRelative("second").colorValue;
            }
            var fls = so.FindProperty("m_SavedProperties.m_Floats");
            for (int i = 0; fls != null && i < fls.arraySize; i++)
            {
                var e = fls.GetArrayElementAtIndex(i);
                floats[e.FindPropertyRelative("first").stringValue] = e.FindPropertyRelative("second").floatValue;
            }

            // グラデーションの空（上・地平線・下の3色）は、Unity 標準の空に置き換える
            if (colors.ContainsKey("_HorizonColor") || colors.ContainsKey("_ZenithColor"))
            {
                dst.shader = Shader.Find("Skybox/Procedural");
                if (colors.TryGetValue("_HorizonColor", out var horizon)) dst.SetColor("_SkyTint", horizon);
                if (colors.TryGetValue("_BottomColor", out var bottom)) dst.SetColor("_GroundColor", bottom);
                dst.SetFloat("_AtmosphereThickness", 0.6f);
                dst.SetFloat("_Exposure", 0.9f);
                EditorUtility.SetDirty(dst);
                return;
            }

            dst.shader = Shader.Find("Universal Render Pipeline/Lit");
            dst.shaderKeywords = new string[0];

            if (tex.TryGetValue("_MainTex", out var main) || tex.TryGetValue("_BaseMap", out main))
            {
                dst.SetTexture("_BaseMap", main.tex);
                dst.SetTextureScale("_BaseMap", main.scale);
                dst.SetTextureOffset("_BaseMap", main.offset);
            }
            Color baseColor = colors.TryGetValue("_Color", out var c) ? c : colors.TryGetValue("_BaseColor", out c) ? c : Color.white;
            dst.SetColor("_BaseColor", baseColor);
            dst.SetFloat("_Smoothness", floats.TryGetValue("_Glossiness", out var g) ? g : floats.TryGetValue("_Smoothness", out g) ? g : 0.3f);
            dst.SetFloat("_Metallic", floats.TryGetValue("_Metallic", out var met) ? met : 0f);

            if (tex.TryGetValue("_BumpMap", out var bump) && bump.tex != null)
            {
                dst.SetTexture("_BumpMap", bump.tex);
                dst.EnableKeyword("_NORMALMAP");
            }

            tex.TryGetValue("_EmissionMap", out var emisTex);
            Color emis = colors.TryGetValue("_EmissionColor", out var ec) ? ec : Color.black;
            // 発光テクスチャがあるのに色が黒のときは、テクスチャの色のまま光らせる
            if (emisTex.tex != null && emis.maxColorComponent <= 0.001f) emis = Color.white;
            if (emisTex.tex != null || emis.maxColorComponent > 0.001f)
            {
                dst.SetTexture("_EmissionMap", emisTex.tex);
                dst.SetColor("_EmissionColor", emis);
                dst.EnableKeyword("_EMISSION");
                dst.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else dst.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;

            EditorUtility.SetDirty(dst);
        }
    }
}
