using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AMG.EditorTools
{
    /// 選択中のオブジェクト（子を含む）を、見えている形のままOBJで書き出す。
    /// Blenderでのブロックアウトや、AI生成モデルの大きさ合わせの基準に使う。
    /// 再生中に一時停止して書き出すと、IKで立っている姿勢のまま書き出せる
    public static class ObjExporter
    {
        [MenuItem("A・M・G/選択中のオブジェクトをOBJで書き出し")]
        static void ExportSelection()
        {
            var root = Selection.activeTransform;
            if (root == null)
            {
                EditorUtility.DisplayDialog("A・M・G", "ヒエラルキーで書き出したいオブジェクト（例：Girl、TreeForm）を選んでください。", "OK");
                return;
            }
            string path = EditorUtility.SaveFilePanel("OBJで書き出し", "", root.name + ".obj", "obj");
            if (string.IsNullOrEmpty(path)) return;

            int count = Export(root, path);
            EditorUtility.DisplayDialog("A・M・G", $"{count} 個のメッシュを書き出しました。\n{path}", "OK");
        }

        /// root の位置・向きを原点とした座標で書き出す（大きさはそのまま、単位はメートル）
        public static int Export(Transform root, string path)
        {
            var ci = CultureInfo.InvariantCulture;
            var obj = new StringBuilder();
            var mtl = new StringBuilder();
            string mtlName = Path.GetFileNameWithoutExtension(path) + ".mtl";
            obj.AppendLine("# A・M・G export").AppendLine("mtllib " + mtlName);

            Matrix4x4 toRoot = Matrix4x4.TRS(root.position, root.rotation, Vector3.one).inverse;
            int offset = 1, count = 0;
            var writtenMaterials = new System.Collections.Generic.HashSet<string>();

            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            {
                var mr = mf.GetComponent<MeshRenderer>();
                var mesh = mf.sharedMesh;
                if (mr == null || !mr.enabled || mesh == null) continue;

                Matrix4x4 m = toRoot * mf.transform.localToWorldMatrix;
                Matrix4x4 n = m.inverse.transpose;
                obj.AppendLine("o " + Sanitize(mf.name) + "_" + count);

                // Unity（左手系）→ OBJ（右手系）：Xを反転し、面の向きを逆にする
                foreach (var v in mesh.vertices)
                {
                    Vector3 p = m.MultiplyPoint3x4(v);
                    obj.AppendLine(string.Format(ci, "v {0} {1} {2}", -p.x, p.y, p.z));
                }
                foreach (var v in mesh.normals)
                {
                    Vector3 d = n.MultiplyVector(v).normalized;
                    obj.AppendLine(string.Format(ci, "vn {0} {1} {2}", -d.x, d.y, d.z));
                }
                var uv = mesh.uv;
                foreach (var t in uv) obj.AppendLine(string.Format(ci, "vt {0} {1}", t.x, t.y));
                bool hasUv = uv.Length == mesh.vertexCount;
                bool hasNormals = mesh.normals.Length == mesh.vertexCount;

                var mat = mr.sharedMaterial;
                string matName = mat != null ? Sanitize(mat.name) : "Default";
                obj.AppendLine("usemtl " + matName);
                if (writtenMaterials.Add(matName))
                {
                    Color c = mat != null && mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.gray;
                    mtl.AppendLine("newmtl " + matName).AppendLine(string.Format(ci, "Kd {0} {1} {2}", c.r, c.g, c.b)).AppendLine();
                }

                var tris = mesh.triangles;
                for (int i = 0; i < tris.Length; i += 3)
                {
                    obj.Append("f");
                    foreach (int idx in new[] { tris[i], tris[i + 2], tris[i + 1] })
                    {
                        int k = idx + offset;
                        obj.Append(' ').Append(k);
                        if (hasUv || hasNormals) obj.Append('/').Append(hasUv ? k.ToString() : "");
                        if (hasNormals) obj.Append('/').Append(k);
                    }
                    obj.AppendLine();
                }
                offset += mesh.vertexCount;
                count++;
            }

            File.WriteAllText(path, obj.ToString());
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(path), mtlName), mtl.ToString());
            return count;
        }

        static string Sanitize(string s) => s.Replace(' ', '_');
    }
}
