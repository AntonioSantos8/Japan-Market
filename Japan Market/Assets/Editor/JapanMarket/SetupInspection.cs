using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SetupInspection
{
    public static void Run()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        var lines = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
            .Where(t => t.GetComponents<Component>().Any(c => c != null && c.GetType().Assembly.GetName().Name == "Assembly-CSharp") || t.name.ToLower().Contains("door"))
            .Select(t => t.name + " @ " + t.position + " : " + string.Join(",", t.GetComponents<Component>().Where(c => c != null).Select(c => c.GetType().Name)));
        File.WriteAllLines("Logs/setup-inspection.txt", lines);
        Debug.Log("SETUP_INSPECTION_DONE");
    }
}
