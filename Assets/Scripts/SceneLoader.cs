using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// A centralized manager for handling scene loading operations.
/// </summary>
public class SceneLoader : MonoBehaviour {
    private static SceneLoader instance;
    public static SceneLoader Instance {
        get {
            if (instance == null) {
                instance = FindObjectOfType<SceneLoader>();
                if (instance == null) {
                    GameObject go = new GameObject("SceneLoader");
                    instance = go.AddComponent<SceneLoader>();
                    DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
    }

    private void Awake() {
        if (instance == null) {
            instance = this;
            DontDestroyOnLoad(gameObject);
        } else if (instance != this) {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Loads a scene synchronously by name.
    /// </summary>
    public void LoadScene(string sceneName) {
        SceneManager.LoadScene(sceneName);
    }

    /// <summary>
    /// Loads a scene asynchronously by name, returning the AsyncOperation.
    /// </summary>
    public AsyncOperation LoadSceneAsync(string sceneName) {
        return SceneManager.LoadSceneAsync(sceneName);
    }

    /// <summary>
    /// Gets the name of the currently active scene.
    /// </summary>
    public string GetActiveSceneName() {
        return SceneManager.GetActiveScene().name;
    }
}
