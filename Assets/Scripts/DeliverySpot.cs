using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

public class DeliverySpot : MonoBehaviour {
    public bool isDeliveryTarget = false;
    public bool isPickupTarget = false;

    private GameObject ringVisual;
    private Material ringMaterial;
    private List<Material> spotMaterials = new List<Material>();
    private Tween pulseTween;

    private void Start() {
        // 1. Create a dedicated trigger collider for checking player arrival
        // This avoids modifying any existing model colliders.
        BoxCollider trigger = gameObject.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(3f, 3f, 3f);
        trigger.center = new Vector3(0f, 1.5f, 0f);

        // 2. Create the flat visual ring representing the zone
        CreateVisualRing();

        // 3. Initialize the 3D spot object (cube/prefab) materials with SpotMaterial copy so they can glow
        InitializeSpotMaterials();
    }

    private void CreateVisualRing() {
        // Create a flat cylinder to act as a landing pad/zone ring
        ringVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(ringVisual.GetComponent<Collider>()); // Visual only, remove physics

        ringVisual.transform.parent = this.transform;
        ringVisual.transform.localPosition = new Vector3(0f, 0.05f, 0f); // Hover slightly above ground
        ringVisual.transform.localScale = new Vector3(3f, 0.02f, 3f); // Wide and flat
        ringVisual.transform.localRotation = Quaternion.identity;
        ringVisual.layer = 30; // Main Camera Only / Hide from Minimap

        Renderer renderer = ringVisual.GetComponent<Renderer>();
        if (renderer != null) {
            ringMaterial = VisualEffectUtility.GetSpotMaterial();
            renderer.sharedMaterial = ringMaterial;
        }

        UpdateVisuals();
    }

    private void InitializeSpotMaterials() {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (var r in renderers) {
            if (ringVisual != null && r.gameObject == ringVisual) {
                continue;
            }
            
            // Assign a unique copy of the spot material
            Material mat = VisualEffectUtility.GetSpotMaterial();
            
            // Configure it to be completely opaque so it contrasts with other buildings
            VisualEffectUtility.ConfigureOpaqueMaterial(mat);
            
            r.sharedMaterial = mat;
            spotMaterials.Add(mat);
        }
        UpdateVisuals();
    }

    public void SetAsTarget(bool isTarget, bool isPickup = false) {
        isDeliveryTarget = isTarget && !isPickup;
        isPickupTarget = isTarget && isPickup;
        UpdateVisuals();
    }

    private void UpdateVisuals() {
        if (ringMaterial == null) return;

        if (pulseTween != null) {
            pulseTween.Kill();
            pulseTween = null;
        }

        Color ringColor;
        Color spotColor;
        Color emissionColor = Color.black;
        bool enableEmission = false;

        if (isPickupTarget) {
            // Glowing purple for stamp spots (every spot)
            ringColor = new Color(0.75f, 0.2f, 0.95f, 0.5f); // Transparent Purple
            spotColor = new Color(0.75f, 0.2f, 0.95f, 1.0f); // Opaque Solid Purple
            emissionColor = new Color(0.75f, 0.2f, 0.95f) * 2.5f; // Stronger HDR Emission to contrast
            enableEmission = true;
        } else if (isDeliveryTarget) {
            // Glowing orange for the last destination
            ringColor = new Color(1.0f, 0.45f, 0.0f, 0.5f); // Transparent Orange
            spotColor = new Color(1.0f, 0.45f, 0.0f, 1.0f); // Opaque Solid Orange
            emissionColor = new Color(1.0f, 0.45f, 0.0f) * 2.5f; // Stronger HDR Emission to contrast
            enableEmission = true;
        } else {
            // Dim, semi-transparent light grey for inactive spots
            ringColor = new Color(0.6f, 0.6f, 0.6f, 0.08f);
            spotColor = new Color(0.35f, 0.35f, 0.35f, 1.0f); // Make inactive spot opaque grey
            emissionColor = Color.black;
            enableEmission = false;
        }

        // Apply to the ring (transparent)
        VisualEffectUtility.ApplyMaterialColor(ringMaterial, ringColor, emissionColor, enableEmission);

        // Apply to the main spot 3D object (opaque)
        if (spotMaterials != null) {
            foreach (var mat in spotMaterials) {
                VisualEffectUtility.ApplyMaterialColor(mat, spotColor, emissionColor, enableEmission);
            }
        }

        if (ringVisual != null) {
            if (isPickupTarget || isDeliveryTarget) {
                // Animate scale to pulsate
                ringVisual.transform.localScale = new Vector3(3f, 0.02f, 3f);
                pulseTween = ringVisual.transform.DOScale(new Vector3(3.6f, 0.02f, 3.6f), 0.8f)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine);
            } else {
                // Settle back to default scale for inactive zones
                ringVisual.transform.localScale = new Vector3(3f, 0.02f, 3f);
            }
        }
    }

    private void OnTriggerEnter(Collider other) {
        // Find the CarController on the entering object or its parents
        CarController car = other.GetComponentInParent<CarController>();
        if (car != null) {
            if (PackageDeliverySystem.Instance != null) {
                PackageDeliverySystem.Instance.OnCarEnteredSpot(this);
            }
        }
    }

    private void OnDestroy() {
        if (pulseTween != null) {
            pulseTween.Kill();
        }
        if (ringMaterial != null) {
            Destroy(ringMaterial);
        }
        if (spotMaterials != null) {
            foreach (var mat in spotMaterials) {
                if (mat != null) {
                    Destroy(mat);
                }
            }
            spotMaterials.Clear();
        }
    }
}
