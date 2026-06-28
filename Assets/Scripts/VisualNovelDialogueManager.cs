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

    [Tooltip("Optional translation of the dialogue to be printed below/alongside the primary text.")]
    public string translationText;

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
/// Defines the type of speaker for JSON-loaded dialogues.
/// </summary>
public enum SpeakerType {
    Person,
    Environment
}

/// <summary>
/// Defines the screen position of the speaker for JSON-loaded dialogues.
/// </summary>
public enum DialoguePosition {
    Left,
    Right
}

/// <summary>
/// Represents a raw dialogue entry loaded from a JSON file.
/// </summary>
[System.Serializable]
public class JsonDialogueEntry {
    public SpeakerType speakerType;
    public string speakerName;
    public DialoguePosition position;
    public string primaryText;
    public string translationText;
    public string assetName;
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

    [Tooltip("UI Text component for rendering the translation dialogue text. Optional.")]
    [SerializeField] private Text translationText;

    [Header("Image Panels Configuration")]
    [Tooltip("Register the available image panels here. Define keys like 'Left', 'Right', 'BG' and link their UI Images.")]
    [SerializeField] private List<ImagePanelConfig> imagePanels = new List<ImagePanelConfig>();

    [Header("Text Settings")]
    [Tooltip("Base speed of typewriter effect in characters per second.")]
    [SerializeField] private float baseCharactersPerSecond = 60f;

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
    private string fullTranslationTextOfCurrentLine = "";
    private int totalVisibleTranslationChars = 0;
    private Coroutine typingCoroutine;
    private CanvasGroup containerCanvasGroup;
    private Color originalSpeakerTextColor = Color.white;


    // Fast mapping for image panels runtime control
    private Dictionary<string, ImagePanelConfig> panelMap = new Dictionary<string, ImagePanelConfig>();
    private Dictionary<string, Tween> panelTweens = new Dictionary<string, Tween>();
    private Dictionary<string, Vector2> originalPositions = new Dictionary<string, Vector2>();

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

        if (speakerNameText != null) {
            originalSpeakerTextColor = speakerNameText.color;
        }
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
        originalPositions.Clear();
        foreach (var config in imagePanels) {
            if (string.IsNullOrEmpty(config.panelKey)) continue;

            if (panelMap.ContainsKey(config.panelKey)) {
                Debug.LogWarning($"VisualNovelDialogueManager: Duplicate panel key '{config.panelKey}' detected!");
                continue;
            }

            panelMap.Add(config.panelKey, config);

            // Store original anchored position
            if (config.imageComponent != null) {
                originalPositions[config.panelKey] = config.imageComponent.rectTransform.anchoredPosition;
            }

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
            // Check if the assigned dialogueContainer is actually the Canvas itself.
            // If so, redirect it to the inner dialogue panel (e.g. named "Panel") to avoid deactivating the entire Canvas.
            Canvas canvas = dialogueContainer.GetComponent<Canvas>();
            if (canvas != null) {
                Transform actualDialoguePanel = null;
                if (dialogueText != null) {
                    Transform current = dialogueText.transform;
                    while (current != null && current.parent != canvas.transform && current.parent != null) {
                        current = current.parent;
                    }
                    if (current != null && current.parent == canvas.transform) {
                        actualDialoguePanel = current;
                    }
                }

                if (actualDialoguePanel == null) {
                    actualDialoguePanel = canvas.transform.Find("Panel");
                }

                if (actualDialoguePanel != null) {
                    Debug.Log($"VisualNovelDialogueManager: dialogueContainer was assigned to Canvas. Redirecting to child panel: '{actualDialoguePanel.name}' to keep Canvas active.");
                    dialogueContainer = actualDialoguePanel.gameObject;
                }
            }

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
            if (translationText != null) FontManager.Instance.ApplyFontToText(translationText);
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

        if (dialogueText != null) {
            dialogueText.text = fullTextOfCurrentLine;
        }
        if (translationText != null && !string.IsNullOrEmpty(fullTranslationTextOfCurrentLine)) {
            translationText.text = fullTranslationTextOfCurrentLine;
        }
        isTypingText = false;

        OnLineComplete?.Invoke(currentLines[currentLineIndex]);
    }

