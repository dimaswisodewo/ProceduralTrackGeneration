using UnityEngine;

/// <summary>
/// Manages the menu behavior and triggers gameplay scene loading when dialogues end.
/// </summary>
public class MenuManager : MonoBehaviour {
    [Header("References")]
    [SerializeField] private SceneLoader sceneLoader;

    [Header("Settings")]
    [SerializeField] private string targetSceneName = "GamePlayScene";

    private void Start() {
        // Automatically resolve the SceneLoader instance if not assigned
        if (sceneLoader == null) {
            sceneLoader = SceneLoader.Instance;
        }
    }

    public void OnDialogueEnd() {
        Debug.Log($"MenuManager: Dialogue ended. Loading target scene: {targetSceneName}");
        if (sceneLoader != null) {
            sceneLoader.LoadScene(targetSceneName);
        } else {
            // Safe fallback to direct loading if SceneLoader reference is unavailable
            UnityEngine.SceneManagement.SceneManager.LoadScene(targetSceneName);
        }
    }
}
