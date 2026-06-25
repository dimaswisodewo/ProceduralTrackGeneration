using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using DG.Tweening;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Defines the display actions for visual novel image panels.
/// </summary>
public enum ImagePanelAction {
    Keep,          // Do nothing to the panel's state.
    ShowOrUpdate,  // Set active, swap sprite, and fade in / transition.
    Hide           // Fade out and set inactive.
}

/// <summary>
/// Defines a single transition operation on an image panel.
/// </summary>
[System.Serializable]
public struct DialogueImageOperation {
    [Tooltip("Unique key of the target Image Panel defined in the Dialogue Manager.")]
    public string panelKey;

    [Tooltip("Action to perform on this panel.")]
    public ImagePanelAction action;

    [Tooltip("Sprite to show on the panel. Ignored if action is Keep or Hide.")]
    public Sprite spriteToDisplay;

    [Tooltip("Whether the image aspect ratio should be preserved.")]
    public bool preserveAspect;

    [Tooltip("Fade transition duration in seconds (using DOTween). Use 0 for instant swap.")]
    public float fadeDuration;

    [Tooltip("Custom scale for the image panel. If Vector3.zero, defaults to Vector3.one.")]
    public Vector3 customScale;
}

/// <summary>
/// Structure representing a single line of visual novel dialogue.
/// </summary>
[System.Serializable]
public class DialogueLine {
    [Tooltip("Name of the speaker character. If empty, the speaker name box can be hidden.")]
    public string speakerName;

    [TextArea(3, 10)]
    [Tooltip("The line of dialogue to be printed. Supports standard Unity Rich Text (e.g. <b>, <i>, <color=...>).")]
    public string dialogueText;

    [Tooltip("Multiplier for the base text writing speed. Higher values write faster.")]
    public float typingSpeedMultiplier = 1f;

    [Tooltip("List of image panel actions to execute when this line starts.")]
    public List<DialogueImageOperation> imageOperations;
}

/// <summary>
/// Configuration mapping a unique key to a UI Image component in the Dialogue Canvas.
/// </summary>
[System.Serializable]
public struct ImagePanelConfig {
    [Tooltip("Unique key of this image panel (e.g., 'LeftCharacter', 'RightCharacter', 'Background').")]
    public string panelKey;

    [Tooltip("The UI Image component corresponding to this panel.")]
    public Image imageComponent;

    [Tooltip("Optional CanvasGroup on the panel for fading. If null, fades the Image directly.")]
    public CanvasGroup canvasGroup;
}

/// <summary>
/// ScriptableObject wrapper to store pre-configured dialogue conversations.
/// </summary>
[CreateAssetMenu(fileName = "NewConversation", menuName = "Visual Novel/Conversation")]
public class DialogueConversation : ScriptableObject {
    [Tooltip("Lines of dialogue in the conversation.")]
    public List<DialogueLine> lines = new List<DialogueLine>();
}

/// <summary>
/// Manager class that orchestrates running text (typewriter effect) and dynamic image panel fades.
/// </summary>
public class VisualNovelDialogueManager : MonoBehaviour {
    private static VisualNovelDialogueManager instance;
    public static VisualNovelDialogueManager Instance {
        get {
            if (instance == null) {
                instance = FindObjectOfType<VisualNovelDialogueManager>();
                if (instance == null) {
                    GameObject go = new GameObject("VisualNovelDialogueManager");
                    instance = go.AddComponent<VisualNovelDialogueManager>();
                }
            }
            return instance;
        }
    }

    [Header("UI Component References")]
    [Tooltip("The main container GameObject of the dialogue panel. This is shown when dialogue starts and hidden when it ends.")]
    [SerializeField] private GameObject dialogueContainer;

    [Tooltip("UI Text component for rendering the speaker's name. Optional.")]
    [SerializeField] private Text speakerNameText;

    [Tooltip("The container GameObject of the speaker name UI. If assigned, will hide when speakerName is empty.")]
    [SerializeField] private GameObject speakerNameContainer;

