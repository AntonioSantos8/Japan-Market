using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace JapanMarket.Art.Editor
{
    /// <summary>Authors the market's lighting without changing the default renderer or shared look.</summary>
    public static class MarketArtLighting
    {
        private const string LightingFolder = "Assets/Art/StylizedMarket/Lighting";
        private const string RendererPath = LightingFolder + "/MarketCleanRenderer.asset";
        private const string ProfilePath = LightingFolder + "/MarketDaylightProfile.asset";
        private const string SourceRendererPath = "Assets/Settings/PC_Renderer.asset";
        private const string PipelinePath = "Assets/Settings/PC_RPAsset.asset";
        private const string LegacyProfilePath = "Assets/Settings/GustavoPP.asset";

        public static void Configure(Transform artRoot)
        {
            if (artRoot == null || !artRoot.gameObject.scene.IsValid())
                throw new ArgumentException("A market scene art root is required.", nameof(artRoot));

            EnsureFolder(LightingFolder);
            Scene scene = artRoot.gameObject.scene;
            int rendererIndex = ConfigureRenderer();
            ConfigureCameras(scene, rendererIndex);
            ConfigureVolume(scene, artRoot, CreateProfile());
            ConfigureEnvironment();
            ConfigureLights(scene, artRoot);
        }

        private static int ConfigureRenderer()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
                throw new InvalidOperationException("Market PC render pipeline was not found: " + PipelinePath);

            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null)
            {
                if (!AssetDatabase.CopyAsset(SourceRendererPath, RendererPath))
                    throw new InvalidOperationException("Could not create the market renderer from " + SourceRendererPath);
                renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            }
            if (renderer == null)
                throw new InvalidOperationException("The scoped market renderer could not be loaded.");

            foreach (var feature in renderer.rendererFeatures)
            {
                if (feature == null) continue;
                var fullscreen = feature as FullScreenPassRendererFeature;
                bool knownDither = ContainsDither(feature.name) ||
                    (fullscreen != null && fullscreen.passMaterial != null &&
                     (ContainsDither(fullscreen.passMaterial.name) ||
                      ContainsDither(AssetDatabase.GetAssetPath(fullscreen.passMaterial))));
                if (!knownDither) continue;
                feature.SetActive(false);
                EditorUtility.SetDirty(feature);
            }
            EditorUtility.SetDirty(renderer);

            // Append only: existing renderer indices and the pipeline default remain stable.
            var serialized = new SerializedObject(pipeline);
            var renderers = serialized.FindProperty("m_RendererDataList");
            if (renderers == null || !renderers.isArray)
                throw new InvalidOperationException("The PC pipeline does not expose its renderer list.");
            for (int index = 0; index < renderers.arraySize; index++)
                if (renderers.GetArrayElementAtIndex(index).objectReferenceValue == renderer)
                    return index;

            int rendererIndex = renderers.arraySize;
            renderers.InsertArrayElementAtIndex(rendererIndex);
            renderers.GetArrayElementAtIndex(rendererIndex).objectReferenceValue = renderer;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
            return rendererIndex;
        }

        private static void ConfigureCameras(Scene scene, int rendererIndex)
        {
            foreach (var camera in SceneComponents<Camera>(scene))
            {
                var data = camera.GetUniversalAdditionalCameraData();
                data.SetRenderer(rendererIndex);
                data.dithering = false;
                data.stopNaN = true;
                // Monitor/render-texture cameras retain their intentionally unprocessed image.
                if (camera.CompareTag("MainCamera") || data.renderPostProcessing)
                {
                    camera.allowHDR = true;
                    data.renderPostProcessing = true;
                    data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                    data.antialiasingQuality = AntialiasingQuality.High;
                    data.volumeLayerMask |= 1;
                }
                EditorUtility.SetDirty(camera);
                EditorUtility.SetDirty(data);
                PrefabUtility.RecordPrefabInstancePropertyModifications(camera);
                PrefabUtility.RecordPrefabInstancePropertyModifications(data);
            }
        }

        private static VolumeProfile CreateProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "Market Daylight - Clean Warm and Cool";
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            var color = Component<ColorAdjustments>(profile);
            color.postExposure.Override(0f);
            color.contrast.Override(8f);
            color.colorFilter.Override(Color.white);
            color.hueShift.Override(0f);
            color.saturation.Override(5f);
            var balance = Component<WhiteBalance>(profile);
            balance.temperature.Override(0f);
            balance.tint.Override(0f);
            Component<Tonemapping>(profile).mode.Override(TonemappingMode.Neutral);

            var bloom = Component<Bloom>(profile);
            bloom.intensity.Override(0.18f);
            bloom.threshold.Override(1.3f);
            bloom.clamp.Override(4f);
            bloom.scatter.Override(0.55f);
            bloom.tint.Override(Color.white);
            bloom.highQualityFiltering.Override(true);
            bloom.dirtIntensity.Override(0f);
            Component<FilmGrain>(profile).intensity.Override(0f);
            Component<MotionBlur>(profile).intensity.Override(0f);
            Component<ChromaticAberration>(profile).intensity.Override(0f);

            var dof = Component<DepthOfField>(profile);
            dof.mode.Override(DepthOfFieldMode.Gaussian);
            dof.gaussianStart.Override(35f);
            dof.gaussianEnd.Override(80f);
            dof.gaussianMaxRadius.Override(0.5f);
            dof.highQualitySampling.Override(true);

            var vignette = Component<Vignette>(profile);
            vignette.intensity.Override(0.12f);
            vignette.smoothness.Override(0.55f);
            vignette.color.Override(new Color(0.08f, 0.10f, 0.13f));
            vignette.center.Override(new Vector2(0.5f, 0.5f));
            foreach (var component in profile.components) EditorUtility.SetDirty(component);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static T Component<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (!profile.TryGet<T>(out var component))
            {
                component = profile.Add<T>(true);
                component.name = typeof(T).Name;
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            component.active = true;
            return component;
        }

        private static void ConfigureVolume(Scene scene, Transform artRoot, VolumeProfile profile)
        {
            Volume target = null;
            foreach (var volume in SceneComponents<Volume>(scene))
            {
                if (!volume.isGlobal) continue;
                string path = AssetDatabase.GetAssetPath(volume.sharedProfile);
                if (path != LegacyProfilePath && path != ProfilePath) continue;
                volume.sharedProfile = profile;
                volume.weight = 1f;
                volume.priority = 1f;
                volume.enabled = true;
                EditorUtility.SetDirty(volume);
                PrefabUtility.RecordPrefabInstancePropertyModifications(volume);
                target = volume;
            }
            if (target != null) return;
            var transform = Child(artRoot, "Market Daylight Volume");
            transform.gameObject.layer = 0;
            target = transform.GetComponent<Volume>();
            if (target == null) target = transform.gameObject.AddComponent<Volume>();
            target.isGlobal = true;
            target.priority = 1f;
            target.weight = 1f;
            target.sharedProfile = profile;
            EditorUtility.SetDirty(target);
        }

        private static void ConfigureEnvironment()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.49f, 0.64f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.52f, 0.49f, 0.43f);
            RenderSettings.ambientGroundColor = new Color(0.29f, 0.25f, 0.20f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.reflectionIntensity = 0.45f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 35f;
            RenderSettings.fogEndDistance = 140f;
            RenderSettings.fogColor = new Color(0.68f, 0.76f, 0.78f);
            RenderSettings.haloStrength = 0f;
            RenderSettings.flareStrength = 0f;
        }

        private static void ConfigureLights(Scene scene, Transform artRoot)
        {
            Transform rig = Child(artRoot, "Daylight and Interior Lighting");
            Light key = null;
            Light fallback = null;
            foreach (var light in SceneComponents<Light>(scene))
            {
                if (light.type != LightType.Directional) continue;
                if (light.name == "Directional Light (1)") key = light;
                if (light.name == "Directional Light") fallback = light;
            }
            key = key != null ? key : fallback;
            if (key == null)
            {
                Transform keyTransform = Child(rig, "Market Afternoon Key");
                key = keyTransform.GetComponent<Light>();
                if (key == null) key = keyTransform.gameObject.AddComponent<Light>();
                key.type = LightType.Directional;
            }
            key.enabled = true;
            key.useColorTemperature = false;
            key.color = new Color(1f, 0.89f, 0.73f);
            key.intensity = 1.6f;
            key.transform.rotation = Quaternion.Euler(40f, -35f, 0f);
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 0.82f;
            key.shadowBias = 0.035f;
            key.shadowNormalBias = 0.30f;
            key.bounceIntensity = 1f;
            RenderSettings.sun = key;
            DirtyLight(key);

            // Retain the legacy fills at a controlled level instead of stacking full-strength suns.
            foreach (var light in SceneComponents<Light>(scene))
            {
                if (light == key || light.type != LightType.Directional) continue;
                if (light.name != "Directional Light" && light.name != "Directional Light (2)") continue;
                light.useColorTemperature = false;
                light.color = new Color(0.69f, 0.82f, 1f);
                light.intensity = light.name == "Directional Light" ? 0.12f : 0.06f;
                light.shadows = LightShadows.None;
                DirtyLight(light);
            }

            Point(rig, "Facade Bounce", new Vector3(21f, 3.4f, -7.5f),
                new Color(1f, 0.91f, 0.77f), 1.1f, 12f);
            var rim = Point(rig, "Cool Roof and Silhouette Rim", new Vector3(32f, 7f, 3f),
                new Color(0.66f, 0.83f, 1f), 4f, 19f);
            rim.type = LightType.Spot;
            rim.spotAngle = 78f;
            rim.innerSpotAngle = 48f;
            rim.transform.rotation = Quaternion.LookRotation(new Vector3(21f, 2f, -3f) - rim.transform.position);
            DirtyLight(rim);
            Point(rig, "Interior Softbox West", new Vector3(21f, 3.8f, 0.5f),
                new Color(1f, 0.92f, 0.81f), 1.6f, 8f);
            Point(rig, "Interior Softbox East", new Vector3(28f, 3.8f, 0.5f),
                new Color(1f, 0.92f, 0.81f), 1.6f, 8f);
        }

        private static Light Point(Transform parent, string name, Vector3 position, Color color,
            float intensity, float range)
        {
            Transform transform = Child(parent, name);
            transform.position = position;
            transform.rotation = Quaternion.identity;
            var light = transform.GetComponent<Light>();
            if (light == null) light = transform.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.enabled = true;
            light.lightmapBakeType = LightmapBakeType.Realtime;
            light.useColorTemperature = false;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.Auto;
            DirtyLight(light);
            return light;
        }

        private static void DirtyLight(Light light)
        {
            EditorUtility.SetDirty(light);
            EditorUtility.SetDirty(light.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(light);
            PrefabUtility.RecordPrefabInstancePropertyModifications(light.transform);
        }

        private static IEnumerable<T> SceneComponents<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var component in root.GetComponentsInChildren<T>(true))
                    yield return component;
        }

        private static Transform Child(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null) return child;
            child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static bool ContainsDither(string value)
        {
            return !string.IsNullOrEmpty(value) && value.IndexOf("dither", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
