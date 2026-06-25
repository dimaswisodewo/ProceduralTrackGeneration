using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A simple example component demonstrating how to trigger dialogue conversations.
/// </summary>
public class DialogueTriggerExample : MonoBehaviour {
    [Header("Conversation Settings")]
    [Tooltip("Reference to a pre-defined conversation ScriptableObject.")]
    [SerializeField] private DialogueConversation conversationAsset;

    [Tooltip("Inline dialogue list. If conversationAsset is null, the manager will play these lines.")]
    [SerializeField] private List<DialogueLine> inlineDialogue = new List<DialogueLine>();

    [Tooltip("If true, dialogue starts as soon as this component's Start method runs.")]
    [SerializeField] private bool triggerOnStart = false;

    [Tooltip("Trigger key to start the dialogue at runtime.")]
    [SerializeField] private KeyCode triggerKey = KeyCode.T;

    private void Start() {
        if (triggerOnStart) {
            TriggerDialogue();
        }
    }

    private void Update() {
        if (Input.GetKeyDown(triggerKey)) {
            TriggerDialogue();
        }
    }

    /// <summary>
    /// Starts the dialogue sequence.
    /// </summary>
    public void TriggerDialogue() {
        if (VisualNovelDialogueManager.Instance == null) {
            Debug.LogError("DialogueTriggerExample: VisualNovelDialogueManager Instance not found in the scene.");
            return;
        }

        if (conversationAsset != null) {
            Debug.Log($"DialogueTriggerExample: Starting dialogue conversation asset: {conversationAsset.name}");
            VisualNovelDialogueManager.Instance.StartDialogue(conversationAsset);
        } else if (inlineDialogue != null && inlineDialogue.Count > 0) {
            Debug.Log("DialogueTriggerExample: Starting inline dialogue lines.");
            VisualNovelDialogueManager.Instance.StartDialogue(inlineDialogue);
        } else {
            Debug.LogWarning("DialogueTriggerExample: No dialogue conversation asset or inline dialogue lines configured to play.");
        }
    }
}