    [Tooltip("UI Text component for rendering the dialogue running text.")]
    [SerializeField] private Text dialogueText;

    [Header("Image Panels Configuration")]
    [Tooltip("Register the available image panels here. Define keys like 'Left', 'Right', 'BG' and link their UI Images.")]
    [SerializeField] private List<ImagePanelConfig> imagePanels = new List<ImagePanelConfig>();

    [Header("Text Settings")]
    [Tooltip("Base speed of typewriter effect in characters per second.")]
    [SerializeField] private float baseCharactersPerSecond = 30f;

    [Tooltip("If true, dialogue can be advanced using Space or Mouse Left Click.")]
    [SerializeField] private bool advanceOnKeyInput = true;

    [Header("Transitions & Tweening")]
    [Tooltip("Transition duration to fade the dialogue container in/out.")]
    [SerializeField] private float containerFadeDuration = 0.3f;

    [Header("Events")]
    public UnityEvent OnDialogueStart;
    public UnityEvent OnDialogueEnd;
    public UnityEvent<DialogueLine> OnLineStart;
    public UnityEvent<DialogueLine> OnLineComplete;

    // Runtime state tracking
    private List<DialogueLine> currentLines = new List<DialogueLine>();
    private int currentLineIndex = -1;
    private bool isDialogueActive = false;
    private bool isTypingText = false;
    private string fullTextOfCurrentLine = "";
    private int totalVisibleChars = 0;
    private Coroutine typingCoroutine;
    private CanvasGroup containerCanvasGroup;

    // Fast mapping for image panels runtime control
    private Dictionary<string, ImagePanelConfig> panelMap = new Dictionary<string, ImagePanelConfig>();
    private Dictionary<string, Tween> panelTweens = new Dictionary<string, Tween>();

    private void Awake() {
        if (instance == null) {
            instance = this;
        } else if (instance != this) {
            Destroy(gameObject);
            return;
        }

        InitializePanelMap();
        InitializeDialogueContainer();
    }

    private void Start() {
        // Automatically hook up custom font manager fonts to our text components
        ApplyCustomFonts();
    }

    private void Update() {
        if (!isDialogueActive) return;

        if (advanceOnKeyInput && GetAdvanceInputPressed()) {
            AdvanceDialogue();
        }
    }

