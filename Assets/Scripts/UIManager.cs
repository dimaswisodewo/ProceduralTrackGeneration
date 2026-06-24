using UnityEngine;
using UnityEngine.UI;
using System.Collections;

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
                label.text = "Document Integrity";
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

        healthSlider.value = currentHealth / 100f;

        Image fill = healthSlider.fillRect != null ? 
            healthSlider.fillRect.GetComponent<Image>() : null;

        if (fill != null) {
            if (currentHealth > 70f) {
                fill.color = new Color(0.2f, 0.8f, 0.2f, 0.9f); // Green
            } else if (currentHealth > 30f) {
                fill.color = new Color(0.9f, 0.65f, 0.1f, 0.9f); // Yellow/Orange
            } else {
                fill.color = new Color(0.9f, 0.2f, 0.2f, 0.9f); // Red
            }
        }
    }

    public void UpdateScore(int collected, int total) {
        if (scoreText != null) {
            scoreText.text = $"STAMPS: {collected} / {total}";
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
        if (warningCoroutine != null) {
            StopCoroutine(warningCoroutine);
        }
        warningCoroutine = StartCoroutine(FlashWarningTextCoroutine(msg));
    }

    private IEnumerator FlashWarningTextCoroutine(string msg) {
        if (damageTextObject != null && damageText != null) {
            damageText.text = msg;
            damageTextObject.SetActive(true);
            yield return new WaitForSeconds(1.5f);
            if (damageText.text == msg) {
                damageTextObject.SetActive(false);
            }
        }
    }

    public void FlashObjectiveSuccessText(string msg, string nextObjectiveText, Color nextObjectiveColor) {
        if (statusSuccessCoroutine != null) {
            StopCoroutine(statusSuccessCoroutine);
        }
        statusSuccessCoroutine = StartCoroutine(FlashStatusSuccessCoroutine(msg, nextObjectiveText, nextObjectiveColor));
    }

    private IEnumerator FlashStatusSuccessCoroutine(string msg, string nextObjectiveText, Color nextObjectiveColor) {
        if (objectivesTextObject != null && objectivesText != null) {
            objectivesText.text = msg;
            objectivesText.color = Color.green;
            yield return new WaitForSeconds(2.0f);
            if (objectivesText.text == msg) {
                objectivesText.text = nextObjectiveText;
                objectivesText.color = nextObjectiveColor;
            }
        }
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
}
