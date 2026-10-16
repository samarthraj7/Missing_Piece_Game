using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class OpenEchoScene
{
    static OpenEchoScene()
    {
        EditorApplication.delayCall += OpenMainIfNeeded;
    }

    [MenuItem("Echo/Open Main Scene")]
    public static void OpenMain()
    {
        if (EditorApplication.isPlaying) return;
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
    }

    static void OpenMainIfNeeded()
    {
        if (EditorApplication.isPlaying) return;
        string path = SceneManager.GetActiveScene().path;
        if (path.Contains("_Recovery") || path.Contains("__Backupscenes") || string.IsNullOrEmpty(path))
            OpenMain();
    }
}
