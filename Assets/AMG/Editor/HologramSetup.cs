using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AMG.EditorTools
{
    /// メニュー「A・M・G > 研究室にホログラムの設計図を配置」。
    /// 開いているシーン（Basecamp）の研究室のカメラ（カーソルを合わせたときの CameraRoom (2)）の正面に、HologramBlueprint を置く。
    /// すでにあれば、マテリアルと表示するモデルだけ設定し直す
    public static class HologramSetup
    {
        const string MaterialPath = "Assets/AMG/Materials/Common/Hologram.mat";

        // 表示する設計図：モデルの名前（プロジェクト内を名前で探す）と見出し
        static readonly (string asset, string title)[] Blueprints =
        {
            ("p_rifle", "Assault Rifle"),
            ("p_pistol", "Handgun"),
            ("Granage", "Grenade Prototype"),
            ("ULT", "Anti-Magical-Girl Core"),
        };

        [MenuItem("A・M・G/研究室にホログラムの設計図を配置")]
        static void Setup()
        {
            var mat = EnsureMaterial();
            if (mat == null) return;

            var holo = Object.FindFirstObjectByType<HologramBlueprint>();
            if (holo == null)
            {
                var go = new GameObject("HologramBlueprint");
                Undo.RegisterCreatedObjectUndo(go, "ホログラムの設計図を配置");
                holo = go.AddComponent<HologramBlueprint>();
                PlaceInFrontOfCamera(go.transform, holo);
            }
            else Undo.RecordObject(holo, "ホログラムの設計図を設定");

            var models = new List<GameObject>();
            var titles = new List<string>();
            foreach (var (asset, title) in Blueprints)
            {
                var model = FindModel(asset);
                if (model == null) continue;
                EnableReadWrite(model);
                models.Add(model);
                titles.Add(title);
            }
            holo.hologramMaterial = mat;
            holo.models = models.ToArray();
            holo.titles = titles.ToArray();
            EditorUtility.SetDirty(holo);
            EditorSceneManager.MarkSceneDirty(holo.gameObject.scene);
            Selection.activeGameObject = holo.gameObject;
            EditorUtility.DisplayDialog("A・M・G", $"ホログラムの設計図を配置しました（設計図 {models.Count} 種）。\n位置は HologramBlueprint を動かして調整してください（このオブジェクトが投影機の床側）。", "OK");
        }

        static Material EnsureMaterial()
        {
            var shader = Shader.Find("AMG/Hologram");
            if (shader == null)
            {
                EditorUtility.DisplayDialog("A・M・G", "ホログラムのシェーダー（Assets/AMG/Shaders/Hologram.shader）が見つかりません。", "OK");
                return null;
            }
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat == null)
            {
                AlphaSceneBuilder.EnsureFolder("Assets/AMG/Materials/Common");
                mat = new Material(shader) { name = "Hologram" };
                AssetDatabase.CreateAsset(mat, MaterialPath);
            }
            else mat.shader = shader;
            AssetDatabase.SaveAssets();
            return mat;
        }

        /// 研究室のカメラ（合わせたとき → クリックしたときの順に探す）の 2.3m 正面に、設計図が浮かぶようにする
        static void PlaceInFrontOfCamera(Transform t, HologramBlueprint holo)
        {
            Transform cam = null;
            foreach (var tr in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (tr.name == "CameraRoom (2)") cam = tr.childCount > 0 ? tr.GetChild(0) : tr;
            if (cam == null)
            {
                var go = GameObject.Find("CameaPotision (2)");
                if (go != null) cam = go.transform;
            }
            if (cam == null && SceneView.lastActiveSceneView != null) cam = SceneView.lastActiveSceneView.camera.transform;
            if (cam == null) return;

            Vector3 flatForward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            if (flatForward.sqrMagnitude < 0.01f) flatForward = Vector3.forward;
            Vector3 center = cam.position + cam.forward * 2.3f;
            t.position = center - Vector3.up * holo.hoverHeight;
            t.rotation = Quaternion.LookRotation(-flatForward);
        }

        /// ワイヤーフレームを作るために、モデルの形のデータをスクリプトから読めるようにする（Read/Write をオン）
        static void EnableReadWrite(GameObject model)
        {
            string path = AssetDatabase.GetAssetPath(model);
            if (AssetImporter.GetAtPath(path) is ModelImporter importer && !importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }
        }

        static GameObject FindModel(string name)
        {
            foreach (var guid in AssetDatabase.FindAssets(name + " t:GameObject", new[] { "Assets/AMG/Models" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == name) return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            return null;
        }
    }
}
