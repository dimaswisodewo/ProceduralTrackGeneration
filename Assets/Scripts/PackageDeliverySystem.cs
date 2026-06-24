using UnityEngine;
using System.Collections.Generic;

public class PackageDeliverySystem : MonoBehaviour {
    public static PackageDeliverySystem Instance { get; private set; }

    public enum DeliveryState {
        CollectingStamps,
        HeadingToFinalDestination,
        Broken, // Document is ruined, player must reset/respawn
        GameOver
    }

    [Header("Gameplay State")]
    public DeliveryState currentState = DeliveryState.CollectingStamps;
    public float packageHealth = 100f; // Referred to as Document Integrity
    private int score = 0;

    [Header("Collision Damage")]
    public float minCollisionSpeed = 3.0f; // Shocks below this relative velocity do not cause damage
    public float collisionDamageMultiplier = 25.0f; // Damage multiplier for high impacts

    // References
    private Rigidbody carRigidbody;
    private Transform carTransform;
    private DeliverySpot activeSpot;
    public DeliverySpot ActiveSpot => activeSpot;
    public bool show3DPointerArrow = false;
    private List<DeliverySpot> stampSpots = new List<DeliverySpot>();
    private HashSet<DeliverySpot> collectedStampSpots = new HashSet<DeliverySpot>();
    private DeliverySpot finalDestinationSpot;
    private List<DeliverySpot> allSpots = new List<DeliverySpot>();

    // UI references
    private UnityEngine.UI.Slider healthSlider;
    private UnityEngine.UI.Text statusText;
    private UnityEngine.UI.Text warningText;
    private UnityEngine.UI.Text scoreText;

    // Navigation Arrow
    private GameObject pointerArrow;
    private Material pointerArrowMaterial;

    private float spawnTime;

    private void Awake() {
        Instance = this;
        carTransform = transform;
        carRigidbody = GetComponent<Rigidbody>();
        spawnTime = Time.time;
    }

    public void InitializeSystem(List<DeliverySpot> spots) {
        allSpots = spots;

        if (spots == null || spots.Count == 0) {
            Debug.LogError("[PackageDeliverySystem] InitializeSystem called with empty spots list!");
            return;
        }

        // 1. Hook the health bar Slider in the Canvas
        GameObject sliderObj = GameObject.Find("HealthBar");
        if (sliderObj != null) {
            healthSlider = sliderObj.GetComponent<UnityEngine.UI.Slider>();
            if (healthSlider != null) {
                healthSlider.minValue = 0f;
                healthSlider.maxValue = 1f;
                healthSlider.value = 1f;
            }
            UnityEngine.UI.Text label = sliderObj.GetComponentInChildren<UnityEngine.UI.Text>();
            if (label != null) {
                label.text = "Document Integrity";
            }
        }

        // 2. Programmatically create UI status, warning, and score text elements to avoid editor requirements
        statusText = GetOrCreateUIText("PackageStatusText", new Vector2(0f, -60f), new Vector2(650f, 40f), 18, Color.white, TextAnchor.MiddleCenter);
        warningText = GetOrCreateUIText("PackageWarningText", new Vector2(0f, -110f), new Vector2(650f, 45f), 22, new Color(1f, 0.2f, 0.2f), TextAnchor.MiddleCenter);
        if (warningText != null) {
            warningText.fontStyle = FontStyle.Bold;
            warningText.gameObject.SetActive(false);
        }
        
        scoreText = GetOrCreateUIText("PackageScoreText", new Vector2(-20f, -20f), new Vector2(250f, 40f), 20, Color.yellow, TextAnchor.MiddleRight);
        if (scoreText != null) {
            // Position at top-right
            RectTransform rect = scoreText.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-25f, -25f);
        }

        // 3. Setup Stamp Spots & Final Destination
        collectedStampSpots.Clear();
        stampSpots.Clear();

        if (spots.Count >= 2) {
            finalDestinationSpot = spots[spots.Count - 1];
            for (int i = 0; i < spots.Count - 1; i++) {
                stampSpots.Add(spots[i]);
            }
        } else {
            finalDestinationSpot = spots[0];
        }

