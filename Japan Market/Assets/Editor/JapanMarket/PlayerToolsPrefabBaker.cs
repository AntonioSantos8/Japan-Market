using System;
using System.Linq;
using JapanMarket.Gameplay;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JapanMarket.Editor
{
    /// <summary>
    /// One-time prefab migration: tools must exist in the Player hierarchy so
    /// their clips can be authored against one shared Animator.
    /// </summary>
    [InitializeOnLoad]
    internal static class PlayerToolsPrefabBaker
    {
        private const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
        private const string AnimatorControllerPath = "Assets/Animations/Tools/Tools.controller";

        private static readonly string[] ToolPrefabPaths =
        {
            "Assets/JapanMarket/Setup/Mao_Esponja.prefab",
            "Assets/JapanMarket/Setup/Mao_Rodo.prefab",
            "Assets/JapanMarket/Setup/Mao_Tablet.prefab",
            "Assets/JapanMarket/Setup/Mao_Engradado.prefab",
            "Assets/JapanMarket/Setup/Mao_Taco.prefab"
        };

        static PlayerToolsPrefabBaker()
        {
            EditorApplication.delayCall += BakeIfNeeded;
            EditorSceneManager.sceneOpened += OnSceneOpened;
        }

        private static void OnSceneOpened(Scene _, OpenSceneMode __) =>
            EditorApplication.delayCall += RemoveOldSceneOverrides;

        [MenuItem("Tools/Japan Market/Tools/Preparar Tools no Player Prefab")]
        private static void BakeFromMenu() => Bake(force: true);

        private static void BakeIfNeeded()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Bake(force: false);
        }

        private static void Bake(bool force)
        {
            EnsureAnimatorParameters();
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            if (root == null) return;

            try
            {
                Camera playerCamera = root.GetComponentInChildren<Camera>(true);
                if (playerCamera == null)
                    throw new InvalidOperationException("Player prefab não possui uma Camera.");

                Transform hand = playerCamera.transform.Find("Tool Hand");
                bool alreadyPrepared = hand != null
                    && ToolPrefabPaths.All(path =>
                    {
                        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                        return prefab != null && hand.Find(prefab.name) != null;
                    });

                if (alreadyPrepared && !force)
                {
                    // O prefab pode já estar pronto enquanto uma cena aberta
                    // ainda conserva os componentes/mão que eram overrides.
                    // A limpeza precisa acontecer também nesse caminho.
                    EditorApplication.delayCall += RemoveOldSceneOverrides;
                    return;
                }

                if (hand == null)
                {
                    var handObject = new GameObject("Tool Hand");
                    hand = handObject.transform;
                    hand.SetParent(playerCamera.transform, false);
                    hand.localPosition = new Vector3(0.3f, -0.25f, 0.5f);
                }

                Animator animator = hand.GetComponent<Animator>();
                if (animator == null) animator = hand.gameObject.AddComponent<Animator>();
                animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                    AnimatorControllerPath);

                foreach (string path in ToolPrefabPaths)
                {
                    GameObject toolPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (toolPrefab == null || hand.Find(toolPrefab.name) != null) continue;

                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(toolPrefab, hand);
                    instance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                    instance.SetActive(false);
                }

                ToolUser toolUser = root.GetComponent<ToolUser>();
                if (toolUser == null) toolUser = root.AddComponent<ToolUser>();
                var serializedToolUser = new SerializedObject(toolUser);
                serializedToolUser.FindProperty("_hand").objectReferenceValue = hand;
                serializedToolUser.FindProperty("_toolAnimator").objectReferenceValue = animator;
                serializedToolUser.FindProperty("_aimOrigin").objectReferenceValue = playerCamera.transform;
                serializedToolUser.ApplyModifiedPropertiesWithoutUndo();

                if (root.GetComponent<SetupToolInput>() == null)
                    root.AddComponent<SetupToolInput>();

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            RemoveOldSceneOverrides();
            AssetDatabase.SaveAssets();
            Debug.Log("[Tools] Todas as tools foram adicionadas ao prefab Player e usam o mesmo Animator.");
        }

        private static void EnsureAnimatorParameters()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                AnimatorControllerPath);
            if (controller == null) return;

            if (controller.parameters.All(parameter => parameter.name != "Using"))
            {
                controller.AddParameter("Using", AnimatorControllerParameterType.Bool);
                EditorUtility.SetDirty(controller);
            }
        }

        private static void RemoveOldSceneOverrides()
        {
            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.isLoaded) continue;

                bool changed = false;
                foreach (ToolUser toolUser in UnityEngine.Object.FindObjectsByType<ToolUser>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (toolUser.gameObject.scene != scene) continue;
                    bool isLegacyOverride = PrefabUtility.IsAddedComponentOverride(toolUser)
                        || (PrefabUtility.IsPartOfPrefabInstance(toolUser.gameObject)
                            && PrefabUtility.GetCorrespondingObjectFromSource(toolUser) == null);
                    if (!isLegacyOverride) continue;

                    SetupToolInput input = toolUser.GetComponent<SetupToolInput>();
                    bool inputIsLegacyOverride = input != null
                        && (PrefabUtility.IsAddedComponentOverride(input)
                            || (PrefabUtility.IsPartOfPrefabInstance(input.gameObject)
                                && PrefabUtility.GetCorrespondingObjectFromSource(input) == null));
                    if (inputIsLegacyOverride)
                        UnityEngine.Object.DestroyImmediate(input);

                    UnityEngine.Object.DestroyImmediate(toolUser);
                    changed = true;
                }

                foreach (GameObject candidate in scene.GetRootGameObjects()
                             .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                             .Where(item => item.name == "Tool Hand")
                             .Select(item => item.gameObject)
                             .ToArray())
                {
                    Transform parent = candidate.transform.parent;
                    bool isEmptyLegacyHand = parent != null
                        && PrefabUtility.IsPartOfPrefabInstance(parent.gameObject)
                        && candidate.transform.childCount == 0
                        && candidate.GetComponent<Animator>() == null;
                    if (!PrefabUtility.IsAddedGameObjectOverride(candidate) && !isEmptyLegacyHand)
                        continue;
                    UnityEngine.Object.DestroyImmediate(candidate);
                    changed = true;
                }

                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }
        }
    }
}
