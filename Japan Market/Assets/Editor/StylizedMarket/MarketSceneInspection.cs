using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace JapanMarket.Art.Editor
{
    public static class MarketSceneInspection
    {
        public const string Output = "Temp/MarketArtReview";

        [InitializeOnLoadMethod]
        static void RegisterReviewRequest()
        {
            EditorApplication.update -= ProcessReviewRequest;
            EditorApplication.update += ProcessReviewRequest;
        }

        static void ProcessReviewRequest()
        {
            var request = Output + "/inspect.request";
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(request)) return;
            File.Delete(request);
            try { Inspect(); }
            catch (Exception error) { Debug.LogException(error); File.WriteAllText(Output + "/inspection-error.txt", error.ToString()); }
        }

        [MenuItem("Japan Market/Art/Inspect current market")]
        public static void Inspect()
        {
            Directory.CreateDirectory(Output);
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/Main.unity") throw new InvalidOperationException("Open Main.unity for this inspection.");
            var report = new StringBuilder();
            foreach (var root in scene.GetRootGameObjects())
                Describe(root.transform, report, 0);
            File.WriteAllText(Output + "/scene-hierarchy.txt", report.ToString());
            foreach (var camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                Debug.Log("ART CAMERA " + camera.name + " " + camera.transform.position + " " + camera.transform.eulerAngles);
                if (camera.CompareTag("MainCamera")) Capture(camera, "before-player");
            }
            var review = new GameObject("Art review camera").AddComponent<Camera>();
            review.nearClipPlane = .1f;
            review.farClipPlane = 500;
            review.fieldOfView = 55;
            review.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            Shot(review, new Vector3(0, 23, -33), new Vector3(22, 0, -5), "before-overview");
            Shot(review, new Vector3(13, 4, -22), new Vector3(22, 2, -4), "before-street");
            UnityEngine.Object.DestroyImmediate(review.gameObject);
            Debug.Log("ART INSPECTION COMPLETE");
        }

        static void Describe(Transform t, StringBuilder text, int depth)
        {
            var renderer = t.GetComponent<Renderer>();
            text.Append(new string(' ', depth * 2)).Append(t.name).Append(" | active=").Append(t.gameObject.activeSelf)
                .Append(" | world=").Append(t.position.ToString("F2")).Append(" | rotation=").Append(t.eulerAngles.ToString("F1"));
            if (renderer != null)
                text.Append(" | bounds=").Append(renderer.bounds.center.ToString("F2")).Append(" / ").Append(renderer.bounds.size.ToString("F2"))
                    .Append(" | mats=").Append(string.Join(",", renderer.sharedMaterials.Select(m => m == null ? "MISSING" : AssetDatabase.GetAssetPath(m))));
            var light = t.GetComponent<Light>();
            if (light != null) text.Append(" | light=").Append(light.type).Append(" intensity=").Append(light.intensity);
            text.AppendLine();
            foreach (Transform child in t) Describe(child, text, depth + 1);
        }

        public static void Shot(Camera camera, Vector3 position, Vector3 target, string name)
        {
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position));
            Capture(camera, name);
        }

        public static void Capture(Camera camera, string name)
        {
            Directory.CreateDirectory(Output);
            var previous = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var target = new RenderTexture(1600, 1000, 24, RenderTextureFormat.ARGBHalf);
            target.Create();
            camera.targetTexture = target;
            camera.allowHDR = true;
            camera.Render();
            RenderTexture.active = target;
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            texture.Apply();
            File.WriteAllBytes(Output + "/" + name + ".png", texture.EncodeToPNG());
            camera.targetTexture = previous;
            RenderTexture.active = previousActive;
            UnityEngine.Object.DestroyImmediate(texture);
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
