using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Digger
{
    [InitializeOnLoad]
    public class SceneManagerEventHandler
    {
        private static Scene currentScene;

        static SceneManagerEventHandler()
        {
            currentScene = SceneManager.GetActiveScene();
            EditorApplication.hierarchyChanged += HierarchyWindowChanged;
        }

        private static void HierarchyWindowChanged()
        {
            if (currentScene != SceneManager.GetActiveScene()) {
                currentScene = SceneManager.GetActiveScene();
                Debug.Log("OnSceneLoaded: LoadAllChunks");
                DiggerMasterEditor.LoadAllChunks();
            }
        }
    }
}