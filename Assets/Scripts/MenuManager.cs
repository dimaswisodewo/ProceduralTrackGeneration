using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Manages the menu behavior, displays the music attribution panel first, 
/// and triggers gameplay scene loading when dialogues end.
/// </summary>
public class MenuManager : MonoBehaviour {
    [Header("References")]
    [SerializeField] private SceneLoader sceneLoader;

    [Header("Settings")]
    [SerializeField] private string targetSceneName = "GamePlayScene";

    [Header("Dialogue Configuration")]
    [SerializeField] private string dialogueResourcePath = "intro_dialogue";

    [Header("Attribution UI Settings")]
    [Tooltip("How long to show the attribution panel (in seconds) before automatically transitioning to dialogue.")]
    [SerializeField] private float attributionDuration = 3f;

    [Tooltip("Delay in seconds between the attribution panel fading out and the dialogue starting.")]
    [SerializeField] private float delayBeforeDialogue = 3f;

    [Header("Attribution UI (Optional)")]
    [Tooltip("If assigned, this panel will be shown/hidden. If null, a default panel will be created dynamically.")]
    [SerializeField] private GameObject attributionPanel;

    [Tooltip("If assigned, the attribution text will be updated here. If null, a text component will be created dynamically.")]
    [SerializeField] private Text attributionText;

    [Tooltip("The canvas to parent the dynamically created attribution panel under.")]
    [SerializeField] private Canvas targetCanvas;

    private bool isShowingAttribution = false;
    private Coroutine autoDismissCoroutine;

    private void Start() {
        // Automatically resolve the SceneLoader instance if not assigned
        if (sceneLoader == null) {
            sceneLoader = SceneLoader.Instance;
        }

        // Start by displaying the attribution text first
        ShowAttribution();
    }

    private void Update() {
        if (isShowingAttribution) {
            if (GetAdvanceInputPressed()) {
                DismissAttribution();
            }
        }
    }

    /// <summary>
    /// Checks for input to advance/dismiss the attribution.
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

    private void ShowAttribution() {
        // If the user already assigned a panel in the inspector, use it
        if (attributionPanel != null) {
            attributionPanel.SetActive(true);
            if (attributionText != null) {
                attributionText.text = "Music from #Uppbeat (free for Creators!):\n\n" +
                                       "https://uppbeat.io/t/kevin-macleod/blockman\n" +
                                       "https://uppbeat.io/track/dark-cat/a-day-in-my-life\n\n\n" +
                                       "(Press Space or Click to continue)";
            }
            isShowingAttribution = true;
            autoDismissCoroutine = StartCoroutine(AutoDismissAfterDelay(attributionDuration));
            return;
        }

        // If targetCanvas is not assigned, let's create a dedicated canvas for the attribution
        // to avoid conflicts with the dialogue manager's canvas group alpha settings.
        if (targetCanvas == null) {
            GameObject canvasGo = new GameObject("AttributionCanvas");
            targetCanvas = canvasGo.AddComponent<Canvas>();
            targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            targetCanvas.sortingOrder = 999; // Ensure it renders on top of everything

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(800, 600);

            canvasGo.AddComponent<GraphicRaycaster>();
        }

        // Create Panel GameObject under the targetCanvas
        GameObject panelGo = new GameObject("AttributionPanel");
        panelGo.transform.SetParent(targetCanvas.transform, false);
        
        // Add RectTransform and stretch it full screen
        RectTransform panelRect = panelGo.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        // Add Image for background color
        Image img = panelGo.AddComponent<Image>();
        img.color = new Color(0.08f, 0.08f, 0.08f, 0.95f); // Elegant very dark grey / almost black overlay

        // Add CanvasGroup for smooth fade-out
        CanvasGroup cg = panelGo.AddComponent<CanvasGroup>();
        cg.alpha = 1f;

        // Create Text GameObject
        GameObject textGo = new GameObject("AttributionText");
        textGo.transform.SetParent(panelGo.transform, false);

        RectTransform textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.1f, 0.1f);
        textRect.anchorMax = new Vector2(0.9f, 0.9f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text txt = textGo.AddComponent<Text>();
        txt.text = "Music from #Uppbeat (free for Creators!):\n\n" +
                   "<color=#5CACE2>https://uppbeat.io/t/kevin-macleod/blockman</color>\n" +
                   "<color=#5CACE2>https://uppbeat.io/track/dark-cat/a-day-in-my-life</color>\n\n\n" +
                   "<size=18><color=#aaaaaa>(Press Space or Click to continue)</color></size>";
        
        // Set alignment and support rich text
        txt.alignment = TextAnchor.MiddleCenter;
        txt.supportRichText = true;
        txt.color = Color.white;
        txt.fontSize = 24;

        // Apply custom fonts if FontManager is available
        if (FontManager.Instance != null) {
            FontManager.Instance.ApplyFontToText(txt);
        }

        // Store reference to the dynamically created panel
        attributionPanel = panelGo;
        isShowingAttribution = true;

        // Start auto dismissal timer
        autoDismissCoroutine = StartCoroutine(AutoDismissAfterDelay(attributionDuration));
    }

    private IEnumerator AutoDismissAfterDelay(float delay) {
        yield return new WaitForSeconds(delay);
        DismissAttribution();
    }

    private void DismissAttribution() {
        if (autoDismissCoroutine != null) {
            StopCoroutine(autoDismissCoroutine);
            autoDismissCoroutine = null;
        }

        isShowingAttribution = false;
        
        if (attributionPanel != null) {
            CanvasGroup cg = attributionPanel.GetComponent<CanvasGroup>();
            if (cg != null) {
                cg.DOComplete();
                cg.DOFade(0f, 0.5f).SetUpdate(true).OnComplete(() => {
                    attributionPanel.SetActive(false);
                    // If targetCanvas was dynamically spawned, destroy it
                    if (targetCanvas != null && targetCanvas.name == "AttributionCanvas") {
                        Destroy(targetCanvas.gameObject);
                    }
                    StartCoroutine(StartDialogueAfterDelay(delayBeforeDialogue));
                });
            } else {
                attributionPanel.SetActive(false);
                if (targetCanvas != null && targetCanvas.name == "AttributionCanvas") {
                    Destroy(targetCanvas.gameObject);
                }
                StartCoroutine(StartDialogueAfterDelay(delayBeforeDialogue));
            }
        } else {
            StartCoroutine(StartDialogueAfterDelay(delayBeforeDialogue));
        }
    }

    private IEnumerator StartDialogueAfterDelay(float delay) {
        yield return new WaitForSeconds(delay);
        TriggerMainDialogue();
    }

    private void TriggerMainDialogue() {
        Debug.Log($"MenuManager: Delay complete. Triggering main dialogue: {dialogueResourcePath}");
        if (VisualNovelDialogueManager.Instance != null) {
            VisualNovelDialogueManager.Instance.StartDialogueFromJson(dialogueResourcePath);
        } else {
            Debug.LogError("MenuManager: VisualNovelDialogueManager Instance not found when triggering main dialogue.");
            LoadGameplayScene(); // Fallback if dialogue manager is missing
        }
    }

    public void OnDialogueEnd() {
        LoadGameplayScene();
    }

    private void LoadGameplayScene() {
        Debug.Log($"MenuManager: Dialogue ended. Loading target scene: {targetSceneName}");
        if (sceneLoader != null) {
            sceneLoader.LoadScene(targetSceneName);
        } else {
            // Safe fallback to direct loading if SceneLoader reference is unavailable
            UnityEngine.SceneManagement.SceneManager.LoadScene(targetSceneName);
        }
    }
}
