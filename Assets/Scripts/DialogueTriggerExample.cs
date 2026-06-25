using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// A simple example component demonstrating how to trigger dialogue conversations.
/// </summary>
public class DialogueTriggerExample : MonoBehaviour {
    [Header("Dialogue Source")]
    [Tooltip("Path of the JSON dialogue file in Resources (without extension). E.g. 'dialogue_example'")]
    public string jsonResourcePath;

    [Tooltip("Reference to a pre-defined conversation ScriptableObject.")]
    public DialogueConversation conversationAsset;

    [Header("Trigger Settings")]
    [Tooltip("If true, dialogue starts as soon as this component's Start method runs.")]
    public bool triggerOnStart = false;

    [Tooltip("Trigger key to start the dialogue at runtime.")]
    public KeyCode triggerKey = KeyCode.T;

    private void Start() {
        if (triggerOnStart) {
            TriggerDialogue();
        }
    }

    private void Update() {
        if (GetTriggerKeyPressed()) {
            TriggerDialogue();
        }
    }

    private bool GetTriggerKeyPressed() {
#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        if (keyboard == null) return false;

        string keyName = triggerKey.ToString();
        // Convert legacy KeyCode names to InputSystem Key enum names
        if (keyName.StartsWith("Alpha")) {
            keyName = "Digit" + keyName.Substring(5);
        }

        if (System.Enum.TryParse<Key>(keyName, true, out var key)) {
            return keyboard[key].wasPressedThisFrame;
        }
        return false;
#else
        return Input.GetKeyDown(triggerKey);
#endif
    }

    /// <summary>
    /// Starts the dialogue sequence from the configured source.
    /// </summary>
    public void TriggerDialogue() {
        if (VisualNovelDialogueManager.Instance == null) {
            Debug.LogError("DialogueTriggerExample: VisualNovelDialogueManager Instance not found in the scene.");
            return;
        }

        if (!string.IsNullOrEmpty(jsonResourcePath)) {
            Debug.Log($"DialogueTriggerExample: Starting dialogue from JSON path: Resources/{jsonResourcePath}");
            VisualNovelDialogueManager.Instance.StartDialogueFromJson(jsonResourcePath);
        } else if (conversationAsset != null) {
            Debug.Log($"DialogueTriggerExample: Starting dialogue conversation asset: {conversationAsset.name}");
            VisualNovelDialogueManager.Instance.StartDialogue(conversationAsset);
        } else {
            Debug.LogWarning("DialogueTriggerExample: No JSON resource path or Dialogue conversation asset configured.");
        }
    }
}
