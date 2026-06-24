using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

public class PedalUI : MonoBehaviour, IPointerDownHandler, IPointerUpHandler {
    public enum PedalType {
        Throttle,
        Brake,
        Handbrake
    }

    [Header("Settings")]
    public PedalType pedalType;
    public float rampSpeed = 2.5f; // Speed at which input value reaches 1 or 0 (simulates foot movement)

    public float InputValue { get; private set; }
    private bool isPressed;
    private Vector3 originalScale;

    private void Awake() {
        originalScale = transform.localScale;
    }

    public void OnPointerDown(PointerEventData eventData) {
        isPressed = true;
        transform.DOComplete();
        transform.DOScale(originalScale * 0.9f, 0.1f).SetEase(Ease.OutQuad);
    }

    public void OnPointerUp(PointerEventData eventData) {
        isPressed = false;
        transform.DOComplete();
        transform.DOScale(originalScale, 0.1f).SetEase(Ease.OutQuad);
    }

    private void Update() {
        // Ramp value up or down for smoother analog feel
        float targetValue = isPressed ? 1f : 0f;
        InputValue = Mathf.MoveTowards(InputValue, targetValue, Time.deltaTime * rampSpeed);

        if (CarInputManager.Instance != null) {
            switch (pedalType) {
                case PedalType.Throttle:
                    CarInputManager.Instance.SetMobileThrottle(InputValue);
                    break;
                case PedalType.Brake:
                    CarInputManager.Instance.SetMobileBrake(InputValue);
                    break;
                case PedalType.Handbrake:
                    CarInputManager.Instance.SetMobileHandbrake(isPressed);
                    break;
            }
        }
    }

    private void OnDisable() {
        // Reset when UI is disabled
        isPressed = false;
        InputValue = 0f;
        if (CarInputManager.Instance != null) {
            switch (pedalType) {
                case PedalType.Throttle:
                    CarInputManager.Instance.SetMobileThrottle(0f);
                    break;
                case PedalType.Brake:
                    CarInputManager.Instance.SetMobileBrake(0f);
                    break;
                case PedalType.Handbrake:
                    CarInputManager.Instance.SetMobileHandbrake(false);
                    break;
            }
        }
    }

    private void OnDestroy() {
        transform.DOKill();
    }
}
