using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CullingManager : MonoBehaviour {
    public static CullingManager Instance { get; private set; }

    [Header("Culling Settings")]
    [Tooltip("Distance from the player car within which objects are kept active.")]
    public float cullingRadius = 50f;

    [Tooltip("Time interval in seconds between culling updates.")]
    public float updateInterval = 0.5f;

    [Tooltip("Minimum distance the player must move before recalculating culling.")]
    public float playerMoveThreshold = 1f;

    private Transform playerTransform;
    private List<CullableObject> cullableObjects = new List<CullableObject>();
    private bool playerSearched = false;

    // Track the last player position to avoid redundant calculations when stationary
    private Vector3 lastPlayerPos;
    private bool hasLastPlayerPos = false;

    private struct CullableObject {
        public GameObject gameObject;
        public Vector3 position;
        public bool isActive; // Cache active state to avoid slow Unity C++ API calls (activeSelf)
    }

    private void Awake() {
        if (Instance == null) {
            Instance = this;
        } else {
            Destroy(gameObject);
        }
    }

    private void Start() {
        StartCoroutine(CullingRoutine());
    }

    public void RegisterCullable(GameObject obj, Vector3 position) {
        if (obj == null) return;

        // Determine initial active state based on current player position
        bool initiallyActive = true;
        if (playerTransform != null) {
            float sqrDist = (position - playerTransform.position).sqrMagnitude;
            initiallyActive = sqrDist <= cullingRadius * cullingRadius;
        }
        
        obj.SetActive(initiallyActive);

        cullableObjects.Add(new CullableObject {
            gameObject = obj,
            position = position,
            isActive = initiallyActive
        });
    }

    public void ClearRegistry() {
        cullableObjects.Clear();
        playerTransform = null;
        playerSearched = false;
        hasLastPlayerPos = false;
    }

    private IEnumerator CullingRoutine() {
        WaitForSeconds wait = new WaitForSeconds(updateInterval);
        while (true) {
            UpdateCulling();
            yield return wait;
        }
    }

    private void UpdateCulling() {
        if (playerTransform == null) {
            LocatePlayer();
            if (playerTransform == null) return;
        }

        Vector3 playerPos = playerTransform.position;

        // Optimization: Skip update if player has not moved enough
        float sqrThreshold = playerMoveThreshold * playerMoveThreshold;
        if (hasLastPlayerPos && (playerPos - lastPlayerPos).sqrMagnitude < sqrThreshold) {
            return;
        }

        lastPlayerPos = playerPos;
        hasLastPlayerPos = true;

        float sqrRadius = cullingRadius * cullingRadius;
        int count = cullableObjects.Count;

        for (int i = 0; i < count; i++) {
            CullableObject item = cullableObjects[i];
            if (item.gameObject == null) continue;

            float sqrDist = (item.position - playerPos).sqrMagnitude;
            bool shouldBeActive = sqrDist <= sqrRadius;

            // Optimization: Only toggle active state if it differs from our cached C# value
            if (item.isActive != shouldBeActive) {
                item.gameObject.SetActive(shouldBeActive);
                item.isActive = shouldBeActive;
                cullableObjects[i] = item; // Write back the modified struct to the list
            }
        }
    }

    private void LocatePlayer() {
        CarController car = FindObjectOfType<CarController>();
        if (car != null) {
            playerTransform = car.transform;
        } else if (!playerSearched) {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) {
                playerTransform = playerObj.transform;
            }
            playerSearched = true;
        }
    }
}
