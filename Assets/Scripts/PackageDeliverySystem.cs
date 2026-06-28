using UnityEngine;
using System.Collections;
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
    public float collisionDamageMultiplier = 4.0f; // Damage multiplier for high impacts

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

    // UI references managed by UIManager

    // Navigation Arrow
    private GameObject pointerArrow;
    private Material pointerArrowMaterial;

    private float spawnTime;
    private bool isRestarting = false;

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

        // Initialize UI Manager
        if (UIManager.Instance != null) {
            UIManager.Instance.InitializeUI(stampSpots.Count);
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

        if (SoundManager.Instance != null) {
            SoundManager.Instance.PlayBGMGameplay();
        }
    }

    private void SetState(DeliveryState newState) {
        currentState = newState;

        if (UIManager.Instance != null) {
            UIManager.Instance.SetStateUI(currentState);
        }

        switch (currentState) {
            case DeliveryState.CollectingStamps:
                packageHealth = 100f;
                if (UIManager.Instance != null) {
                    UIManager.Instance.UpdateHealthSlider(packageHealth);
                }
                if (pointerArrow != null) pointerArrow.SetActive(show3DPointerArrow);
                UpdateObjectiveText();
                UpdatePointerDirection();
                break;

            case DeliveryState.HeadingToFinalDestination:
                if (pointerArrow != null) pointerArrow.SetActive(show3DPointerArrow);
                UpdateObjectiveText();
                UpdatePointerDirection();
                break;

            case DeliveryState.Broken:
                packageHealth = 0f;
                if (UIManager.Instance != null) {
                    UIManager.Instance.UpdateHealthSlider(packageHealth);
                }
                if (pointerArrow != null) pointerArrow.SetActive(false);
                // Trigger a dramatic screen shake & impact shock zoom
                if (CameraFollow.Instance != null) {
                    CameraFollow.Instance.TriggerShake(0.6f, 0.45f);
                    CameraFollow.Instance.TriggerShockZoom(0.5f, -20f);
                }
                if (SoundManager.Instance != null) {
                    SoundManager.Instance.PlaySFXHealthZero();
                    SoundManager.Instance.StopBGM();
                }
                break;

            case DeliveryState.GameOver:
                if (pointerArrow != null) pointerArrow.SetActive(false);
                if (SoundManager.Instance != null) {
                    SoundManager.Instance.PlayBGMWinning();
                }
                break;
        }
    }

    private void UpdateScoreText() {
        if (UIManager.Instance != null) {
            int collected = collectedStampSpots.Count;
            int total = stampSpots.Count;
            UIManager.Instance.UpdateScore(collected, total);
        }
    }

    private void UpdateObjectiveText() {
        if (UIManager.Instance == null) return;

        if (currentState == DeliveryState.CollectingStamps) {
            int collected = collectedStampSpots.Count;
            int total = stampSpots.Count;
            string text = $"OBJECTIVE: Collect stamps from The Authorities to validate evidence. ({collected}/{total} collected)";
            Color color = new Color(0.35f, 0.78f, 1.0f); // Vibrant Pastel Blue
            UIManager.Instance.SetObjectiveText(text, color);
        } else if (currentState == DeliveryState.HeadingToFinalDestination) {
            string text = "OBJECTIVE: Bring the bulletproof evidence to the Supreme Court!";
            Color color = new Color(1.0f, 0.68f, 0.35f); // Vibrant Pastel Gold/Orange
            UIManager.Instance.SetObjectiveText(text, color);
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

                if (SoundManager.Instance != null) {
                    SoundManager.Instance.PlaySFXObjectiveSuccess();
                }

                // Restore integrity to 100% on collection is disabled per request

                UpdateScoreText();

                int collected = collectedStampSpots.Count;
                int total = stampSpots.Count;

                if (collected == total) {
                    SetState(DeliveryState.HeadingToFinalDestination);
                    if (finalDestinationSpot != null) {
                        finalDestinationSpot.SetAsTarget(true, isPickup: false); // Highlight final destination orange
                    }
                    if (UIManager.Instance != null) {
                        string nextText = "OBJECTIVE: Bring the bulletproof evidence to the Supreme Court!";
                        Color nextColor = new Color(1.0f, 0.68f, 0.35f); // Vibrant Pastel Gold/Orange
                        UIManager.Instance.FlashObjectiveSuccessText("EVIDENCE IS BULLETPROOF! Head to the Supreme Court!", nextText, nextColor);
                    }
                } else {
                    UpdatePointerDirection();
                    if (UIManager.Instance != null) {
                        string nextText = $"OBJECTIVE: Collect stamps from The Authorities to validate evidence. ({collected}/{total} collected)";
                        Color nextColor = new Color(0.35f, 0.78f, 1.0f); // Vibrant Pastel Blue
                        UIManager.Instance.FlashObjectiveSuccessText($"EVIDENCE STAMPED! ({collected}/{total} validated)", nextText, nextColor);
                    }
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
        // Do NOT restore packageHealth or change currentState. Repositioning does not heal or reset the game.
    }

    private void TakeDamage(float amount) {
        if (currentState != DeliveryState.CollectingStamps && currentState != DeliveryState.HeadingToFinalDestination) return;

        packageHealth = Mathf.Max(0f, packageHealth - amount);
        if (UIManager.Instance != null) {
            UIManager.Instance.UpdateHealthSlider(packageHealth);
        }

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

        // Reset the whole state of the game anytime 'R' (ResetPressed) is pressed
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

        if (resetPressed && !isRestarting) {
            StartCoroutine(RestartGameCoroutine());
        }
    }

    private IEnumerator RestartGameCoroutine() {
        isRestarting = true;
        Time.timeScale = 1f;

        MapGenerator.IsRestarting = true;

        if (UIManager.Instance != null) {
            UIManager.Instance.SetGenerationPanelActive(true);
        }

        // Wait for the generation panel fade animation to finish
        yield return new WaitForSecondsRealtime(0.4f);

        string sceneName = SceneLoader.Instance.GetActiveSceneName();
        AsyncOperation asyncLoad = SceneLoader.Instance.LoadSceneAsync(sceneName);

        while (asyncLoad != null && !asyncLoad.isDone) {
            yield return null;
        }
    }

    public void ProcessCollisionDamage(float impactSpeed, bool isNPC) {
        if (currentState != DeliveryState.CollectingStamps && currentState != DeliveryState.HeadingToFinalDestination) return;

        if (impactSpeed > minCollisionSpeed) {
            float excess = impactSpeed - minCollisionSpeed;
            int damage = Mathf.RoundToInt(excess * collisionDamageMultiplier);
            
            if (damage > 0) {
                TakeDamage(damage);

                if (UIManager.Instance != null) {
                    string msg = isNPC ? $"TRAFFIC CRASH! Evidence Damaged -{damage}%" : $"IMPACT! Evidence Damaged -{damage}%";
                    UIManager.Instance.FlashDamageText(msg);
                }
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
        pointerArrowMaterial.color = new Color(0.95f, 0.85f, 0.45f, 1f); // Pastel Gold

        foreach (var r in pointerArrow.GetComponentsInChildren<Renderer>()) {
            r.sharedMaterial = pointerArrowMaterial;
        }

        pointerArrow.SetActive(false);
    }



    private void OnDestroy() {
        if (pointerArrowMaterial != null) {
            Destroy(pointerArrowMaterial);
        }
    }
}
