using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JapanMarket.Data;
using JapanMarket.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace JapanMarket.Editor
{
    public static class TrashEquipmentSetup
    {
        private const string Folder = "Assets/JapanMarket/Setup/";

        [MenuItem("Tools/Japan Market/Tools/Preparar saco de lixo")]
        public static void Apply()
        {
            CreateEquipment();
            MigrateFloorTrash();
            MigrateBins();
            BakePlayer();
            AssetDatabase.SaveAssets();
            Debug.Log("[TrashEquipment] Saco, toolwheel, lixeiras e coleta configurados.");
        }

        private static void CreateEquipment()
        {
            string modelPath = Folder + "Mao_SacoDeLixo.prefab";
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null)
            {
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.color = new Color(.07f, .12f, .1f);
                material.SetFloat("_Smoothness", .35f);
                AssetDatabase.CreateAsset(material, Folder + "SacoDeLixo.mat");
                var root = new GameObject("Mao_SacoDeLixo");
                try
                {
                    Shape(root.transform, "Saco", new Vector3(-.1f, .23f, 0), new Vector3(.22f, .3f, .2f), material);
                    Shape(root.transform, "No", new Vector3(-.1f, .375f, 0), new Vector3(.07f, .055f, .07f), material);
                    Shape(root.transform, "Ponta", new Vector3(-.085f, .415f, 0), new Vector3(.045f, .08f, .04f), material);
                    model = PrefabUtility.SaveAsPrefabAsset(root, modelPath);
                }
                finally { Object.DestroyImmediate(root); }
            }
            ToolDefinition tool = AssetDatabase.LoadAssetAtPath<ToolDefinition>(Folder + "SacoDeLixo.asset");
            if (tool == null)
            {
                tool = ScriptableObject.CreateInstance<ToolDefinition>();
                AssetDatabase.CreateAsset(tool, Folder + "SacoDeLixo.asset");
                tool.EditorInitialize(Array.Empty<ToolSurface>());
                var data = new SerializedObject(tool);
                data.FindProperty("_isTrashBag").boolValue = true;
                data.FindProperty("_trashCapacity").intValue = 10;
                data.FindProperty("_heldPrefab").objectReferenceValue = model;
                var entries = data.FindProperty("_displayName").FindPropertyRelative("_entries");
                entries.arraySize = 1;
                entries.GetArrayElementAtIndex(0).FindPropertyRelative("language").intValue = 0;
                entries.GetArrayElementAtIndex(0).FindPropertyRelative("text").stringValue = "Saco de lixo";
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            var serialized = new SerializedObject(tool);
            serialized.FindProperty("_icon").objectReferenceValue = CreateIcon();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            ToolBeltLayout layout = AssetDatabase.LoadAssetAtPath<ToolBeltLayout>(Folder + "ToolBeltLayout.asset");
            if (layout == null) throw new InvalidOperationException("ToolBeltLayout não encontrado.");
            if (!layout.Slots.Any(slot => slot.Tool == tool))
            {
                var slots = layout.Slots.ToList();
                slots.Add(new ToolSlotLayout { Tool = tool });
                layout.EditorSetSlots(slots.ToArray());
                EditorUtility.SetDirty(layout);
            }
        }

        private static void Shape(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject shape = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shape.name = name;
            shape.transform.SetParent(parent, false);
            shape.transform.localPosition = position;
            shape.transform.localScale = scale;
            shape.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(shape.GetComponent<Collider>());
        }

        private static Sprite CreateIcon()
        {
            string path = Folder + "SacoDeLixoIcon.png";
            if (!File.Exists(path))
            {
                var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 64; x++)
                    {
                        float dx = (x - 32) / 22f, dy = (y - 27) / 25f;
                        bool body = dx * dx + dy * dy <= 1 && y < 50;
                        bool tie = y >= 48 && y < 59 && Mathf.Abs(x - 32) < 5 + (y - 48) / 3;
                        texture.SetPixel(x, y, body || tie ? new Color(.25f + x / 300f, .7f, .57f) : Color.clear);
                    }
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void MigrateFloorTrash()
        {
            var paths = new HashSet<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:TrashDefinition"))
            {
                var definition = AssetDatabase.LoadAssetAtPath<TrashDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (definition.Prefab != null) paths.Add(AssetDatabase.GetAssetPath(definition.Prefab));
            }
            foreach (string guid in AssetDatabase.FindAssets("t:TrashData"))
            {
                var definition = AssetDatabase.LoadAssetAtPath<TrashData>(AssetDatabase.GUIDToAssetPath(guid));
                if (definition.prefab != null) paths.Add(AssetDatabase.GetAssetPath(definition.prefab));
            }
            foreach (string path in paths)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (HoldableItem old in root.GetComponentsInChildren<HoldableItem>(true)) Object.DestroyImmediate(old);
                    foreach (PhysicsHoldableItem old in root.GetComponentsInChildren<PhysicsHoldableItem>(true)) Object.DestroyImmediate(old);
                    if (root.GetComponent<TrashCollectionInteraction>() == null) root.AddComponent<TrashCollectionInteraction>();
                    int layer = LayerMask.NameToLayer("Interactive");
                    if (layer < 0) throw new InvalidOperationException("Layer Interactive não encontrada.");
                    foreach (Transform node in root.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = layer;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }

        private static void MigrateBins()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string yaml = File.ReadAllText(path);
                if (!yaml.Contains("2e15f90f795aeb74e8c2d9053ed91e17") && path != Folder + "Lixeira.prefab") continue;
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (AddBinInteractions(root)) PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            foreach (string path in new[] { "Assets/Scenes/Main.unity", "Assets/Scenes/Sandbox.unity" })
            {
                var scene = SceneManager.GetSceneByPath(path);
                bool wasLoaded = scene.IsValid() && scene.isLoaded;
                if (!wasLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                bool changed = false;
                foreach (GameObject root in scene.GetRootGameObjects()) changed |= AddBinInteractions(root);
                if (changed) EditorSceneManager.SaveScene(scene);
                if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static bool AddBinInteractions(GameObject root)
        {
            bool changed = false;
            var bins = root.GetComponentsInChildren<global::TrashBin>(true).Select(bin => bin.gameObject)
                .Concat(root.GetComponentsInChildren<JapanMarket.Gameplay.TrashBin>(true).Select(bin => bin.gameObject));
            foreach (GameObject bin in bins.Distinct())
            {
                if (bin.GetComponent<TrashBinInteraction>() != null) continue;
                bin.AddComponent<TrashBinInteraction>();
                changed = true;
            }
            return changed;
        }

        private static void BakePlayer()
        {
            string path = "Assets/Prefabs/Player/Player.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform hand = root.GetComponentsInChildren<Transform>(true).First(node => node.name == "Tool Hand");
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Mao_SacoDeLixo.prefab");
                if (hand.Find(prefab.name) == null)
                {
                    var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, hand);
                    model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                    model.SetActive(false);
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
