using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using DG.Tweening;

public class UIManager : MonoBehaviour {
    private static UIManager instance;
    public static UIManager Instance {
        get {
            if (instance == null) {
                instance = FindObjectOfType<UIManager>();
                if (instance == null) {
                    GameObject go = new GameObject("UIManager");
                    instance = go.AddComponent<UIManager>();
                }
            }
            return instance;
        }
    }

    [Tooltip("Text GameObject flashed when taking collision damage")]
    [SerializeField] private GameObject damageTextObject;
    
    [Tooltip("Text GameObject flashed when objective is completed successfully")]
    [SerializeField] private GameObject successTextObject;
    
    [Tooltip("Text GameObject showing current gameplay objectives")]
    [SerializeField] private GameObject objectivesTextObject;
    
    [Tooltip("UI GameObject shown when documents are ruined / broken")]
    [SerializeField] private GameObject gameOverTextObject;
    
    [Tooltip("UI GameObject shown when track is completed (victory)")]
    [SerializeField] private GameObject victoryTextObject;

    [Tooltip("Black panel shown during map generation")]
    [SerializeField] private GameObject generationPanel;

    [Header("UI Components")]
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Text scoreText;

    // Resolved text components
    private Text damageText;
    private Text successText;
    private Text objectivesText;
    private Text gameOverText;
    private Text victoryText;
    private Image healthBarFillImage;
    private Text healthBarText;
    private Text repositionHelpText;

    private Coroutine warningCoroutine;
    private Coroutine statusSuccessCoroutine;

    private Tween damageTextTween;
    private Tween damageTextScaleTween;
    private Tween successTextTween;
    private Tween successTextScaleTween;
    private Tween objectiveTween;
    private Tween objectiveScaleTween;

    private void Awake() {
        if (instance == null) {
            instance = this;
        } else if (instance != this) {
            Destroy(gameObject);
        }
    }

    public void InitializeUI(int totalStamps) {
        // 1. Hook the health bar Slider in the Canvas if not assigned
        if (healthSlider == null) {
            GameObject sliderObj = GameObject.Find("HealthBar");
            if (sliderObj != null) {
                healthSlider = sliderObj.GetComponent<Slider>();
            }
        }

        if (healthSlider != null) {
            healthSlider.minValue = 0f;
            healthSlider.maxValue = 1f;
            healthSlider.value = 1f;

            healthBarFillImage = healthSlider.fillRect != null ? healthSlider.fillRect.GetComponent<Image>() : null;
            healthBarText = healthSlider.GetComponentInChildren<Text>();
            if (healthBarText != null) {
                healthBarText.text = "Evidence Integrity: 100%";
                Outline outline = healthBarText.GetComponent<Outline>();
                if (outline == null) {
                    outline = healthBarText.gameObject.AddComponent<Outline>();
                }
                if (outline != null) {
                    outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
                    outline.effectDistance = new Vector2(1.5f, -1.5f);
                }
            }
        }

        // 2. Resolve/create the text components
        SetupTextComponents();

        // Apply new fonts to all Text components in the scene
        if (FontManager.Instance != null) {
            FontManager.Instance.ApplyFontsToAllUI();
        }

        // 3. Reset states for new run
        if (damageTextObject != null) damageTextObject.SetActive(false);
        if (successTextObject != null) successTextObject.SetActive(false);
        if (gameOverTextObject != null) gameOverTextObject.SetActive(false);
        if (victoryTextObject != null) victoryTextObject.SetActive(false);
        if (objectivesTextObject != null) objectivesTextObject.SetActive(true);

        UpdateScore(0, totalStamps);
    }

