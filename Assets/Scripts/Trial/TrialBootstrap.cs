using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Runtime-only entry point for the improved assignment demo. It keeps the old
/// scene/prefabs intact while adding polish systems automatically when the game
/// starts.
/// </summary>
public class TrialBootstrap : MonoBehaviour
{
    private const string BootstrapName = "[Trial] Runtime Bootstrap";

    private static bool sceneHooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapAfterSceneLoad()
    {
        EnsureBootstrap();

        if (!sceneHooked)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            sceneHooked = true;
        }
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureBootstrap();
    }

    private static void EnsureBootstrap()
    {
        if (FindObjectOfType<TrialBootstrap>() != null)
        {
            return;
        }

        GameObject bootstrap = new GameObject(BootstrapName);
        DontDestroyOnLoad(bootstrap);
        bootstrap.AddComponent<TrialBootstrap>();
    }

    private void Awake()
    {
        TrialBootstrap[] bootstraps = FindObjectsOfType<TrialBootstrap>();
        if (bootstraps.Length > 1)
        {
            Destroy(gameObject);
            return;
        }

        DontDestroyOnLoad(gameObject);
        ApplyPerformanceDefaults();
        EnsureRuntimeSystems();
    }

    private void Update()
    {
        EnsureRuntimeSystems();
    }

    private static void EnsureRuntimeSystems()
    {
        TrialHud.EnsureExists();
        TrialChallengeDirector.EnsureExists();
    }

    private static void ApplyPerformanceDefaults()
    {
        Application.targetFrameRate = 120;
        QualitySettings.vSyncCount = 0;
        Time.fixedDeltaTime = 1f / 60f;
        Time.maximumDeltaTime = 0.08f;

        if (QualitySettings.antiAliasing < 2)
        {
            QualitySettings.antiAliasing = 2;
        }

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.012f;
        RenderSettings.fogColor = new Color(0.62f, 0.68f, 0.70f);
        RenderSettings.ambientLight = new Color(0.34f, 0.36f, 0.38f);
    }
}
