using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Quits the application when the Escape key is pressed.
/// Useful for quick testing in the Editor / on a desktop build —
/// a Quest headset itself has no keyboard, so this won't fire
/// through the controllers.
/// </summary>
public class EscapeToQuit : MonoBehaviour
{
    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            QuitApplication();
        }
    }

    private void QuitApplication()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}