        // Initially setup rings color/state
        foreach (var s in stampSpots) {
            s.SetAsTarget(true, isPickup: true); // Vibrant Blue
        }
        if (finalDestinationSpot != null) {
            finalDestinationSpot.SetAsTarget(false); // Dim grey initially
        }

        UpdateScoreText();

        // 4. Attach the Minimap & GPS navigation system
        MinimapGPSController gpsController = gameObject.GetComponent<MinimapGPSController>();
        if (gpsController == null) {
            gpsController = gameObject.AddComponent<MinimapGPSController>();
        }

        // Initialize pointer arrow if requested
        if (show3DPointerArrow) {
            CreatePointerArrow();
        }

        // 5. Enter start state
        SetState(DeliveryState.CollectingStamps);
    }

    private void SetState(DeliveryState newState) {
        currentState = newState;

        switch (currentState) {
            case DeliveryState.CollectingStamps:
                packageHealth = 100f;
                UpdateSliderColor();
                if (healthSlider != null) healthSlider.gameObject.SetActive(true);
                if (pointerArrow != null) pointerArrow.SetActive(show3DPointerArrow);
                if (warningText != null) warningText.gameObject.SetActive(false);
                UpdateObjectiveText();
                UpdatePointerDirection();
                break;

            case DeliveryState.HeadingToFinalDestination:
                if (healthSlider != null) healthSlider.gameObject.SetActive(true);
                if (pointerArrow != null) pointerArrow.SetActive(show3DPointerArrow);
                if (warningText != null) warningText.gameObject.SetActive(false);
                UpdateObjectiveText();
                UpdatePointerDirection();
                break;

            case DeliveryState.Broken:
                packageHealth = 0f;
                UpdateSliderColor();
                if (pointerArrow != null) pointerArrow.SetActive(false);
                if (warningText != null) {
                    warningText.text = "DOCUMENTS RUINED! Press R to Respawn";
                    warningText.gameObject.SetActive(true);
                }
                if (statusText != null) {
                    statusText.text = "OBJECTIVE: Press R to Respawn at last safe position";
                    statusText.color = new Color(1f, 0.3f, 0.3f);
                }
                break;

            case DeliveryState.GameOver:
                if (pointerArrow != null) pointerArrow.SetActive(false);
                if (healthSlider != null) healthSlider.gameObject.SetActive(false);
                if (warningText != null) warningText.gameObject.SetActive(false);
                if (statusText != null) {
                    statusText.text = "VICTORY! All stamps collected and delivered! Press R to restart.";
                    statusText.color = Color.green;
                }
                break;
        }
    }

    private void UpdateScoreText() {
        if (scoreText != null) {
            int collected = collectedStampSpots.Count;
            int total = stampSpots.Count;
            scoreText.text = $"STAMPS: {collected} / {total}";
        }
    }

    private void UpdateObjectiveText() {
        if (statusText == null) return;

        if (currentState == DeliveryState.CollectingStamps) {
            int collected = collectedStampSpots.Count;
            int total = stampSpots.Count;
            statusText.text = $"OBJECTIVE: Collect stamps from each location. ({collected}/{total} collected)";
            statusText.color = new Color(0.3f, 0.8f, 1f); // Sky blue
        } else if (currentState == DeliveryState.HeadingToFinalDestination) {
            statusText.text = "OBJECTIVE: Go to the FINAL DESTINATION to finish!";
            statusText.color = new Color(1f, 0.6f, 0.2f); // Orange
        }
    }

    private void UpdatePointerDirection() {
        if (carTransform == null) {
            GameObject car = GameObject.Find("Car");
            if (car != null) {
                carTransform = car.transform;
            }
        }

        if (carTransform == null) return;

        if (currentState == DeliveryState.CollectingStamps) {
            DeliverySpot closest = null;
            float minDistance = float.MaxValue;
            Vector3 playerPos = carTransform.position;

            foreach (var spot in stampSpots) {
                if (collectedStampSpots.Contains(spot)) continue;

                float dist = Vector3.Distance(playerPos, spot.transform.position);
                if (dist < minDistance) {
                    minDistance = dist;
                    closest = spot;
                }
            }

            activeSpot = closest;
        } else if (currentState == DeliveryState.HeadingToFinalDestination) {
            activeSpot = finalDestinationSpot;
        } else {
            activeSpot = null;
        }
    }

    public void OnCarEnteredSpot(DeliverySpot spot) {
        if (currentState == DeliveryState.CollectingStamps) {
            if (stampSpots.Contains(spot) && !collectedStampSpots.Contains(spot)) {
                collectedStampSpots.Add(spot);
                spot.SetAsTarget(false); // Disable ring (turn grey)

                // Restore integrity to 100% on collection is disabled per request

                UpdateScoreText();

                int collected = collectedStampSpots.Count;
                int total = stampSpots.Count;

                if (collected == total) {
                    SetState(DeliveryState.HeadingToFinalDestination);
                    if (finalDestinationSpot != null) {
                        finalDestinationSpot.SetAsTarget(true, isPickup: false); // Highlight final destination orange
                    }
                    StartCoroutine(FlashStatusSuccess("ALL STAMPS COLLECTED! Head to final destination!"));
                } else {
                    UpdatePointerDirection();
                    StartCoroutine(FlashStatusSuccess($"STAMP COLLECTED! ({collected}/{total})"));
                }
            }
        } else if (currentState == DeliveryState.HeadingToFinalDestination) {
            if (spot == finalDestinationSpot) {
                SetState(DeliveryState.GameOver);
            }
        }
    }

    public void OnCarRespawn() {
        spawnTime = Time.time;
        if (currentState == DeliveryState.Broken) {
            SetState(DeliveryState.CollectingStamps);
            if (collectedStampSpots.Count == stampSpots.Count && stampSpots.Count > 0) {
                SetState(DeliveryState.HeadingToFinalDestination);
                if (finalDestinationSpot != null) {
                    finalDestinationSpot.SetAsTarget(true, isPickup: false);
                }
            }
            StartCoroutine(FlashStatusSuccess("Documents Restored! Drive carefully."));
        } else if (currentState == DeliveryState.CollectingStamps || currentState == DeliveryState.HeadingToFinalDestination) {
            packageHealth = 100f;
            UpdateSliderColor();
        }
    }

    private void TakeDamage(float amount) {
        if (currentState != DeliveryState.CollectingStamps && currentState != DeliveryState.HeadingToFinalDestination) return;

        packageHealth = Mathf.Max(0f, packageHealth - amount);
        UpdateSliderColor();

        if (packageHealth <= 0.01f) {
            SetState(DeliveryState.Broken);
        }
    }



    private void Update() {
        if (currentState == DeliveryState.CollectingStamps) {
            UpdatePointerDirection();
        }

        // Rotate navigation arrow to face target
        if (pointerArrow != null && pointerArrow.activeSelf && activeSpot != null) {
            pointerArrow.transform.position = carTransform.position + Vector3.up * 2.2f;

            Vector3 direction = activeSpot.transform.position - pointerArrow.transform.position;
            direction.y = 0f; // horizontal alignment

            if (direction.sqrMagnitude > 0.01f) {
                pointerArrow.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }
        }

        // Handle GameOver restart / reload scene
        if (currentState == DeliveryState.GameOver) {
            bool resetPressed = false;
            if (CarInputManager.Instance != null) {
                resetPressed = CarInputManager.Instance.ResetPressed;
            } else {
#if ENABLE_INPUT_SYSTEM
                if (UnityEngine.InputSystem.Keyboard.current != null) {
                    resetPressed = UnityEngine.InputSystem.Keyboard.current.rKey.wasPressedThisFrame;
                }
#else
                resetPressed = Input.GetKeyDown(KeyCode.R);
#endif
            }

            if (resetPressed) {
                Time.timeScale = 1f;
                UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            }
        }
    }

    private void OnCollisionEnter(Collision collision) {
        if (currentState != DeliveryState.CollectingStamps && currentState != DeliveryState.HeadingToFinalDestination) return;

        // Collision damage only applies to Building and Spot tagged objects
        bool isBuilding = collision.gameObject.CompareTag("Building") || 
                          (collision.transform.parent != null && collision.transform.parent.CompareTag("Building"));
        bool isSpot = collision.gameObject.CompareTag("Spot") || 
                      (collision.transform.parent != null && collision.transform.parent.CompareTag("Spot")) ||
                      collision.gameObject.GetComponentInParent<DeliverySpot>() != null;

        if (!isBuilding && !isSpot) return;

        float impactSpeed = collision.relativeVelocity.magnitude;
        if (impactSpeed > minCollisionSpeed) {
            float excess = impactSpeed - minCollisionSpeed;
            float damage = excess * collisionDamageMultiplier;
            TakeDamage(damage);

            StartCoroutine(FlashWarningText($"IMPACT! -{damage:F0}%"));
        }
    }

    private System.Collections.IEnumerator FlashWarningText(string msg) {
        if (warningText == null) yield break;
        warningText.text = msg;
        warningText.gameObject.SetActive(true);
        yield return new WaitForSeconds(1.5f);
        if (warningText.text == msg) {
            warningText.gameObject.SetActive(false);
        }
    }

    private System.Collections.IEnumerator FlashStatusSuccess(string msg) {
        if (statusText != null) {
            statusText.text = msg;
            statusText.color = Color.green;
        }
        yield return new WaitForSeconds(2.0f);
        if (statusText != null && statusText.text == msg) {
            UpdateObjectiveText();
        }
    }

    private void UpdateSliderColor() {
        if (healthSlider == null) return;

        healthSlider.value = packageHealth / 100f;

        UnityEngine.UI.Image fill = healthSlider.fillRect != null ? 
            healthSlider.fillRect.GetComponent<UnityEngine.UI.Image>() : null;

        if (fill != null) {
            if (packageHealth > 70f) {
                fill.color = new Color(0.2f, 0.8f, 0.2f, 0.9f); // Green
            } else if (packageHealth > 30f) {
                fill.color = new Color(0.9f, 0.65f, 0.1f, 0.9f); // Yellow/Orange
            } else {
                fill.color = new Color(0.9f, 0.2f, 0.2f, 0.9f); // Red
            }
        }
    }

    private void CreatePointerArrow() {
        if (pointerArrow != null) return;

        pointerArrow = new GameObject("DeliveryPointerArrow");

        // Shaft
        GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(shaft.GetComponent<Collider>());
        shaft.transform.parent = pointerArrow.transform;
        shaft.transform.localPosition = new Vector3(0f, 0f, -0.2f);
        shaft.transform.localScale = new Vector3(0.12f, 0.12f, 0.6f);

        // Left wing of pointer
        GameObject left = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(left.GetComponent<Collider>());
        left.transform.parent = pointerArrow.transform;
        left.transform.localPosition = new Vector3(-0.15f, 0f, 0.1f);
        left.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
        left.transform.localScale = new Vector3(0.1f, 0.1f, 0.3f);

        // Right wing of pointer
        GameObject right = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(right.GetComponent<Collider>());
        right.transform.parent = pointerArrow.transform;
        right.transform.localPosition = new Vector3(0.15f, 0f, 0.1f);
        right.transform.localRotation = Quaternion.Euler(0f, -45f, 0f);
        right.transform.localScale = new Vector3(0.1f, 0.1f, 0.3f);

        // Set materials
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        pointerArrowMaterial = new Material(shader);
        pointerArrowMaterial.color = new Color(1f, 0.85f, 0f, 1f); // Gold

        foreach (var r in pointerArrow.GetComponentsInChildren<Renderer>()) {
            r.sharedMaterial = pointerArrowMaterial;
        }

        pointerArrow.SetActive(false);
    }

    private UnityEngine.UI.Text GetOrCreateUIText(string name, Vector2 anchoredPosition, Vector2 size, int fontSize, Color color, TextAnchor alignment) {
        GameObject existing = GameObject.Find(name);
        if (existing != null) {
            return existing.GetComponent<UnityEngine.UI.Text>();
        }
        return CreateUIText(name, anchoredPosition, size, fontSize, color, alignment);
    }

    private UnityEngine.UI.Text CreateUIText(string name, Vector2 anchoredPosition, Vector2 size, int fontSize, Color color, TextAnchor alignment) {
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

        UnityEngine.UI.Text text = go.AddComponent<UnityEngine.UI.Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (text.font == null) {
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;

        UnityEngine.UI.Shadow shadow = go.AddComponent<UnityEngine.UI.Shadow>();

        return text;
    }

    private void OnDestroy() {
        if (pointerArrowMaterial != null) {
            Destroy(pointerArrowMaterial);
        }
    }
}
