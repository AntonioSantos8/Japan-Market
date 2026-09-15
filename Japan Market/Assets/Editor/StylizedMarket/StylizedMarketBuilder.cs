using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace JapanMarket.Art.Editor
{
    /// <summary>Authored art pass for Main; no runtime scene generation is required.</summary>
    public static class StylizedMarketBuilder
    {
        const string Art = "Assets/Art/StylizedMarket";
        const string RootName = "ART - Stylized Market";
        static readonly Color Cream = Hex("F1DEB7"), Teal = Hex("235C65"), Coral = Hex("C75D43"), Gold = Hex("DCA657");
        static Material cedar, teal, coral, cream, gold, dark, sage, leaf, stone;
        static TMP_FontAsset font;

        [MenuItem("Japan Market/Art/Apply stylized market art pass")]
        public static void Apply()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/Main.unity" || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Open Main.unity in Edit mode before applying the art pass.");
            Directory.CreateDirectory(MarketSceneInspection.Output);
            if (!File.Exists(MarketSceneInspection.Output + "/Main-before-art.unity"))
                EditorSceneManager.SaveScene(scene, MarketSceneInspection.Output + "/Main-before-art.unity", true);

            var existing = scene.GetRootGameObjects().FirstOrDefault(g => g.name == RootName);
            if (existing != null) Object.DestroyImmediate(existing);
            EnsureFolder(Art + "/Materials");
            EnsureFolder(Art + "/Typography");
            SetupMaterials();
            var root = new GameObject(RootName).transform;
            PaletteExistingScene(scene);
            BuildFacade(Group(root, "01 - Facade and entrance"));
            BuildStreetDetails(Group(root, "02 - Neighborhood details"));
            BuildInterior(Group(root, "03 - Interior accents"));
            MarketArtLighting.Configure(root);
            MakeReviewViews(root);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            Validate(scene, root);
            EditorSceneManager.SaveScene(scene);
            CaptureAfter();
            var view = SceneView.lastActiveSceneView;
            if (view != null)
            {
                view.sceneLighting = true;
                view.sceneViewState.showFog = true;
                view.LookAt(new Vector3(22, 2.5f, -3), Quaternion.Euler(18, -25, 0), 24);
            }
            Selection.activeGameObject = root.gameObject;
            Debug.Log("STYLIZED MARKET COMPLETE: Main scene saved; renders and validation in " + MarketSceneInspection.Output);
        }

        static void SetupMaterials()
        {
            cedar = Material("Painted Cedar", Color.white, .24f);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "/Textures/PaintedCedar.png");
            if (texture == null) throw new InvalidOperationException("PaintedCedar.png must be imported first.");
            var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture));
            if (!importer.mipmapEnabled || importer.wrapMode != TextureWrapMode.Repeat || importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.sRGBTexture = true;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 8;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            cedar.SetTexture("_BaseMap", texture);
            teal = Material("Petrol Enamel", Teal, .3f);
            coral = Material("Vermilion Enamel", Coral, .22f);
            cream = Material("Warm Porcelain", Cream, .22f);
            gold = Material("Soft Brass", Gold, .32f);
            gold.SetFloat("_Metallic", .35f);
            dark = Material("Ink Blue", Hex("253A46"), .25f);
            sage = Material("Sage Ceramic", Hex("779677"), .25f);
            leaf = Material("Jade Leaves", Hex("4D814E"), .1f);
            stone = Material("Warm Sandstone", Hex("BEAD92"), .12f);
        }

        static void PaletteExistingScene(Scene scene)
        {
            var market = scene.GetRootGameObjects().First(g => g.name == "Market");
            var shell = scene.GetRootGameObjects().First(g => g.name == "LojaSUpercartoon");
            var roof = Material("Roof Slate", Hex("345764"), .18f);
            var brick = Material("Terracotta Brick", Hex("B97458"), .13f);
            var plaster = Material("Ivory Plaster", Hex("DBC8AC"), .08f);
            var paper = Material("Lantern Paper", Hex("E97751"), .1f, .12f);
            var warmStrip = Material("Warm Cove Light", Hex("FFD294"), .25f, .75f);
            var fasciaMap = new Dictionary<string, Material>
            {
                ["Assets/Materials/MAt.mat"] = coral,
                ["Assets/Materials/MAt 1.mat"] = teal,
                ["Assets/Materials/MAt 2.mat"] = cream,
                ["Assets/Materials/Orange.mat"] = gold,
                ["Assets/Materials/Floor/black.mat"] = dark
            };
            foreach (var renderer in market.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null) continue;
                    string path = AssetDatabase.GetAssetPath(materials[i]);
                    if (fasciaMap.TryGetValue(path, out var replacement)) { materials[i] = replacement; changed = true; }
                    // Keep the original painted lantern characters, adjusting only the material response.
                    else if (path.Contains("PAperBloon") || path.Contains("PaperBaloon_Default"))
                    {
                        var clean = CloneMaterial(materials[i], "Painted Lantern Original");
                        if (clean.HasProperty("_Smoothness")) clean.SetFloat("_Smoothness", .12f);
                        if (clean.HasProperty("_EmissionColor")) clean.SetColor("_EmissionColor", new Color(.16f, .06f, .025f));
                        materials[i] = clean; changed = true;
                    }
                    else if (path.Contains("Volumetric2")) renderer.enabled = false;
                }
                if (renderer.name == "StoreFloor")
                {
                    var floor = CloneMaterial(materials[0], "Warm Interior Tiles");
                    if (floor.HasProperty("_BaseColor")) floor.SetColor("_BaseColor", Hex("D5D3BC"));
                    if (floor.HasProperty("_Smoothness")) floor.SetFloat("_Smoothness", .2f);
                    materials[0] = floor; changed = true;
                }
                if (renderer.name == "Cube (3)" && renderer.transform.parent != null && renderer.transform.parent.name == "FIllers")
                { materials[0] = plaster; changed = true; }
                if (changed) Assign(renderer, materials);
            }
            foreach (var renderer in shell.GetComponentsInChildren<Renderer>(true))
            {
                string n = renderer.name;
                Material replacement = null;
                if (n == "Teto" || n == "SuperficieTeto") replacement = roof;
                else if (n.StartsWith("Brick") || n == "Cube.016") replacement = brick;
                else if (n.StartsWith("Wall")) replacement = plaster;
                else if (n == "Window" || n == "DoorLeft" || n == "DoorRight") replacement = teal;
                else if (n == "Sombreiro") replacement = coral;
                else if (n == "YellowFIte") replacement = gold;
                if (replacement != null) Assign(renderer, Enumerable.Repeat(replacement, renderer.sharedMaterials.Length).ToArray());
            }
            // Air depth comes from the scene fog; overlapping white particle cards obscure the composition.
            var fog = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Whitish Fog");
            if (fog != null) fog.SetActive(false);
            foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (renderer is not MeshRenderer || renderer.sharedMaterial == null) continue;
                if (AssetDatabase.GetAssetPath(renderer.sharedMaterial).Contains("Volumetric2")) renderer.enabled = false;
            }
            // A narrow warm edge gives the overhang a readable silhouette without broad neon clipping.
            var lightingRoot = GameObject.Find(RootName).transform;
            Box(lightingRoot, "Warm soffit edge", new Vector3(21.4f, 4.56f, -6.94f), new Vector3(22.4f, .045f, .045f), warmStrip, .012f);
        }

        static void BuildFacade(Transform root)
        {
            // The door occupies x26.05..29.72, z-5.0. Posts remain outside its opening.
            foreach (float x in new[] { 25.6f, 30.35f })
            {
                Box(root, "Cedar entrance post", new Vector3(x, 2.17f, -6.22f), new Vector3(.24f, 4.14f, .25f), cedar, .04f, true);
                Box(root, "Brass post foot", new Vector3(x, .3f, -6.22f), new Vector3(.28f, .4f, .29f), gold, .035f);
            }
            Box(root, "Entrance lintel", new Vector3(27.975f, 4.28f, -6.23f), new Vector3(5.25f, .26f, .4f), cedar, .05f);
            Box(root, "Lintel vermilion cap", new Vector3(27.975f, 4.45f, -6.23f), new Vector3(5.55f, .12f, .47f), coral, .04f);

            // Broad fascia plaque and a modest secondary line preserve the original Japanese hero sign.
            Box(root, "Market identity plaque", new Vector3(17.4f, 5.75f, -7.12f), new Vector3(10.9f, 1.35f, .22f), teal, .09f);
            Box(root, "Plaque brass upper trim", new Vector3(17.4f, 6.44f, -7.13f), new Vector3(11.05f, .065f, .25f), gold, .018f);
            Label(root, "JAPAN MARKET", new Vector3(17.4f, 5.95f, -7.26f), 9.65f, .67f, Cream);
            Label(root, "FRESH FOOD  /  EVERYDAY GOODS", new Vector3(17.4f, 5.37f, -7.26f), 8.8f, .21f, Hex("E6BD7D"));

            // Repeated timber battens add scale and a crisp rhythm to the shaded overhang.
            for (int i = 0; i < 21; i++)
                Box(root, "Soffit cedar batten", new Vector3(10.8f + i * 1.05f, 4.36f, -5.96f), new Vector3(.09f, .1f, 1.7f), cedar, .018f);
            foreach (float x in new[] { 11.2f, 18.4f, 25.6f, 30.35f })
                Box(root, "Facade vertical trim", new Vector3(x, 2.17f, -5.52f), new Vector3(.115f, 4.12f, .09f), cedar, .022f);

            // A flush threshold marker and side edging emphasize the 3 m clear approach.
            Box(root, "Entry welcome inlay", new Vector3(27.95f, .103f, -7.3f), new Vector3(3.15f, .025f, 1.55f), teal, .012f);
            for (int i = 0; i < 6; i++)
                Box(root, "Inlay brass dash", new Vector3(26.69f + i * .5f, .12f, -8.02f), new Vector3(.26f, .012f, .035f), gold, .005f);
            var welcome = Label(root, "WELCOME", new Vector3(27.95f, .125f, -7.26f), 2.35f, .31f, Cream);
            welcome.transform.rotation = Quaternion.Euler(90, 0, 0);
        }

        static void BuildStreetDetails(Transform root)
        {
            // The left return is a quiet merchandise vignette, away from the door and ATM interaction.
            var stall = Group(root, "Seasonal neighborhood stand");
            stall.position = new Vector3(10.35f, .09f, -7.2f);
            stall.rotation = Quaternion.Euler(0, 8, 0);
            LocalBox(stall, "Stand base", new Vector3(0, .38f, 0), new Vector3(1.4f, .76f, .75f), teal, .06f, true);
            for (int i = 0; i < 4; i++)
                LocalBox(stall, "Cedar front slat", new Vector3(0, .16f + .18f * i, -.397f), new Vector3(1.3f, .12f, .06f), cedar, .016f);
            LocalBox(stall, "Stand countertop", new Vector3(0, .79f, 0), new Vector3(1.55f, .1f, .87f), cedar, .035f);
            for (int tray = 0; tray < 2; tray++)
            {
                float x = -.38f + tray * .76f;
                LocalBox(stall, "Fruit tray", new Vector3(x, .9f, 0), new Vector3(.65f, .13f, .68f), dark, .03f);
                for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++)
                {
                    var fruit = MarketArtGeometry.Ellipsoid(stall, tray == 0 ? "Mikan" : "Green pear", Vector3.zero,
                        new Vector3(.18f, tray == 0 ? .18f : .23f, .18f), tray == 0 ? gold : sage);
                    fruit.transform.localPosition = new Vector3(x + (i - 1) * .19f, 1.01f, (j - 1) * .19f);
                }
            }
            LocalBox(stall, "Produce sign stem", new Vector3(0, 1.3f, .32f), new Vector3(.055f, .85f, .055f), cedar, .01f);
            LocalBox(stall, "Produce sign plaque", new Vector3(0, 1.66f, .32f), new Vector3(1.28f, .52f, .075f), cream, .04f);
            var produceLabel = Label(stall, "SEASONAL\nFRESH TODAY", Vector3.zero, 1.11f, .14f, Teal);
            produceLabel.transform.localPosition = new Vector3(0, 1.66f, .27f);
            produceLabel.transform.localRotation = Quaternion.identity;

            // A compact delivery stack tells a restocking story along the side wall.
            Crate(root, new Vector3(32.75f, .11f, -3.65f), 0);
            Crate(root, new Vector3(33.48f, .11f, -3.4f), 7);
            Crate(root, new Vector3(32.76f, .69f, -3.64f), -6);

            // Wall-mounted information leaves the entire sidewalk width available.
            Box(root, "Neighborhood notice frame", new Vector3(31.42f, 2.02f, -1.55f), new Vector3(.12f, 1.28f, 1.58f), cedar, .025f);
            Box(root, "Neighborhood notice back", new Vector3(31.49f, 2.02f, -1.55f), new Vector3(.035f, 1.12f, 1.43f), teal, .01f);
            var notice = Label(root, "NEIGHBORHOOD\nMARKET\n\nFRESH / LOCAL", new Vector3(31.52f, 2.03f, -1.55f), 1.26f, .15f, Cream);
            notice.transform.rotation = Quaternion.Euler(0, -90, 0);

            // Pair of low planters flank, rather than fill, the store's approach.
            Planter(root, new Vector3(9.65f, .08f, -8.35f));
            Planter(root, new Vector3(33.35f, .08f, -7.55f));
        }

        static void BuildInterior(Transform root)
        {
            // Broad high-level accents make an initially empty, player-furnished shop feel intentional.
            Box(root, "Interior back wall timber rail", new Vector3(24.05f, 2.92f, 4.82f), new Vector3(11.7f, .13f, .11f), cedar, .025f);
            Box(root, "Interior dado", new Vector3(24.05f, .63f, 4.81f), new Vector3(11.7f, .92f, .055f), teal, .018f);
            Box(root, "Interior dado cap", new Vector3(24.05f, 1.13f, 4.76f), new Vector3(11.72f, .09f, .12f), cedar, .022f);
            foreach (float x in new[] { 20.0f, 23.2f })
            {
                Box(root, "Back wall category plaque", new Vector3(x, 3.48f, 4.76f), new Vector3(2.6f, .63f, .1f), cream, .045f);
                Label(root, x < 21 ? "PANTRY" : "FRESH DAILY", new Vector3(x, 3.48f, 4.69f), 2.32f, .3f, Teal);
            }
        }

        static void Crate(Transform root, Vector3 position, float angle)
        {
            var crate = Group(root, "Delivery crate");
            crate.position = position; crate.rotation = Quaternion.Euler(0, angle, 0);
            LocalBox(crate, "Contents shadow", new Vector3(0, .22f, 0), new Vector3(.67f, .44f, .56f), dark, .025f, true);
            for (int i = 0; i < 3; i++)
            {
                float y = .09f + .17f * i;
                foreach (float z in new[] { -.31f, .31f }) LocalBox(crate, "Cedar slat", new Vector3(0, y, z), new Vector3(.78f, .13f, .07f), cedar, .015f);
                foreach (float x in new[] { -.37f, .37f }) LocalBox(crate, "Cedar end slat", new Vector3(x, y, 0), new Vector3(.06f, .13f, .58f), cedar, .015f);
            }
            foreach (float x in new[] { -.3f, .3f })
                LocalBox(crate, "Crate brace", new Vector3(x, .26f, -.36f), new Vector3(.07f, .52f, .04f), cedar, .01f);
            var label = Label(crate, "FRESH", Vector3.zero, .47f, .115f, Cream);
            label.transform.localPosition = new Vector3(0, .27f, -.365f);
            label.transform.localRotation = Quaternion.identity;
        }

        static void Planter(Transform root, Vector3 position)
        {
            var planter = Group(root, "Low ceramic planter"); planter.position = position;
            LocalBox(planter, "Ceramic container", new Vector3(0, .29f, 0), new Vector3(.86f, .58f, .86f), sage, .09f, true);
            LocalBox(planter, "Lip", new Vector3(0, .58f, 0), new Vector3(.94f, .12f, .94f), cream, .045f);
            LocalBox(planter, "Soil", new Vector3(0, .644f, 0), new Vector3(.75f, .02f, .75f), dark, .009f);
            for (int i = 0; i < 5; i++)
            {
                float angle = i * 2.4f;
                var foliage = MarketArtGeometry.Ellipsoid(planter, "Sculpted jade foliage", Vector3.zero,
                    new Vector3(.54f, .42f, .53f), leaf);
                foliage.transform.localPosition = new Vector3(Mathf.Cos(angle) * .23f, .78f + (i % 2) * .14f, Mathf.Sin(angle) * .23f);
            }
        }

        static void MakeReviewViews(Transform root)
        {
            var views = Group(root, "04 - Art review viewpoints (not gameplay cameras)");
            Marker(views, "Hero", new Vector3(39, 10.5f, -25), new Vector3(21.5f, 2.8f, -1.5f));
            Marker(views, "Entry", new Vector3(27.8f, 2.3f, -15.2f), new Vector3(27.8f, 2.3f, -4.5f));
            Marker(views, "Interior", new Vector3(27.8f, 2.25f, -3.7f), new Vector3(22, 2, 4.4f));
        }

        [MenuItem("Japan Market/Art/Capture polished market")]
        public static void CaptureAfter()
        {
            var cameraObject = new GameObject("Temporary art capture");
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 53; camera.farClipPlane = 200; camera.nearClipPlane = .1f;
            var data = camera.GetUniversalAdditionalCameraData();
            var gameplayCamera = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).FirstOrDefault(c => c.CompareTag("MainCamera"));
            if (gameplayCamera != null)
            {
                var source = new SerializedObject(gameplayCamera.GetUniversalAdditionalCameraData());
                data.SetRenderer(source.FindProperty("m_RendererIndex").intValue);
            }
            data.renderPostProcessing = true; data.dithering = false;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            try
            {
                foreach (Transform view in GameObject.Find(RootName + "/04 - Art review viewpoints (not gameplay cameras)").transform)
                {
                    camera.transform.SetPositionAndRotation(view.position, view.rotation);
                    MarketSceneInspection.Capture(camera, "after-" + view.name.ToLowerInvariant());
                }
                MarketSceneInspection.Shot(camera, new Vector3(0, 23, -33), new Vector3(22, 0, -5), "after-overview");
                if (gameplayCamera != null)
                {
                    camera.transform.SetPositionAndRotation(gameplayCamera.transform.position, gameplayCamera.transform.rotation);
                    camera.fieldOfView = gameplayCamera.fieldOfView;
                    MarketSceneInspection.Capture(camera, "after-player");
                }
            }
            finally { Object.DestroyImmediate(cameraObject); }
        }

        static void Validate(Scene scene, Transform root)
        {
            var missing = root.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.sharedMaterials.Any(m => m == null || m.shader == null || !m.shader.isSupported)).ToArray();
            if (missing.Length > 0) throw new InvalidOperationException("Missing/unsupported art material: " + string.Join(",", missing.Select(r => r.name)));
            var clearRoute = new Bounds(new Vector3(27.95f, 1.55f, -7.25f), new Vector3(3f, 2.7f, 4.1f));
            var blockers = root.GetComponentsInChildren<Collider>(true).Where(c => c.enabled && !c.isTrigger && c.bounds.Intersects(clearRoute)).ToArray();
            if (blockers.Length > 0) throw new InvalidOperationException("New art blocks door approach: " + string.Join(",", blockers.Select(c => c.name)));
            File.WriteAllText(MarketSceneInspection.Output + "/validation.txt",
                "Scene: " + scene.path + "\nNew art renderers: " + root.GetComponentsInChildren<Renderer>(true).Length +
                "\nNew art colliders: " + root.GetComponentsInChildren<Collider>(true).Length +
                "\nMissing/unsupported art materials: 0\nNew blockers in 3m door approach: 0\n" +
                "Main gameplay cameras: " + Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(c => c.CompareTag("MainCamera")) +
                "\nValidation checks authored geometry/material references; gameplay testing is separate.\n");
        }

        static Material Material(string name, Color color, float smoothness, float emission = 0)
        {
            string path = Art + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
            material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", smoothness); material.SetFloat("_Metallic", 0);
            material.SetColor("_EmissionColor", color * emission);
            if (emission > 0) material.EnableKeyword("_EMISSION"); else material.DisableKeyword("_EMISSION");
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material CloneMaterial(Material original, string name)
        {
            string path = Art + "/Materials/" + name + ".mat";
            var clone = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (clone == null) { clone = new Material(original) { name = name }; AssetDatabase.CreateAsset(clone, path); }
            EditorUtility.SetDirty(clone); return clone;
        }

        static GameObject Label(Transform parent, string text, Vector3 position, float width, float height, Color color)
        {
            if (font == null)
            {
                string path = Art + "/Typography/Market Sign Font.asset";
                font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (font == null)
                {
                    var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Font/Archivo_Black/ArchivoBlack-Regular.ttf");
                    font = TMP_FontAsset.CreateFontAsset(source, 64, 6, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024);
                    font.name = "Market Sign Font";
                    AssetDatabase.CreateAsset(font, path);
                    font.TryAddCharacters("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 /-.");
                    foreach (var atlas in font.atlasTextures) if (!AssetDatabase.Contains(atlas)) AssetDatabase.AddObjectToAsset(atlas, font);
                    if (!AssetDatabase.Contains(font.material)) AssetDatabase.AddObjectToAsset(font.material, font);
                    font.atlasPopulationMode = AtlasPopulationMode.Static;
                    EditorUtility.SetDirty(font);
                }
            }
            var go = new GameObject("Sign - " + text.Replace('\n', ' '));
            go.transform.SetParent(parent, false); go.transform.position = position;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.font = font; tmp.text = text; tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center; tmp.fontSize = height * 10;
            tmp.rectTransform.sizeDelta = new Vector2(width, height * (text.Contains('\n') ? 5 : 1.6f));
            tmp.textWrappingMode = TextWrappingModes.NoWrap; tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.ForceMeshUpdate();
            if (tmp.preferredWidth > width) tmp.fontSize *= width / tmp.preferredWidth;
            tmp.renderer.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size, Material mat, float bevel = .035f, bool solid = false)
            => MarketArtGeometry.Box(parent, name, position, size, mat, bevel, solid);
        static GameObject LocalBox(Transform parent, string name, Vector3 position, Vector3 size, Material mat, float bevel = .035f, bool solid = false)
        {
            var go = Box(parent, name, Vector3.zero, size, mat, bevel, solid);
            go.transform.localPosition = position; go.transform.localRotation = Quaternion.identity; return go;
        }
        static Transform Group(Transform parent, string name) { var t = new GameObject(name).transform; t.SetParent(parent, false); return t; }
        static void Marker(Transform parent, string name, Vector3 position, Vector3 target) { var t = Group(parent, name); t.SetPositionAndRotation(position, Quaternion.LookRotation(target - position)); }
        static void Assign(Renderer renderer, Material[] materials) { renderer.sharedMaterials = materials; PrefabUtility.RecordPrefabInstancePropertyModifications(renderer); EditorUtility.SetDirty(renderer); }
        static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out var color); return color; }
        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