    /// <summary>
    /// Checks for input to advance dialogue.
    /// </summary>
    private bool GetAdvanceInputPressed() {
#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        bool spacePressed = keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
        bool leftClickPressed = mouse != null && mouse.leftButton.wasPressedThisFrame;
        return spacePressed || leftClickPressed;
#else
        return Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0);
#endif
    }

    /// <summary>
    /// Registers panels from config list into a dictionary for quick lookup.
    /// </summary>
    private void InitializePanelMap() {
        panelMap.Clear();
        foreach (var config in imagePanels) {
            if (string.IsNullOrEmpty(config.panelKey)) continue;

            if (panelMap.ContainsKey(config.panelKey)) {
                Debug.LogWarning($"VisualNovelDialogueManager: Duplicate panel key '{config.panelKey}' detected!");
                continue;
            }

            panelMap.Add(config.panelKey, config);

            // Set initial alpha/state of panels to hidden
            SetPanelAlpha(config, 0f);
            config.imageComponent.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Gets or adds CanvasGroup to the dialogue container to handle fade animations.
    /// </summary>
    private void InitializeDialogueContainer() {
        if (dialogueContainer != null) {
            containerCanvasGroup = dialogueContainer.GetComponent<CanvasGroup>();
            if (containerCanvasGroup == null) {
                containerCanvasGroup = dialogueContainer.AddComponent<CanvasGroup>();
            }
            containerCanvasGroup.alpha = 0f;
            dialogueContainer.SetActive(false);
        }
    }

    /// <summary>
    /// Applies custom project fonts to the text components if the FontManager exists.
    /// </summary>
    private void ApplyCustomFonts() {
        if (FontManager.Instance != null) {
            if (speakerNameText != null) FontManager.Instance.ApplyFontToText(speakerNameText);
            if (dialogueText != null) FontManager.Instance.ApplyFontToText(dialogueText);
        }
    }

    /// <summary>
    /// Begins a dialogue sequence from a DialogueConversation ScriptableObject.
    /// </summary>
    public void StartDialogue(DialogueConversation conversation) {
        if (conversation == null) {
            Debug.LogError("VisualNovelDialogueManager: Cannot start dialogue with null Conversation.");
            return;
        }
        StartDialogue(conversation.lines);
    }

    /// <summary>
    /// Begins a dialogue sequence from a raw list of DialogueLines.
    /// </summary>
    public void StartDialogue(List<DialogueLine> lines) {
        if (lines == null || lines.Count == 0) {
            Debug.LogWarning("VisualNovelDialogueManager: Cannot start dialogue with empty dialogue lines list.");
            return;
        }

        currentLines = new List<DialogueLine>(lines);
        currentLineIndex = 0;
        isDialogueActive = true;

        OnDialogueStart?.Invoke();

        // Animate the dialogue container in
        if (dialogueContainer != null) {
            dialogueContainer.SetActive(true);
            containerCanvasGroup.DOComplete();
            containerCanvasGroup.DOFade(1f, containerFadeDuration).SetUpdate(true);
        }

        PlayCurrentLine();
    }

    /// <summary>
    /// Advances dialogue. If text is printing, it skips typing and shows full line. Otherwise, it moves to the next line.
    /// </summary>
    public void AdvanceDialogue() {
        if (!isDialogueActive) return;

        if (isTypingText) {
            SkipTyping();
        } else {
            currentLineIndex++;
            if (currentLineIndex < currentLines.Count) {
                PlayCurrentLine();
            } else {
                EndDialogue();
            }
        }
    }

    /// <summary>
    /// Instantly finishes text printing for the current dialogue line.
    /// </summary>
    public void SkipTyping() {
        if (!isTypingText) return;

        if (typingCoroutine != null) {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        dialogueText.text = fullTextOfCurrentLine;
        isTypingText = false;

        OnLineComplete?.Invoke(currentLines[currentLineIndex]);
    }

    /// <summary>
    /// Plays the dialogue line at the current index.
    /// </summary>
    private void PlayCurrentLine() {
        DialogueLine line = currentLines[currentLineIndex];
        OnLineStart?.Invoke(line);

        // Update speaker name UI
        if (speakerNameText != null) {
            speakerNameText.text = line.speakerName;
        }
        if (speakerNameContainer != null) {
            speakerNameContainer.SetActive(!string.IsNullOrEmpty(line.speakerName));
        }

        // Trigger dynamic image panel operations
        ExecuteImageOperations(line.imageOperations);

        // Run typewriter effect
        fullTextOfCurrentLine = line.dialogueText;
        totalVisibleChars = GetVisibleCharacterCount(fullTextOfCurrentLine);

        if (typingCoroutine != null) {
            StopCoroutine(typingCoroutine);
        }

        if (dialogueText != null) {
            typingCoroutine = StartCoroutine(TypeTextCoroutine(line));
        }
    }

    /// <summary>
    /// Coroutine performing the character-by-character printing, handling rich text tags seamlessly.
    /// </summary>
    private IEnumerator TypeTextCoroutine(DialogueLine line) {
        isTypingText = true;
        dialogueText.text = "";

        float speedMultiplier = Mathf.Max(0.01f, line.typingSpeedMultiplier);
        float delayBetweenChars = 1f / (baseCharactersPerSecond * speedMultiplier);

        int visibleCharsShown = 0;
        while (visibleCharsShown <= totalVisibleChars) {
            dialogueText.text = GetParsedText(fullTextOfCurrentLine, visibleCharsShown);
            visibleCharsShown++;
            yield return new WaitForSeconds(delayBetweenChars);
        }

        isTypingText = false;
        OnLineComplete?.Invoke(line);
    }

    /// <summary>
    /// Executes transitions and sprite swaps on registered image panels.
    /// </summary>
    private void ExecuteImageOperations(List<DialogueImageOperation> operations) {
        if (operations == null) return;

        foreach (var op in operations) {
            if (string.IsNullOrEmpty(op.panelKey)) continue;

            if (!panelMap.TryGetValue(op.panelKey, out ImagePanelConfig panel)) {
                Debug.LogWarning($"VisualNovelDialogueManager: Panel key '{op.panelKey}' not registered in manager.");
                continue;
            }

            // Cancel any current tweens running on this panel
            if (panelTweens.TryGetValue(op.panelKey, out Tween existingTween)) {
                existingTween?.Kill();
                panelTweens.Remove(op.panelKey);
            }

            switch (op.action) {
                case ImagePanelAction.ShowOrUpdate:
                    bool wasActive = panel.imageComponent.gameObject.activeSelf;
                    float currentAlpha = GetPanelAlpha(panel);

                    // Setup target sprite and scaling
                    if (op.spriteToDisplay != null) {
                        panel.imageComponent.sprite = op.spriteToDisplay;
                        panel.imageComponent.preserveAspect = op.preserveAspect;
                    }

                    if (op.customScale != Vector3.zero) {
                        panel.imageComponent.transform.localScale = op.customScale;
                    } else {
                        panel.imageComponent.transform.localScale = Vector3.one;
                    }

                    if (!wasActive || currentAlpha < 0.01f) {
                        // Fade in from hidden
                        panel.imageComponent.gameObject.SetActive(true);
                        SetPanelAlpha(panel, 0f);

                        if (op.fadeDuration > 0f) {
                            Tween tween = FadePanel(panel, 1f, op.fadeDuration);
                            panelTweens[op.panelKey] = tween;
                        } else {
                            SetPanelAlpha(panel, 1f);
                        }
                    } else {
                        // Already active. If a new sprite was swapped and we want a visual dip / transition
                        if (op.fadeDuration > 0f) {
                            // Punch/scale pop transition is standard and looks great
                            panel.imageComponent.transform.DOPunchScale(new Vector3(0.05f, 0.05f, 0.05f), op.fadeDuration, 5, 0.5f);
                            Tween tween = FadePanel(panel, 1f, op.fadeDuration);
                            panelTweens[op.panelKey] = tween;
                        }
                    }
                    break;

                case ImagePanelAction.Hide:
                    if (panel.imageComponent.gameObject.activeSelf) {
                        if (op.fadeDuration > 0f) {
                            Tween tween = FadePanel(panel, 0f, op.fadeDuration)
                                .OnComplete(() => panel.imageComponent.gameObject.SetActive(false));
                            panelTweens[op.panelKey] = tween;
                        } else {
                            SetPanelAlpha(panel, 0f);
                            panel.imageComponent.gameObject.SetActive(false);
                        }
                    }
                    break;

                case ImagePanelAction.Keep:
                    // Do nothing
                    break;
            }
        }
    }

    /// <summary>
    /// Helper to fade a panel using DOTween.
    /// </summary>
    private Tween FadePanel(ImagePanelConfig panel, float targetAlpha, float duration) {
        if (panel.canvasGroup != null) {
            return panel.canvasGroup.DOFade(targetAlpha, duration).SetUpdate(true);
        } else {
            return panel.imageComponent.DOFade(targetAlpha, duration).SetUpdate(true);
        }
    }

    /// <summary>
    /// Helper to get current alpha of a panel.
    /// </summary>
    private float GetPanelAlpha(ImagePanelConfig panel) {
        if (panel.canvasGroup != null) {
            return panel.canvasGroup.alpha;
        } else {
            return panel.imageComponent.color.a;
        }
    }

    /// <summary>
    /// Helper to set alpha instantly.
    /// </summary>
    private void SetPanelAlpha(ImagePanelConfig panel, float alpha) {
        if (panel.canvasGroup != null) {
            panel.canvasGroup.alpha = alpha;
        } else {
            Color col = panel.imageComponent.color;
            col.a = alpha;
            panel.imageComponent.color = col;
        }
    }

    /// <summary>
    /// Closes the dialogue interface and resets states.
    /// </summary>
    public void EndDialogue() {
        if (!isDialogueActive) return;

        isDialogueActive = false;
        isTypingText = false;

        if (typingCoroutine != null) {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        // Fade dialogue box out
        if (dialogueContainer != null) {
            containerCanvasGroup.DOComplete();
            containerCanvasGroup.DOFade(0f, containerFadeDuration).SetUpdate(true)
                .OnComplete(() => dialogueContainer.SetActive(false));
        }

        // Fade all active image panels out
        foreach (var entry in panelMap) {
            var panel = entry.Value;
            if (panel.imageComponent.gameObject.activeSelf) {
                if (panelTweens.TryGetValue(entry.Key, out Tween t)) {
                    t?.Kill();
                }

                Tween tween = FadePanel(panel, 0f, containerFadeDuration)
                    .OnComplete(() => panel.imageComponent.gameObject.SetActive(false));
                panelTweens[entry.Key] = tween;
            }
        }

        OnDialogueEnd?.Invoke();
    }

    #region Rich Text Typewriter Helpers

    /// <summary>
    /// Calculates the number of non-tag characters in a rich text string.
    /// </summary>
    public static int GetVisibleCharacterCount(string text) {
        int count = 0;
        int i = 0;
        int length = text.Length;
        while (i < length) {
            if (text[i] == '<') {
                int closeIndex = text.IndexOf('>', i);
                if (closeIndex != -1) {
                    i = closeIndex + 1;
                    continue;
                }
            }
            count++;
            i++;
        }
        return count;
    }

    /// <summary>
    /// Returns a substring of fullText that contains precisely visibleCharsToShow characters,
    /// keeping all surrounding formatting tags intact and properly closing them.
    /// </summary>
    public static string GetParsedText(string fullText, int visibleCharsToShow) {
        System.Text.StringBuilder result = new System.Text.StringBuilder();
        System.Collections.Generic.Stack<string> openTags = new System.Collections.Generic.Stack<string>();
        int visibleCount = 0;
        int i = 0;
        int length = fullText.Length;

        while (i < length) {
            if (fullText[i] == '<') {
                int closeIndex = fullText.IndexOf('>', i);
                if (closeIndex != -1) {
                    string tag = fullText.Substring(i, closeIndex - i + 1);
                    result.Append(tag);
                    i = closeIndex + 1;

                    // Process tag for stack tracking
                    if (tag.StartsWith("</")) {
                        if (openTags.Count > 0) openTags.Pop();
                    } else if (!tag.EndsWith("/>") && !tag.StartsWith("<!--")) {
                        // Get tag name (e.g. "b" from "<b>" or "color" from "<color=red>")
                        int spaceOrEq = tag.IndexOfAny(new char[] { '=', ' ', '>' }, 1);
                        if (spaceOrEq != -1) {
                            string tagName = tag.Substring(1, spaceOrEq - 1);
                            openTags.Push(tagName);
                        }
                    }
                    continue;
                }
            }

            if (visibleCount < visibleCharsToShow) {
                result.Append(fullText[i]);
                visibleCount++;
            }
            i++;
        }

        // Close any tags left open by the substring limit
        while (openTags.Count > 0) {
            string tagName = openTags.Pop();
            result.Append("</").Append(tagName).Append(">");
        }

        return result.ToString();
    }

    #endregion

    private void OnDestroy() {
        // Clean up active tweens
        foreach (var t in panelTweens.Values) {
            t?.Kill();
        }
        if (containerCanvasGroup != null) {
            containerCanvasGroup.DOKill();
        }
    }
}