    /// <summary>
    /// Plays the dialogue line at the current index.
    /// </summary>
    private void PlayCurrentLine() {
        DialogueLine line = currentLines[currentLineIndex];
        OnLineStart?.Invoke(line);

        // Update speaker name UI with transition effects
        if (speakerNameText != null) {
            bool nameChanged = speakerNameText.text != line.speakerName;
            speakerNameText.text = line.speakerName;
            
            if (nameChanged && !string.IsNullOrEmpty(line.speakerName)) {
                // Kill active tweens
                speakerNameText.transform.DOKill(true);
                speakerNameText.DOKill(true);
                
                // Reset scale
                speakerNameText.transform.localScale = Vector3.one;
                
                // Set alpha to 0 and fade in to original color
                Color startCol = originalSpeakerTextColor;
                startCol.a = 0f;
                speakerNameText.color = startCol;
                
                speakerNameText.DOColor(originalSpeakerTextColor, 0.25f).SetUpdate(true);
                speakerNameText.transform.DOPunchScale(new Vector3(0.12f, 0.12f, 0f), 0.3f, 6, 0.5f).SetUpdate(true);
            }
        }

        if (speakerNameContainer != null) {
            bool wasActive = speakerNameContainer.activeSelf;
            bool shouldBeActive = !string.IsNullOrEmpty(line.speakerName);
            speakerNameContainer.SetActive(shouldBeActive);
            
            if (shouldBeActive && !wasActive) {
                // Entry pop for the speaker container itself
                speakerNameContainer.transform.DOKill(true);
                speakerNameContainer.transform.localScale = Vector3.zero;
                speakerNameContainer.transform.DOScale(Vector3.one, 0.25f).SetEase(Ease.OutBack).SetUpdate(true);
            }
        }

        // Trigger dynamic image panel operations
        ExecuteImageOperations(line.imageOperations);

        // Run typewriter effect
        fullTextOfCurrentLine = line.dialogueText;
        totalVisibleChars = GetVisibleCharacterCount(fullTextOfCurrentLine);

        // Set translation text values
        if (translationText != null) {
            if (!string.IsNullOrEmpty(line.translationText)) {
                translationText.gameObject.SetActive(true);
                fullTranslationTextOfCurrentLine = line.translationText;
                totalVisibleTranslationChars = GetVisibleCharacterCount(fullTranslationTextOfCurrentLine);
            } else {
                translationText.text = "";
                translationText.gameObject.SetActive(false);
                fullTranslationTextOfCurrentLine = "";
                totalVisibleTranslationChars = 0;
            }
        } else {
            fullTranslationTextOfCurrentLine = "";
            totalVisibleTranslationChars = 0;
        }

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
        if (translationText != null) {
            translationText.text = "";
        }

        float speedMultiplier = Mathf.Max(0.01f, line.typingSpeedMultiplier);
        float delayBetweenChars = 1f / (baseCharactersPerSecond * speedMultiplier);

        int visibleCharsShown = 0;
        int visibleTranslationCharsShown = 0;

        int charCounter = 0;
        bool playedSound = false;
        while (visibleCharsShown <= totalVisibleChars || visibleTranslationCharsShown <= totalVisibleTranslationChars) {
            playedSound = false;
            if (dialogueText != null && visibleCharsShown <= totalVisibleChars) {
                dialogueText.text = GetParsedText(fullTextOfCurrentLine, visibleCharsShown);
                visibleCharsShown++;
                playedSound = true;
            }
            if (translationText != null && !string.IsNullOrEmpty(fullTranslationTextOfCurrentLine) && visibleTranslationCharsShown <= totalVisibleTranslationChars) {
                translationText.text = GetParsedText(fullTranslationTextOfCurrentLine, visibleTranslationCharsShown);
                visibleTranslationCharsShown++;
                playedSound = true;
            }
            if (playedSound) {
                charCounter++;
                if (charCounter % 3 == 0 && SoundManager.Instance != null) {
                    SoundManager.Instance.PlaySFXDialogue();
                }
            }
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
            if (panel.imageComponent != null) {
                panel.imageComponent.rectTransform.DOKill(false);
                panel.imageComponent.transform.DOKill(false);
            }

            switch (op.action) {
                case ImagePanelAction.ShowOrUpdate:
                {
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

                    if (originalPositions.TryGetValue(op.panelKey, out Vector2 origPos)) {
                        if (!wasActive || currentAlpha < 0.01f) {
                            // Fade & slide in from bottom (Ease.OutBack creates a beautiful bounce/spring entry)
                            panel.imageComponent.gameObject.SetActive(true);
                            SetPanelAlpha(panel, 0f);
                            panel.imageComponent.rectTransform.anchoredPosition = origPos + new Vector2(0f, -40f);

                            if (op.fadeDuration > 0f) {
                                panel.imageComponent.rectTransform.DOAnchorPos(origPos, op.fadeDuration).SetEase(Ease.OutBack).SetUpdate(true);
                                Tween tween = FadePanel(panel, 1f, op.fadeDuration);
                                panelTweens[op.panelKey] = tween;
                            } else {
                                panel.imageComponent.rectTransform.anchoredPosition = origPos;
                                SetPanelAlpha(panel, 1f);
                            }
                        } else {
                            // Already active. Do a quick vertical hop (bounce) and scale pop to signify talking
                            if (op.fadeDuration > 0f) {
                                panel.imageComponent.rectTransform.DOPunchPosition(new Vector2(0f, 15f), op.fadeDuration, 2, 0.5f).SetUpdate(true);
                                panel.imageComponent.transform.DOPunchScale(new Vector3(0.06f, 0.06f, 0f), op.fadeDuration, 5, 0.5f).SetUpdate(true);
                                Tween tween = FadePanel(panel, 1f, op.fadeDuration);
                                panelTweens[op.panelKey] = tween;
                            }
                        }
                    } else {
                        // Fallback if original positions are not mapped
                        if (!wasActive || currentAlpha < 0.01f) {
                            panel.imageComponent.gameObject.SetActive(true);
                            SetPanelAlpha(panel, 0f);

                            if (op.fadeDuration > 0f) {
                                Tween tween = FadePanel(panel, 1f, op.fadeDuration);
                                panelTweens[op.panelKey] = tween;
                            } else {
                                SetPanelAlpha(panel, 1f);
                            }
                        } else {
                            if (op.fadeDuration > 0f) {
                                panel.imageComponent.transform.DOPunchScale(new Vector3(0.05f, 0.05f, 0.05f), op.fadeDuration, 5, 0.5f).SetUpdate(true);
                                Tween tween = FadePanel(panel, 1f, op.fadeDuration);
                                panelTweens[op.panelKey] = tween;
                            }
                        }
                    }
                    break;
                }

                case ImagePanelAction.Hide:
                {
                    if (panel.imageComponent.gameObject.activeSelf) {
                        if (originalPositions.TryGetValue(op.panelKey, out Vector2 origPos)) {
                            if (op.fadeDuration > 0f) {
                                // Slide out down & fade out
                                panel.imageComponent.rectTransform.DOAnchorPos(origPos + new Vector2(0f, -40f), op.fadeDuration).SetEase(Ease.InQuad).SetUpdate(true);
                                Tween tween = FadePanel(panel, 0f, op.fadeDuration)
                                    .OnComplete(() => {
                                        panel.imageComponent.gameObject.SetActive(false);
                                        panel.imageComponent.rectTransform.anchoredPosition = origPos;
                                    });
                                panelTweens[op.panelKey] = tween;
                            } else {
                                SetPanelAlpha(panel, 0f);
                                panel.imageComponent.gameObject.SetActive(false);
                                panel.imageComponent.rectTransform.anchoredPosition = origPos;
                            }
                        } else {
                            if (op.fadeDuration > 0f) {
                                Tween tween = FadePanel(panel, 0f, op.fadeDuration)
                                    .OnComplete(() => panel.imageComponent.gameObject.SetActive(false));
                                panelTweens[op.panelKey] = tween;
                            } else {
                                SetPanelAlpha(panel, 0f);
                                panel.imageComponent.gameObject.SetActive(false);
                            }
                        }
                    }
                    break;
                }

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
                .OnComplete(() => {
                    dialogueContainer.SetActive(false);
                    if (dialogueText != null) dialogueText.text = "";
                    if (translationText != null) translationText.text = "";
                });
        }

        // Fade and slide all active image panels out
        foreach (var entry in panelMap) {
            var panel = entry.Value;
            if (panel.imageComponent.gameObject.activeSelf) {
                if (panelTweens.TryGetValue(entry.Key, out Tween t)) {
                    t?.Kill();
                }

                if (panel.imageComponent != null) {
                    panel.imageComponent.rectTransform.DOKill(false);
                    panel.imageComponent.transform.DOKill(false);
                }

                if (originalPositions.TryGetValue(entry.Key, out Vector2 origPos)) {
                    panel.imageComponent.rectTransform.DOAnchorPos(origPos + new Vector2(0f, -40f), containerFadeDuration).SetEase(Ease.InQuad).SetUpdate(true);
                    Tween tween = FadePanel(panel, 0f, containerFadeDuration)
                        .OnComplete(() => {
                            panel.imageComponent.gameObject.SetActive(false);
                            panel.imageComponent.rectTransform.anchoredPosition = origPos;
                        });
                    panelTweens[entry.Key] = tween;
                } else {
                    Tween tween = FadePanel(panel, 0f, containerFadeDuration)
                        .OnComplete(() => panel.imageComponent.gameObject.SetActive(false));
                    panelTweens[entry.Key] = tween;
                }
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

    #region JSON Dialogue Loading & Parsing

    /// <summary>
    /// Loads and begins a dialogue sequence from a JSON text file stored in Resources.
    /// </summary>
    /// <param name="resourcePath">The path of the JSON file relative to Resources (without extension).</param>
    public void StartDialogueFromJson(string resourcePath) {
        TextAsset jsonAsset = Resources.Load<TextAsset>(resourcePath);
        if (jsonAsset == null) {
            Debug.LogError($"VisualNovelDialogueManager: Cannot find JSON dialogue file at Resources/{resourcePath}");
            return;
        }

        StartDialogueFromJsonContent(jsonAsset.text);
    }

    /// <summary>
    /// Begins a dialogue sequence from a raw JSON string content.
    /// </summary>
    public void StartDialogueFromJsonContent(string jsonContent) {
        List<JsonDialogueEntry> entries = ParseDialogueJson(jsonContent);
        if (entries == null || entries.Count == 0) {
            Debug.LogWarning("VisualNovelDialogueManager: Parsed JSON dialogue is empty or invalid.");
            return;
        }

        List<DialogueLine> dialogueLines = ConvertJsonEntriesToDialogueLines(entries);
        StartDialogue(dialogueLines);
    }

    /// <summary>
    /// Converts deserialized JSON dialogue entries into DialogueLine structures.
    /// </summary>
    private List<DialogueLine> ConvertJsonEntriesToDialogueLines(List<JsonDialogueEntry> entries) {
        List<DialogueLine> lines = new List<DialogueLine>();
        foreach (var entry in entries) {
            DialogueLine line = new DialogueLine();
            line.speakerName = entry.speakerName;
            line.dialogueText = entry.primaryText;
            line.translationText = entry.translationText;
            line.typingSpeedMultiplier = 1f;
            line.imageOperations = new List<DialogueImageOperation>();

            // Find if there is a panel key matching the asset name or position
            string activePanelKey = null;
            Sprite sprite = null;

            if (!string.IsNullOrEmpty(entry.assetName)) {
                sprite = LoadSpriteFromResources(entry.assetName);
                if (sprite != null) {
                    // Check if entry.assetName is registered in imagePanels
                    bool hasAssetPanel = false;
                    foreach (var panel in imagePanels) {
                        if (panel.panelKey == entry.assetName) {
                            hasAssetPanel = true;
                            break;
                        }
                    }

                    if (hasAssetPanel) {
                        activePanelKey = entry.assetName;
                    } else {
                        // Fallback to position
                        activePanelKey = entry.position.ToString();
                    }
                }
            }

            // Create operations for all panels to ensure correct show/hide state
            foreach (var panel in imagePanels) {
                if (string.IsNullOrEmpty(panel.panelKey)) continue;

                // Skip background/BG panels from auto show/hide character logic
                string keyLower = panel.panelKey.ToLower();
                if (keyLower == "bg" || keyLower == "background") continue;

                DialogueImageOperation op = new DialogueImageOperation();
                op.panelKey = panel.panelKey;
                op.preserveAspect = true;
                op.fadeDuration = 0.3f;
                op.customScale = Vector3.one;

                if (panel.panelKey == activePanelKey) {
                    op.action = ImagePanelAction.ShowOrUpdate;
                    op.spriteToDisplay = sprite;
                } else {
                    op.action = ImagePanelAction.Hide;
                }
                line.imageOperations.Add(op);
            }

            lines.Add(line);
        }
        return lines;
    }

    /// <summary>
    /// Helper to strip the extension from asset name and load from Resources.
    /// </summary>
    private Sprite LoadSpriteFromResources(string assetName) {
        if (string.IsNullOrEmpty(assetName)) return null;
        string path = assetName;
        int dotIndex = path.LastIndexOf('.');
        if (dotIndex > 0) {
            path = path.Substring(0, dotIndex);
        }
        Sprite sprite = Resources.Load<Sprite>(path);
        if (sprite == null) {
            Debug.LogWarning($"VisualNovelDialogueManager: Failed to load Sprite '{path}' from Resources.");
        }
        return sprite;
    }

    /// <summary>
    /// A robust custom JSON parser designed to handle arrays of objects with possible missing/trailing commas and comments.
    /// </summary>
    public static List<JsonDialogueEntry> ParseDialogueJson(string jsonText) {
        List<JsonDialogueEntry> entries = new List<JsonDialogueEntry>();
        if (string.IsNullOrEmpty(jsonText)) return entries;

        // 1. Remove comments
        string cleanText = System.Text.RegularExpressions.Regex.Replace(jsonText, @"//.*$", "", System.Text.RegularExpressions.RegexOptions.Multiline);
        cleanText = System.Text.RegularExpressions.Regex.Replace(cleanText, @"/\*.*?\*/", "", System.Text.RegularExpressions.RegexOptions.Singleline);

        // 2. Character-by-character parsing
        int index = 0;
        int length = cleanText.Length;

        void SkipWhitespace() {
            while (index < length && char.IsWhiteSpace(cleanText[index])) {
                index++;
            }
        }

        string ReadString() {
            if (index >= length || cleanText[index] != '"') return null;
            index++; // Skip opening quote
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            while (index < length) {
                char c = cleanText[index];
                if (c == '"') {
                    index++; // Skip closing quote
                    break;
                } else if (c == '\\' && index + 1 < length) {
                    index++;
                    char next = cleanText[index];
                    switch (next) {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        default: sb.Append('\\').Append(next); break;
                    }
                } else {
                    sb.Append(c);
                }
                index++;
            }
            return sb.ToString();
        }

        string ReadValue() {
            SkipWhitespace();
            if (index >= length) return null;
            if (cleanText[index] == '"') {
                return ReadString();
            }
            // Read unquoted word (e.g. null, true, false, or numbers)
            int start = index;
            while (index < length && !char.IsWhiteSpace(cleanText[index]) && cleanText[index] != ',' && cleanText[index] != '}' && cleanText[index] != ']') {
                index++;
            }
            string word = cleanText.Substring(start, index - start);
            if (word.ToLower() == "null") return null;
            return word;
        }

        while (index < length) {
            SkipWhitespace();
            if (index >= length) break;
            
            char c = cleanText[index];
            if (c == '{') {
                index++; // Skip '{'
                JsonDialogueEntry entry = new JsonDialogueEntry();
                while (index < length) {
                    SkipWhitespace();
                    if (index >= length) break;
                    if (cleanText[index] == '}') {
                        index++; // Skip '}'
                        break;
                    }
                    if (cleanText[index] == ',') {
                        index++; // Skip comma
                        continue;
                    }
                    
                    if (cleanText[index] == '"') {
                        string key = ReadString();
                        SkipWhitespace();
                        if (index < length && cleanText[index] == ':') {
                            index++; // Skip ':'
                        }
                        string val = ReadValue();
                        
                        switch (key) {
                            case "speakerType":
                                if (System.Enum.TryParse(val, true, out SpeakerType st)) entry.speakerType = st;
                                break;
                            case "speakerName":
                                entry.speakerName = val;
                                break;
                            case "position":
                                if (System.Enum.TryParse(val, true, out DialoguePosition pos)) entry.position = pos;
                                break;
                            case "primaryText":
                                entry.primaryText = val;
                                break;
                            case "translationText":
                                entry.translationText = val;
                                break;
                            case "assetName":
                                entry.assetName = val;
                                break;
                        }
                    } else {
                        index++; // Skip invalid character to avoid infinite loop
                    }
                }
                entries.Add(entry);
            } else {
                index++; // Skip non-object character
            }
        }

        return entries;
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
