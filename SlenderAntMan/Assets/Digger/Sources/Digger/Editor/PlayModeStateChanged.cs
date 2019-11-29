using UnityEditor;
using UnityEngine;

namespace Digger
{
    // ensure class initializer is called whenever scripts recompile
    [InitializeOnLoad]
    public class PlayModeStateChanged
    {
        // register an event handler when the class is initialized
        static PlayModeStateChanged()
        {
            EditorApplication.playModeStateChanged += LogPlayModeState;
        }

        private static void LogPlayModeState(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode) {
                Debug.Log("LogPlayModeState: EnteredEditMode");
                DiggerMasterEditor.OnExitPlayMode();
            } else if (state == PlayModeStateChange.ExitingEditMode) {
                Debug.Log("LogPlayModeState: ExitingEditMode");
                DiggerMasterEditor.OnEnterPlayMode();
            }
        }
    }
}