    private void SetupTextComponents() {
        // Resolve Damage/Warning Text
        if (damageTextObject != null) {
            damageText = damageTextObject.GetComponent<Text>();
        }
        if (damageText == null) {
            damageText = GetOrCreateUIText("PackageWarningText", new Vector2(0f, -110f), new Vector2(720f, 65f), 22, new Color(1f, 0.35f, 0.35f), TextAnchor.MiddleCenter);
            if (damageText != null) {
                damageText.fontStyle = FontStyle.Bold;
                damageTextObject = damageText.gameObject;
                damageTextObject.SetActive(false);
            }
        }

        // Resolve Success Text
        if (successTextObject != null) {
            successText = successTextObject.GetComponent<Text>();
        }
        if (successText == null) {
            successText = GetOrCreateUIText("PackageSuccessText", new Vector2(0f, -170f), new Vector2(720f, 65f), 22, new Color(0.45f, 0.85f, 0.55f), TextAnchor.MiddleCenter);
            if (successText != null) {
                successText.fontStyle = FontStyle.Bold;
                successTextObject = successText.gameObject;
                successTextObject.SetActive(false);
            }
        }

        // Resolve Objectives/Status Text
        if (objectivesTextObject != null) {
            objectivesText = objectivesTextObject.GetComponent<Text>();
        }
        if (objectivesText == null) {
            objectivesText = GetOrCreateUIText("PackageStatusText", new Vector2(0f, -50f), new Vector2(720f, 85f), 18, Color.white, TextAnchor.MiddleCenter);
            if (objectivesText != null) {
                objectivesTextObject = objectivesText.gameObject;
            }
        }

        // Resolve Score Text
        if (scoreText == null) {
            GameObject scoreObj = GameObject.Find("PackageScoreText");
            if (scoreObj != null) {
                scoreText = scoreObj.GetComponent<Text>();
            }
        }
        if (scoreText == null) {
            scoreText = GetOrCreateUIText("PackageScoreText", new Vector2(-20f, -20f), new Vector2(350f, 40f), 20, new Color(1.0f, 0.85f, 0.2f), TextAnchor.MiddleRight);
            if (scoreText != null) {
                RectTransform rect = scoreText.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(1f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
                rect.anchoredPosition = new Vector2(-25f, -25f);
            }
        }

        // Resolve Game Over Text
        if (gameOverTextObject != null) {
            gameOverText = gameOverTextObject.GetComponent<Text>();
        }

        // Resolve Victory Text
        if (victoryTextObject != null) {
            victoryText = victoryTextObject.GetComponent<Text>();
        }

        // Resolve Reposition Help Text
        if (repositionHelpText == null) {
            repositionHelpText = GetOrCreateUIText("RepositionHelpText", new Vector2(-25f, 25f), new Vector2(600f, 30f), 12, new Color(0.85f, 0.85f, 0.85f, 0.85f), TextAnchor.LowerRight);
            if (repositionHelpText != null) {
                RectTransform rect = repositionHelpText.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(1f, 0f);
                rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(1f, 0f);
                rect.anchoredPosition = new Vector2(-25f, 25f);
            }
        }
        if (repositionHelpText != null) {
            repositionHelpText.text = "T: Reposition | C: Toggle Camera | V: Lock Yaw | R: Restart";
        }
    }

    public void SetStateUI(PackageDeliverySystem.DeliveryState state) {
        // Clear active status/warning routines when state changes
        if (warningCoroutine != null) {
            StopCoroutine(warningCoroutine);
            warningCoroutine = null;
        }
        if (statusSuccessCoroutine != null) {
            StopCoroutine(statusSuccessCoroutine);
            statusSuccessCoroutine = null;
        }

        if (healthSlider != null) {
            healthSlider.gameObject.SetActive(state != PackageDeliverySystem.DeliveryState.GameOver);
        }

        switch (state) {
            case PackageDeliverySystem.DeliveryState.CollectingStamps:
            case PackageDeliverySystem.DeliveryState.HeadingToFinalDestination:
                if (damageTextObject != null) damageTextObject.SetActive(false);
                if (successTextObject != null) successTextObject.SetActive(false);
                if (gameOverTextObject != null) gameOverTextObject.SetActive(false);
                if (victoryTextObject != null) victoryTextObject.SetActive(false);
                if (objectivesTextObject != null) objectivesTextObject.SetActive(true);
                break;
 
            case PackageDeliverySystem.DeliveryState.Broken:
                // Show game over / ruined screen
                if (gameOverTextObject != null) {
                    gameOverTextObject.SetActive(true);
                    if (gameOverText != null) {
                        gameOverText.text = "EVIDENCE RUINED! You'll rot in a prison cell! Press R to Restart";
                    }
                    gameOverTextObject.transform.DOComplete();
                    gameOverTextObject.transform.localScale = Vector3.zero;
                    gameOverTextObject.transform.DOScale(1f, 0.5f).SetEase(Ease.OutElastic);
 
                    if (objectivesTextObject != null) objectivesTextObject.SetActive(false);
                    if (damageTextObject != null) damageTextObject.SetActive(false);
                    if (successTextObject != null) successTextObject.SetActive(false);
                    if (victoryTextObject != null) victoryTextObject.SetActive(false);
                } else {
                    // Fallback to warningText + statusText
                    if (damageTextObject != null) {
                        damageTextObject.SetActive(true);
                        if (damageText != null) {
                            damageText.text = "EVIDENCE RUINED! You'll rot in a prison cell! Press R to Restart the game.";
                        }
                    }
                    if (objectivesTextObject != null) {
                        objectivesTextObject.SetActive(true);
                        if (objectivesText != null) {
                            objectivesText.text = "EVIDENCE RUINED! You'll rot in a prison cell! Press R to Restart the game.";
                            objectivesText.color = new Color(1.0f, 0.4f, 0.4f); // Vibrant Pastel Red/Coral
                        }
                    }
                }
                break;
 
            case PackageDeliverySystem.DeliveryState.GameOver:
                // Show victory screen
                if (victoryTextObject != null) {
                    victoryTextObject.SetActive(true);
                    if (victoryText != null) {
                        victoryText.text = "TRIAL WON! The evidence is legally bulletproof! Press R to restart.";
                    }
                    victoryTextObject.transform.DOComplete();
                    victoryTextObject.transform.localScale = Vector3.zero;
                    victoryTextObject.transform.DOScale(1f, 0.7f).SetEase(Ease.OutElastic);
 
                    if (objectivesTextObject != null) objectivesTextObject.SetActive(false);
                    if (damageTextObject != null) damageTextObject.SetActive(false);
                    if (successTextObject != null) successTextObject.SetActive(false);
                    if (gameOverTextObject != null) gameOverTextObject.SetActive(false);
                } else {
                    // Fallback to statusText
                    if (damageTextObject != null) damageTextObject.SetActive(false);
                    if (objectivesTextObject != null) {
                        objectivesTextObject.SetActive(true);
                        if (objectivesText != null) {
                            objectivesText.text = "TRIAL WON! The evidence is legally bulletproof! Press R to restart.";
                            objectivesText.color = new Color(0.35f, 0.9f, 0.55f); // Vibrant Pastel Green
                        }
                    }
                }
                break;
        }
    }

    public void UpdateHealthSlider(float currentHealth) {
        if (healthSlider == null) return;

        // Smoothly animate the health bar value using DOTween
        healthSlider.DOComplete();
        healthSlider.DOValue(currentHealth / 100f, 0.3f).SetEase(Ease.OutQuad);

        // Resolve fill Image and smoothly animate its color based on current health
        if (healthBarFillImage != null) {
            healthBarFillImage.DOComplete();
            Color targetColor;
            if (currentHealth > 70f) {
                targetColor = new Color(0.2f, 0.8f, 0.2f, 0.9f); // Green
            } else if (currentHealth > 30f) {
                targetColor = new Color(0.9f, 0.65f, 0.1f, 0.9f); // Yellow/Orange
            } else {
                targetColor = new Color(0.9f, 0.2f, 0.2f, 0.9f); // Red
            }
            healthBarFillImage.DOColor(targetColor, 0.3f);
        }

        // Dynamically update the Text label to show the correct health percentage
        if (healthBarText != null) {
            healthBarText.text = $"Evidence Integrity: {currentHealth:F0}%";
        }
    }

    public void UpdateScore(int collected, int total) {
        if (scoreText != null) {
            scoreText.text = $"VALIDATED EVIDENCE: {collected} / {total}";
            scoreText.transform.DOComplete();
            scoreText.transform.localScale = Vector3.one;
            scoreText.transform.DOPunchScale(new Vector3(0.25f, 0.25f, 0.25f), 0.35f, 10, 0.5f);
        }
    }

    public void SetObjectiveText(string text, Color color) {
        if (statusSuccessCoroutine != null) {
            // Let the success flash finish and set the next objective text automatically
            return;
        }

        if (objectivesTextObject != null && objectivesText != null) {
            objectivesText.text = text;
            objectivesText.color = color;
        }
    }

    public void FlashDamageText(string msg) {
        if (damageTextObject == null || damageText == null) return;

        if (damageTextTween != null) damageTextTween.Kill();
        if (damageTextScaleTween != null) damageTextScaleTween.Kill();

        damageText.text = msg;
        damageTextObject.SetActive(true);

        Color c = damageText.color;
        c.a = 1f;
        damageText.color = c;
        damageTextObject.transform.localScale = Vector3.zero;

        damageTextScaleTween = damageTextObject.transform.DOScale(1.2f, 0.2f)
            .SetEase(Ease.OutBack)
            .OnComplete(() => {
                damageTextObject.transform.DOScale(1.0f, 0.15f)
                    .SetDelay(1.0f)
                    .OnComplete(() => {
                        damageTextTween = damageText.DOFade(0f, 0.3f);
                        damageTextScaleTween = damageTextObject.transform.DOScale(0.8f, 0.3f)
                            .OnComplete(() => damageTextObject.SetActive(false));
                    });
            });

        if (healthSlider != null) {
            healthSlider.transform.DOComplete();
            healthSlider.transform.DOPunchScale(new Vector3(0.12f, 0.12f, 0.12f), 0.4f, 12, 0.8f);
            healthSlider.transform.DOPunchPosition(new Vector3(8f, 0f, 0f), 0.4f, 12, 0.8f);
        }
    }

    public void FlashObjectiveSuccessText(string msg, string nextObjectiveText, Color nextObjectiveColor) {
        // 1. Immediately update the main objective text
        if (objectivesTextObject != null && objectivesText != null) {
            objectivesText.text = nextObjectiveText;
            objectivesText.color = nextObjectiveColor;
            
            // Subtle pop animation for the new objective
            objectivesTextObject.transform.DOComplete();
            objectivesTextObject.transform.localScale = Vector3.one;
            objectivesTextObject.transform.DOPunchScale(new Vector3(0.1f, 0.1f, 0.1f), 0.3f);
        }

        // 2. Flash the success message on the dedicated success text
        if (successTextObject == null || successText == null) return;

        if (successTextTween != null) successTextTween.Kill();
        if (successTextScaleTween != null) successTextScaleTween.Kill();

        successText.text = msg;
        successTextObject.SetActive(true);

        Color c = successText.color;
        c.a = 1f;
        successText.color = c;
        successTextObject.transform.localScale = Vector3.zero;

        successTextScaleTween = successTextObject.transform.DOScale(1.2f, 0.2f)
            .SetEase(Ease.OutBack)
            .OnComplete(() => {
                successTextObject.transform.DOScale(1.0f, 0.15f)
                    .SetDelay(3.5f)
                    .OnComplete(() => {
                        successTextTween = successText.DOFade(0f, 0.4f);
                        successTextScaleTween = successTextObject.transform.DOScale(0.8f, 0.4f)
                            .OnComplete(() => successTextObject.SetActive(false));
                    });
            });
    }

    public void FlashNotificationText(string msg) {
        if (successTextObject == null || successText == null) return;

        if (successTextTween != null) successTextTween.Kill();
        if (successTextScaleTween != null) successTextScaleTween.Kill();

        successText.text = msg;
        successTextObject.SetActive(true);

        Color c = new Color(0.4f, 0.75f, 1f); // Smooth light blue notification color
        successText.color = c;
        successTextObject.transform.localScale = Vector3.zero;

        successTextScaleTween = successTextObject.transform.DOScale(1.2f, 0.2f)
            .SetEase(Ease.OutBack)
            .OnComplete(() => {
                successTextObject.transform.DOScale(1.0f, 0.15f)
                    .SetDelay(1.5f)
                    .OnComplete(() => {
                        successTextTween = successText.DOFade(0f, 0.4f);
                        successTextScaleTween = successTextObject.transform.DOScale(0.8f, 0.4f)
                            .OnComplete(() => successTextObject.SetActive(false));
                    });
            });
    }

    private Text GetOrCreateUIText(string name, Vector2 anchoredPosition, Vector2 size, int fontSize, Color color, TextAnchor alignment) {
        GameObject go = GameObject.Find(name);
        Text textComponent = null;

        if (go == null) {
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return null;

            go = new GameObject(name);
            go.layer = 5;
            go.transform.SetParent(canvas.transform, false);
            textComponent = go.AddComponent<Text>();

            Outline outline = go.AddComponent<Outline>();
            if (outline != null) {
                outline.effectColor = new Color(0.02f, 0.02f, 0.02f, 0.9f);
                outline.effectDistance = new Vector2(1.8f, -1.8f);
            }

            Shadow shadow = go.AddComponent<Shadow>();
            if (shadow != null) {
                shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
                shadow.effectDistance = new Vector2(1.5f, -1.5f);
            }
        } else {
            textComponent = go.GetComponent<Text>();
        }

        if (textComponent != null) {
            RectTransform rect = go.GetComponent<RectTransform>();
            if (rect == null) {
                rect = go.AddComponent<RectTransform>();
            }
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);

            if (FontManager.Instance != null && FontManager.Instance.RegularFont != null) {
                textComponent.font = FontManager.Instance.RegularFont;
            } else if (textComponent.font == null) {
                textComponent.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            textComponent.fontSize = fontSize;
            textComponent.color = color;
            textComponent.alignment = alignment;
            textComponent.horizontalOverflow = HorizontalWrapMode.Wrap;
            textComponent.verticalOverflow = VerticalWrapMode.Overflow;
        }

        return textComponent;
    }

    public void SetGenerationPanelActive(bool active, bool instant = false) {
        if (generationPanel != null) {
            if (active) {
                generationPanel.transform.SetAsLastSibling();
            }
            Image img = generationPanel.GetComponent<Image>();
            if (img != null) {
                img.DOComplete();
                if (active) {
                    generationPanel.SetActive(true);
                    Color c = img.color;
                    if (instant) {
                        c.a = 1f;
                        img.color = c;
                    } else {
                        c.a = 0f;
                        img.color = c;
                        img.DOFade(1f, 0.4f).SetUpdate(true);
                    }
                } else {
                    if (instant) {
                        Color c = img.color;
                        c.a = 0f;
                        img.color = c;
                        generationPanel.SetActive(false);
                    } else {
                        img.DOFade(0f, 0.5f).SetUpdate(true)
                            .OnComplete(() => generationPanel.SetActive(false));
                    }
                }
            } else {
                generationPanel.SetActive(active);
            }
        }
    }

    private void OnDestroy() {
        if (damageTextTween != null) damageTextTween.Kill();
        if (damageTextScaleTween != null) damageTextScaleTween.Kill();
        if (objectiveTween != null) objectiveTween.Kill();
        if (objectiveScaleTween != null) objectiveScaleTween.Kill();
        if (gameOverTextObject != null) gameOverTextObject.transform.DOKill();
        if (victoryTextObject != null) victoryTextObject.transform.DOKill();
        if (successTextTween != null) successTextTween.Kill();
        if (successTextScaleTween != null) successTextScaleTween.Kill();
        if (generationPanel != null) {
            Image img = generationPanel.GetComponent<Image>();
            if (img != null) img.DOKill();
        }
    }
}
