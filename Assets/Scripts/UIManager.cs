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

    [Header("UI Customization GameObjects")]
    [Tooltip("Text GameObject flashed when taking collision damage")]
    [SerializeField] private GameObject damageTextObject;
    
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
    private Text objectivesText;
    private Text gameOverText;
    private Text victoryText;

    private Coroutine warningCoroutine;
    private Coroutine statusSuccessCoroutine;

    private Tween damageTextTween;
    private Tween damageTextScaleTween;
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

            Text label = healthSlider.GetComponentInChildren<Text>();
            if (label != null) {
                label.text = "Document Integrity: 100%";
            }
        }

        // 2. Resolve/create the text components
        SetupTextComponents();

        // 3. Reset states for new run
        if (damageTextObject != null) damageTextObject.SetActive(false);
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
            damageText = GetOrCreateUIText("PackageWarningText", new Vector2(0f, -110f), new Vector2(650f, 45f), 22, new Color(1f, 0.2f, 0.2f), TextAnchor.MiddleCenter);
            if (damageText != null) {
                damageText.fontStyle = FontStyle.Bold;
                damageTextObject = damageText.gameObject;
                damageTextObject.SetActive(false);
            }
        }

        // Resolve Objectives/Status Text
        if (objectivesTextObject != null) {
            objectivesText = objectivesTextObject.GetComponent<Text>();
        }
        if (objectivesText == null) {
            objectivesText = GetOrCreateUIText("PackageStatusText", new Vector2(0f, -60f), new Vector2(650f, 40f), 18, Color.white, TextAnchor.MiddleCenter);
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
            scoreText = GetOrCreateUIText("PackageScoreText", new Vector2(-20f, -20f), new Vector2(250f, 40f), 20, Color.yellow, TextAnchor.MiddleRight);
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
                if (gameOverTextObject != null) gameOverTextObject.SetActive(false);
                if (victoryTextObject != null) victoryTextObject.SetActive(false);
                if (objectivesTextObject != null) objectivesTextObject.SetActive(true);
                break;

            case PackageDeliverySystem.DeliveryState.Broken:
                // Show game over / ruined screen
                if (gameOverTextObject != null) {
                    gameOverTextObject.SetActive(true);
                    if (gameOverText != null) {
                        gameOverText.text = "DOCUMENTS RUINED! Press R to Respawn";
                    }
                    gameOverTextObject.transform.DOComplete();
                    gameOverTextObject.transform.localScale = Vector3.zero;
                    gameOverTextObject.transform.DOScale(1f, 0.5f).SetEase(Ease.OutElastic);

                    if (objectivesTextObject != null) objectivesTextObject.SetActive(false);
                    if (damageTextObject != null) damageTextObject.SetActive(false);
                    if (victoryTextObject != null) victoryTextObject.SetActive(false);
                } else {
                    // Fallback to warningText + statusText
                    if (damageTextObject != null) {
                        damageTextObject.SetActive(true);
                        if (damageText != null) {
                            damageText.text = "DOCUMENTS RUINED! Press R to Respawn";
                        }
                    }
                    if (objectivesTextObject != null) {
                        objectivesTextObject.SetActive(true);
                        if (objectivesText != null) {
                            objectivesText.text = "OBJECTIVE: Press R to Respawn at last safe position";
                            objectivesText.color = new Color(1f, 0.3f, 0.3f);
                        }
                    }
                }
                break;

            case PackageDeliverySystem.DeliveryState.GameOver:
                // Show victory screen
                if (victoryTextObject != null) {
                    victoryTextObject.SetActive(true);
                    if (victoryText != null) {
                        victoryText.text = "VICTORY! All stamps collected and delivered! Press R to restart.";
                    }
                    victoryTextObject.transform.DOComplete();
                    victoryTextObject.transform.localScale = Vector3.zero;
                    victoryTextObject.transform.DOScale(1f, 0.7f).SetEase(Ease.OutElastic);

                    if (objectivesTextObject != null) objectivesTextObject.SetActive(false);
                    if (damageTextObject != null) damageTextObject.SetActive(false);
                    if (gameOverTextObject != null) gameOverTextObject.SetActive(false);
                } else {
                    // Fallback to statusText
                    if (damageTextObject != null) damageTextObject.SetActive(false);
                    if (objectivesTextObject != null) {
                        objectivesTextObject.SetActive(true);
                        if (objectivesText != null) {
                            objectivesText.text = "VICTORY! All stamps collected and delivered! Press R to restart.";
                            objectivesText.color = Color.green;
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
        Image fill = healthSlider.fillRect != null ? 
            healthSlider.fillRect.GetComponent<Image>() : null;

        if (fill != null) {
            fill.DOComplete();
            Color targetColor;
            if (currentHealth > 70f) {
                targetColor = new Color(0.2f, 0.8f, 0.2f, 0.9f); // Green
            } else if (currentHealth > 30f) {
                targetColor = new Color(0.9f, 0.65f, 0.1f, 0.9f); // Yellow/Orange
            } else {
                targetColor = new Color(0.9f, 0.2f, 0.2f, 0.9f); // Red
            }
            fill.DOColor(targetColor, 0.3f);
        }

        // Dynamically update the Text label to show the correct health percentage
        Text label = healthSlider.GetComponentInChildren<Text>();
        if (label != null) {
            label.text = $"Document Integrity: {currentHealth:F0}%";
        }
    }

    public void UpdateScore(int collected, int total) {
        if (scoreText != null) {
            scoreText.text = $"STAMPS: {collected} / {total}";
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
        if (objectivesTextObject == null || objectivesText == null) return;

        if (objectiveTween != null) objectiveTween.Kill();
        if (objectiveScaleTween != null) objectiveScaleTween.Kill();

        objectivesText.text = msg;
        objectivesText.color = Color.green;

        objectivesTextObject.transform.localScale = Vector3.one;
        objectiveScaleTween = objectivesTextObject.transform.DOPunchScale(new Vector3(0.2f, 0.2f, 0.2f), 0.4f, 8, 0.5f)
            .OnComplete(() => {
                objectiveTween = DOTween.Sequence()
                    .AppendInterval(1.6f)
                    .Append(objectivesText.DOColor(nextObjectiveColor, 0.3f))
                    .OnStart(() => {
                        objectivesText.text = nextObjectiveText;
                        objectivesTextObject.transform.localScale = Vector3.one;
                        objectivesTextObject.transform.DOPunchScale(new Vector3(0.1f, 0.1f, 0.1f), 0.3f);
                    });
            });
    }

    private Text GetOrCreateUIText(string name, Vector2 anchoredPosition, Vector2 size, int fontSize, Color color, TextAnchor alignment) {
        GameObject existing = GameObject.Find(name);
        if (existing != null) {
            return existing.GetComponent<Text>();
        }
        return CreateUIText(name, anchoredPosition, size, fontSize, color, alignment);
    }

    private Text CreateUIText(string name, Vector2 anchoredPosition, Vector2 size, int fontSize, Color color, TextAnchor alignment) {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return null;

        GameObject go = new GameObject(name);
        go.layer = 5;
        go.transform.SetParent(canvas.transform, false);

        RectTransform rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);

        Text text = go.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (text.font == null) {
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;

        go.AddComponent<Shadow>();

        return text;
    }

    public void SetGenerationPanelActive(bool active) {
        if (generationPanel != null) {
            Image img = generationPanel.GetComponent<Image>();
            if (img != null) {
                img.DOComplete();
                if (active) {
                    generationPanel.SetActive(true);
                    Color c = img.color;
                    c.a = 0f;
                    img.color = c;
                    img.DOFade(1f, 0.4f).SetUpdate(true);
                } else {
                    img.DOFade(0f, 0.5f).SetUpdate(true)
                        .OnComplete(() => generationPanel.SetActive(false));
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
        if (generationPanel != null) {
            Image img = generationPanel.GetComponent<Image>();
            if (img != null) img.DOKill();
        }
    }
}
