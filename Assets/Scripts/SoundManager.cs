using UnityEngine;
using System.Collections;

/// <summary>
/// Centralized persistent sound manager for controlling BGMs and SFXs.
/// </summary>
public class SoundManager : MonoBehaviour {
    private static SoundManager instance;
    public static SoundManager Instance {
        get {
            if (instance == null) {
                instance = FindObjectOfType<SoundManager>();
                if (instance == null) {
                    GameObject go = new GameObject("SoundManager");
                    instance = go.AddComponent<SoundManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
    }

    [Header("BGM Clips")]
    [SerializeField] private AudioClip bgmGameplay;
    [SerializeField] private AudioClip bgmWinning;

    [Header("SFX Clips")]
    [SerializeField] private AudioClip sfxHitObstacle;
    [SerializeField] private AudioClip sfxObjectiveSuccess;
    [SerializeField] private AudioClip sfxHealthZero;
    [SerializeField] private AudioClip sfxDrifting;
    [SerializeField] private AudioClip sfxCarEngine;
    [SerializeField] private AudioClip sfxDialogue;

    // AudioSources
    private AudioSource bgmSource;
    private AudioSource engineSource;
    private AudioSource driftSource;
    private AudioSource sfxSource;
    private AudioSource dialogueSource;
    private AudioSource popSource;

    private Coroutine bgmCrossfadeCoroutine;

    private void Awake() {
        if (instance == null) {
            instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeAudioSources();
        } else if (instance != this) {
            Destroy(gameObject);
        }
    }

    private void InitializeAudioSources() {
        // Create BGM source
        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.loop = true;
        bgmSource.playOnAwake = false;

        // Create Engine source
        engineSource = gameObject.AddComponent<AudioSource>();
        engineSource.loop = true;
        engineSource.playOnAwake = false;
        engineSource.clip = sfxCarEngine;

        // Create Drift source
        driftSource = gameObject.AddComponent<AudioSource>();
        driftSource.loop = true;
        driftSource.playOnAwake = false;
        driftSource.clip = sfxDrifting;

        // Create general SFX source
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.loop = false;
        sfxSource.playOnAwake = false;

        // Create dialogue SFX source
        dialogueSource = gameObject.AddComponent<AudioSource>();
        dialogueSource.loop = false;
        dialogueSource.playOnAwake = false;
        dialogueSource.clip = sfxDialogue;

        // Create pop source
        popSource = gameObject.AddComponent<AudioSource>();
        popSource.loop = false;
        popSource.playOnAwake = false;
    }

    // --- BGM Controls ---
    public void PlayBGMGameplay() {
        PlayBGM(bgmGameplay);
    }

    public void PlayBGMWinning() {
        PlayBGM(bgmWinning);
    }

    public void StopBGM() {
        if (bgmCrossfadeCoroutine != null) {
            StopCoroutine(bgmCrossfadeCoroutine);
        }
        bgmCrossfadeCoroutine = StartCoroutine(CrossfadeBGM(null));
    }

    /// <summary>
    /// Pauses the currently playing BGM.
    /// </summary>
    public void PauseBGM() {
        if (bgmSource != null && bgmSource.isPlaying) {
            bgmSource.Pause();
        }
    }

    /// <summary>
    /// Resumes the paused BGM.
    /// </summary>
    public void ResumeBGM() {
        if (bgmSource != null && !bgmSource.isPlaying && bgmSource.clip != null) {
            bgmSource.UnPause();
        }
    }

    /// <summary>
    /// Fades the currently playing BGM out and then pauses it.
    /// </summary>
    public void FadeAndPauseBGM(float duration = 0.5f) {
        if (bgmCrossfadeCoroutine != null) {
            StopCoroutine(bgmCrossfadeCoroutine);
        }
        bgmCrossfadeCoroutine = StartCoroutine(FadeAndPauseBGMCoroutine(duration));
    }

    private IEnumerator FadeAndPauseBGMCoroutine(float duration) {
        float elapsed = 0f;
        if (bgmSource != null && bgmSource.isPlaying) {
            float startVol = bgmSource.volume;
            while (elapsed < duration) {
                elapsed += Time.deltaTime;
                bgmSource.volume = Mathf.Lerp(startVol, 0f, elapsed / duration);
                yield return null;
            }
            bgmSource.Pause();
        }
    }

    private void PlayBGM(AudioClip clip) {
        if (clip == null) return;
        if (bgmSource != null && bgmSource.clip == clip && bgmSource.isPlaying) return;

        if (bgmCrossfadeCoroutine != null) {
            StopCoroutine(bgmCrossfadeCoroutine);
        }
        bgmCrossfadeCoroutine = StartCoroutine(CrossfadeBGM(clip));
    }

    private IEnumerator CrossfadeBGM(AudioClip newClip) {
        float duration = 0.5f;
        float elapsed = 0f;

        if (bgmSource != null && bgmSource.isPlaying) {
            float startVol = bgmSource.volume;
            while (elapsed < duration) {
                elapsed += Time.deltaTime;
                bgmSource.volume = Mathf.Lerp(startVol, 0f, elapsed / duration);
                yield return null;
            }
        }

        if (bgmSource != null) {
            bgmSource.clip = newClip;
            if (newClip != null) {
                bgmSource.Play();
                elapsed = 0f;
                while (elapsed < duration) {
                    elapsed += Time.deltaTime;
                    bgmSource.volume = Mathf.Lerp(0f, 1.0f, elapsed / duration);
                    yield return null;
                }
            } else {
                bgmSource.Stop();
            }
        }
    }

    // --- SFX Play Methods ---
    public void PlaySFXHitObstacle() {
        if (sfxHitObstacle != null && sfxSource != null) {
            sfxSource.PlayOneShot(sfxHitObstacle);
        }
    }

    public void PlaySFXObjectiveSuccess() {
        if (sfxObjectiveSuccess != null && sfxSource != null) {
            sfxSource.PlayOneShot(sfxObjectiveSuccess);
        }
    }

    public void PlaySFXHealthZero() {
        if (sfxHealthZero != null && sfxSource != null) {
            sfxSource.PlayOneShot(sfxHealthZero);
        }
    }

    public void PlaySFXDialogue(float volumeScale = 0.15f) {
        if (sfxDialogue != null && dialogueSource != null) {
            dialogueSource.PlayOneShot(sfxDialogue, volumeScale);
        }
    }

    /// <summary>
    /// Plays the standard UI Click SFX (using dialogue click clip).
    /// </summary>
    public void PlaySFXClick(float volumeScale = 0.5f) {
        if (sfxDialogue != null && sfxSource != null) {
            sfxSource.PlayOneShot(sfxDialogue, volumeScale);
        }
    }

    // --- Engine & Drift Loops ---
    public void SetEngineSFXActive(bool active) {
        if (engineSource == null || sfxCarEngine == null) return;

        if (active) {
            if (!engineSource.isPlaying) {
                engineSource.Play();
            }
        } else {
            if (engineSource.isPlaying) {
                engineSource.Stop();
            }
        }
    }

    public void UpdateEngineSFX(float rpm, float load, int gear) {
        if (engineSource == null || !engineSource.isPlaying) return;

        // Pitch is determined by simulated RPM (rpm is 0 to 1)
        float basePitch = Mathf.Lerp(0.65f, 2.3f, rpm);

        // Make pitch slightly higher in higher gears to sound faster/more intense
        float gearPitchModifier = 1f + (gear - 1) * 0.04f;
        engineSource.pitch = basePitch * gearPitchModifier;

        // Volume increases as RPM and gear goes up
        float baseVolume = Mathf.Lerp(0.28f, 0.85f, rpm);
        
        // Increase volume slightly in higher gears
        float gearVolumeModifier = 1f + (gear - 1) * 0.05f;
        float loadVolumeMultiplier = Mathf.Lerp(0.65f, 1.0f, load);
        
        engineSource.volume = baseVolume * gearVolumeModifier * loadVolumeMultiplier;
    }

    public void PlayGearShiftPop() {
        if (sfxHitObstacle != null && popSource != null) {
            popSource.pitch = Random.Range(1.4f, 1.9f);
            popSource.PlayOneShot(sfxHitObstacle, Random.Range(0.06f, 0.12f));
        }
    }

    public void SetDriftSFXActive(bool active) {
        if (driftSource == null || sfxDrifting == null) return;

        if (active) {
            if (!driftSource.isPlaying) {
                driftSource.Play();
            }
        } else {
            if (driftSource.isPlaying) {
                driftSource.Stop();
            }
        }
    }
}
