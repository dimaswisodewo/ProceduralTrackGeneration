using UnityEngine;
using UnityEngine.UI;

public class FontManager : MonoBehaviour {
    private static FontManager instance;
    public static FontManager Instance {
        get {
            if (instance == null) {
                instance = FindObjectOfType<FontManager>();
                if (instance == null) {
                    GameObject go = new GameObject("FontManager");
                    instance = go.AddComponent<FontManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
    }

    public Font RegularFont { get; private set; }
    public Font BoldFont { get; private set; }

    private void Awake() {
        if (instance == null) {
            instance = this;
            DontDestroyOnLoad(gameObject);
            LoadFonts();
        } else if (instance != this) {
            Destroy(gameObject);
        }
    }

    private void LoadFonts() {
        RegularFont = Resources.Load<Font>("Play/Play-Regular");
        BoldFont = Resources.Load<Font>("Play/Play-Bold");

        if (RegularFont == null) {
            Debug.LogError("FontManager: Failed to load Play-Regular font from Resources.");
        }
        if (BoldFont == null) {
            Debug.LogError("FontManager: Failed to load Play-Bold font from Resources.");
        }
    }

    public void ApplyFontsToAllUI() {
        if (RegularFont == null || BoldFont == null) {
            LoadFonts();
        }

        Text[] texts = Resources.FindObjectsOfTypeAll<Text>();
        foreach (Text txt in texts) {
            // Make sure the object is part of a loaded scene and not a prefab template asset
            if (txt != null && txt.gameObject != null && txt.gameObject.scene.isLoaded) {
                ApplyFontToText(txt);
            }
        }
    }

    public void ApplyFontToText(Text textComponent) {
        if (textComponent == null) return;
        if (RegularFont == null || BoldFont == null) LoadFonts();

        if (textComponent.fontStyle == FontStyle.Bold) {
            if (BoldFont != null) {
                textComponent.font = BoldFont;
            }
        } else {
            if (RegularFont != null) {
                textComponent.font = RegularFont;
            }
        }

        // Remove any Outline components to switch back to Shadow
        Outline[] outlines = textComponent.GetComponents<Outline>();
        foreach (var outline in outlines) {
            Destroy(outline);
        }

        // Add/configure the standard Shadow component (must not be an Outline)
        Shadow shadow = null;
        Shadow[] shadows = textComponent.GetComponents<Shadow>();
        foreach (var s in shadows) {
            if (!(s is Outline)) {
                shadow = s;
                break;
            }
        }
        if (shadow == null) {
            shadow = textComponent.gameObject.AddComponent<Shadow>();
        }
        if (shadow != null) {
            shadow.effectColor = Color.black;
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
        }
    }
}
