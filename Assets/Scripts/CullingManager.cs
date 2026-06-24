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

    private Transform playerTransform;
    private List<CullableObject> cullableObjects = new List<CullableObject>();
    private bool playerSearched = false;

    private struct CullableObject {
        public GameObject gameObject;
        public Vector3 position;
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
        cullableObjects.Add(new CullableObject {
            gameObject = obj,
            position = position
        });
        
        // Initially set active state based on player position if player is already spawned
        if (playerTransform != null) {
            float sqrDist = (position - playerTransform.position).sqrMagnitude;
            obj.SetActive(sqrDist <= cullingRadius * cullingRadius);
        } else {
            // Keep active initially so player doesn't start in void before first update
            obj.SetActive(true);
        }
    }

    public void ClearRegistry() {
        cullableObjects.Clear();
        playerTransform = null;
        playerSearched = false;
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

            if (playerTransform == null) return;
        }

        Vector3 playerPos = playerTransform.position;
        float sqrRadius = cullingRadius * cullingRadius;

        int count = cullableObjects.Count;
        for (int i = 0; i < count; i++) {
            CullableObject item = cullableObjects[i];
            if (item.gameObject == null) continue;

            float sqrDist = (item.position - playerPos).sqrMagnitude;
            bool shouldBeActive = sqrDist <= sqrRadius;

            if (item.gameObject.activeSelf != shouldBeActive) {
                item.gameObject.SetActive(shouldBeActive);
            }
        }
    }
}
