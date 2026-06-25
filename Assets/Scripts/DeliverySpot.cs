using UnityEngine;
using DG.Tweening;

public class DeliverySpot : MonoBehaviour {
    public bool isDeliveryTarget = false;
    public bool isPickupTarget = false;

    private GameObject ringVisual;
    private Material ringMaterial;
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

        Color targetColor;
        if (isPickupTarget) {
            // Glowing pastel blue for package pickup
            targetColor = new Color(0.5f, 0.75f, 0.95f, 0.4f);
        } else if (isDeliveryTarget) {
            // Glowing pastel orange/peach for delivery destination
            targetColor = new Color(0.95f, 0.65f, 0.45f, 0.4f);
        } else {
            // Dim, semi-transparent light grey for inactive spots
            targetColor = new Color(0.6f, 0.6f, 0.6f, 0.08f);
        }

        VisualEffectUtility.ApplyMaterialColor(ringMaterial, targetColor);

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
    }
}
