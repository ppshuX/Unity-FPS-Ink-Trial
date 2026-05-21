using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TrialHud : MonoBehaviour
{
    private const string HudName = "[Trial] Assignment HUD";

    public static TrialHud Singleton { get; private set; }

    private Canvas canvas;
    private TextMeshProUGUI statusText;
    private TextMeshProUGUI objectiveText;
    private TextMeshProUGUI vitalsText;
    private TextMeshProUGUI missionText;
    private TextMeshProUGUI warningText;
    private TextMeshProUGUI feedbackText;
    private TextMeshProUGUI hitMarkerText;
    private TextMeshProUGUI helpText;
    private TextMeshProUGUI overlayTitleText;
    private TextMeshProUGUI overlayBodyText;
    private Image damageOverlay;
    private CanvasGroup overlayGroup;
    private CanvasGroup feedbackGroup;
    private CanvasGroup hitMarkerGroup;

    private float feedbackTimer;
    private float hitMarkerTimer;
    private float damageFlashTimer;
    private float warningTimer;
    private bool overlayVisible;

    public static void EnsureExists()
    {
        if (Singleton != null)
        {
            return;
        }

        TrialHud existing = FindObjectOfType<TrialHud>();
        if (existing != null)
        {
            Singleton = existing;
            return;
        }

        GameObject hud = new GameObject(HudName);
        DontDestroyOnLoad(hud);
        hud.AddComponent<TrialHud>();
    }

    private void Awake()
    {
        if (Singleton != null && Singleton != this)
        {
            Destroy(gameObject);
            return;
        }

        Singleton = this;
        DontDestroyOnLoad(gameObject);
        BuildHud();
        RefreshWaitingState();
    }

    private void Update()
    {
        if (feedbackGroup != null)
        {
            feedbackTimer -= Time.unscaledDeltaTime;
            feedbackGroup.alpha = Mathf.MoveTowards(
                feedbackGroup.alpha,
                feedbackTimer > 0f ? 1f : 0f,
                Time.unscaledDeltaTime * 4.5f);
        }

        if (hitMarkerGroup != null)
        {
            hitMarkerTimer -= Time.unscaledDeltaTime;
            hitMarkerGroup.alpha = Mathf.MoveTowards(
                hitMarkerGroup.alpha,
                hitMarkerTimer > 0f ? 1f : 0f,
                Time.unscaledDeltaTime * 8f);
        }

        if (damageOverlay != null)
        {
            damageFlashTimer -= Time.unscaledDeltaTime;
            Color color = damageOverlay.color;
            color.a = Mathf.MoveTowards(color.a, damageFlashTimer > 0f ? 0.38f : 0f, Time.unscaledDeltaTime * 2.9f);
            damageOverlay.color = color;
        }

        if (warningText != null)
        {
            warningTimer -= Time.unscaledDeltaTime;
            Color color = warningText.color;
            color.a = Mathf.MoveTowards(color.a, warningTimer > 0f ? 1f : 0f, Time.unscaledDeltaTime * 5f);
            warningText.color = color;
        }

        if (overlayGroup != null)
        {
            overlayGroup.alpha = Mathf.MoveTowards(
                overlayGroup.alpha,
                overlayVisible ? 1f : 0f,
                Time.unscaledDeltaTime * 8f);
            overlayGroup.blocksRaycasts = overlayVisible;
        }
    }

    public void RefreshWaitingState()
    {
        if (statusText == null || objectiveText == null)
        {
            return;
        }

        statusText.text = "INK FPS TRIAL\nScore 0   Wave --   Time --";
        objectiveText.text = "Click LOCAL TRIAL or press L to start a playable solo demo.";
        if (vitalsText != null)
        {
            vitalsText.text = "Health --   Dash --";
        }
        if (missionText != null)
        {
            missionText.text = "Missions: start a local trial";
        }
    }

    public void RefreshChallenge(
        int score,
        int highScore,
        int wave,
        float timeLeft,
        int activeTargets,
        int totalTargets,
        int activeSpecters,
        int activePickups,
        int combo,
        float accuracy,
        bool running)
    {
        if (statusText == null || objectiveText == null)
        {
            return;
        }

        string time = running ? Mathf.CeilToInt(Mathf.Max(0f, timeLeft)).ToString() : "--";
        statusText.text =
            "INK FPS TRIAL\n" +
            "Score " + score + "   Best " + highScore + "   Wave " + wave + "   Time " + time;

        string comboText = combo > 1 ? "   Combo x" + combo : "";
        objectiveText.text =
            "Targets " + Mathf.Max(0, activeTargets) + "/" + Mathf.Max(1, totalTargets) +
            "   Specters " + Mathf.Max(0, activeSpecters) +
            "   Pickups " + Mathf.Max(0, activePickups) +
            "   Accuracy " + Mathf.RoundToInt(accuracy * 100f) + "%" +
            comboText;
    }

    public void RefreshVitals(int health, int maxHealth, float dashReady01, bool dashReady)
    {
        if (vitalsText == null)
        {
            return;
        }

        int healthPercent = Mathf.RoundToInt(Mathf.Clamp01((float)health / Mathf.Max(1, maxHealth)) * 100f);
        int dashPercent = Mathf.RoundToInt(Mathf.Clamp01(dashReady01) * 100f);
        string dashText = dashReady ? "READY" : dashPercent + "%";
        vitalsText.text = "Health " + healthPercent + "%   Dash " + dashText;

        if (healthPercent <= 35)
        {
            vitalsText.color = new Color(1f, 0.42f, 0.34f);
        }
        else if (dashReady)
        {
            vitalsText.color = new Color(0.68f, 0.95f, 1f);
        }
        else
        {
            vitalsText.color = new Color(0.88f, 0.9f, 0.86f);
        }
    }

    public void RefreshMissions(string missions)
    {
        if (missionText == null)
        {
            return;
        }

        missionText.text = missions;
    }

    public void ShowFeedback(string message)
    {
        if (feedbackText == null || feedbackGroup == null)
        {
            return;
        }

        feedbackText.text = message;
        feedbackTimer = 1.35f;
        feedbackGroup.alpha = 1f;
    }

    public void ShowHitMarker(bool critical)
    {
        if (hitMarkerText == null || hitMarkerGroup == null)
        {
            return;
        }

        hitMarkerText.text = critical ? "X" : "+";
        hitMarkerText.color = critical ? new Color(1f, 0.78f, 0.32f) : Color.white;
        hitMarkerTimer = 0.14f;
        hitMarkerGroup.alpha = 1f;
    }

    public void ShowDamageFlash()
    {
        damageFlashTimer = 0.42f;
        if (damageOverlay != null)
        {
            damageOverlay.color = new Color(1f, 0.05f, 0.02f, 0.38f);
        }
    }

    public void ShowThreatWarning(string message)
    {
        if (warningText == null)
        {
            return;
        }

        warningText.text = message;
        warningTimer = 0.65f;
        warningText.color = new Color(1f, 0.22f, 0.16f, 1f);
    }

    public void ShowPause(bool paused)
    {
        if (overlayTitleText == null || overlayBodyText == null)
        {
            return;
        }

        overlayVisible = paused;
        if (!paused)
        {
            return;
        }

        overlayTitleText.text = "PAUSED";
        overlayBodyText.text = "Press P to resume\nPress T to restart trial\nEsc releases cursor";
    }

    public void ShowResult(
        int score,
        int highScore,
        int wave,
        int maxCombo,
        int targetsDestroyed,
        int spectersDestroyed,
        int pickupsCollected,
        float accuracy,
        string rank,
        bool newBest,
        string awards)
    {
        if (overlayTitleText == null || overlayBodyText == null)
        {
            return;
        }

        overlayVisible = true;
        overlayTitleText.text = "TRIAL COMPLETE  RANK " + rank;
        overlayBodyText.text =
            "Score " + score + (newBest ? "  NEW BEST" : "  Best " + highScore) + "\n" +
            "Wave " + wave + "   Accuracy " + Mathf.RoundToInt(accuracy * 100f) + "%   Max Combo x" + maxCombo + "\n" +
            "Targets " + targetsDestroyed + "   Specters " + spectersDestroyed + "   Pickups " + pickupsCollected + "\n" +
            awards + "\n" +
            "Press T to run again";
    }

    public void HideOverlay()
    {
        overlayVisible = false;
    }

    private void BuildHud()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();

        Image panel = CreateImage("Status Panel", transform, new Color(0.03f, 0.04f, 0.045f, 0.58f));
        RectTransform panelRect = panel.rectTransform;
        SetRect(panelRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -26f), new Vector2(540f, 116f));

        statusText = CreateText("Status Text", panelRect, 26, Color.white, TextAlignmentOptions.TopLeft);
        SetRect(statusText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 1f), new Vector2(18f, -12f), new Vector2(-36f, -24f));

        Image objectivePanel = CreateImage("Objective Panel", transform, new Color(0.02f, 0.02f, 0.025f, 0.42f));
        SetRect(objectivePanel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(620f, 46f));

        objectiveText = CreateText("Objective Text", objectivePanel.rectTransform, 22, new Color(0.88f, 0.94f, 0.92f), TextAlignmentOptions.Center);
        SetRect(objectiveText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-24f, -12f));

        GameObject feedback = new GameObject("Feedback");
        feedback.transform.SetParent(transform, false);
        feedbackGroup = feedback.AddComponent<CanvasGroup>();
        feedbackGroup.alpha = 0f;
        RectTransform feedbackRect = feedback.AddComponent<RectTransform>();
        SetRect(feedbackRect, new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, 80f));
        feedbackText = CreateText("Feedback Text", feedbackRect, 42, new Color(1f, 0.83f, 0.38f), TextAlignmentOptions.Center);
        SetRect(feedbackText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        GameObject hit = new GameObject("Hit Marker");
        hit.transform.SetParent(transform, false);
        hitMarkerGroup = hit.AddComponent<CanvasGroup>();
        hitMarkerGroup.alpha = 0f;
        RectTransform hitRect = hit.AddComponent<RectTransform>();
        SetRect(hitRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(80f, 80f));
        hitMarkerText = CreateText("Hit Marker Text", hitRect, 48, Color.white, TextAlignmentOptions.Center);
        SetRect(hitMarkerText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        helpText = CreateText("Help Text", transform, 20, new Color(0.88f, 0.9f, 0.86f, 0.86f), TextAlignmentOptions.BottomLeft);
        SetRect(helpText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 24f), new Vector2(760f, 72f));
        helpText.text = "WASD Move   Shift Sprint   Ctrl Dash   RMB Focus   Q/1/2 Switch   R Reload   P Pause   T Restart   Esc Cursor";

        Image vitalsPanel = CreateImage("Vitals Panel", transform, new Color(0.03f, 0.04f, 0.045f, 0.44f));
        SetRect(vitalsPanel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-26f, -26f), new Vector2(360f, 54f));
        vitalsText = CreateText("Vitals Text", vitalsPanel.rectTransform, 23, new Color(0.88f, 0.9f, 0.86f), TextAlignmentOptions.Center);
        SetRect(vitalsText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-18f, -10f));

        Image missionPanel = CreateImage("Mission Panel", transform, new Color(0.02f, 0.025f, 0.028f, 0.42f));
        SetRect(missionPanel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 86f), new Vector2(900f, 40f));
        missionText = CreateText("Mission Text", missionPanel.rectTransform, 19, new Color(0.88f, 0.94f, 0.86f), TextAlignmentOptions.Center);
        SetRect(missionText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-20f, -8f));

        warningText = CreateText("Warning Text", transform, 34, new Color(1f, 0.22f, 0.16f, 0f), TextAlignmentOptions.Center);
        SetRect(warningText.rectTransform, new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 54f));

        damageOverlay = CreateImage("Damage Overlay", transform, new Color(1f, 0.05f, 0.02f, 0f));
        SetRect(damageOverlay.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        damageOverlay.transform.SetAsFirstSibling();

        GameObject overlay = new GameObject("Trial Overlay");
        overlay.transform.SetParent(transform, false);
        overlayGroup = overlay.AddComponent<CanvasGroup>();
        overlayGroup.alpha = 0f;
        overlayGroup.blocksRaycasts = false;
        RectTransform overlayRect = overlay.AddComponent<RectTransform>();
        SetRect(overlayRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        Image overlayBack = CreateImage("Overlay Back", overlayRect, new Color(0.01f, 0.012f, 0.014f, 0.72f));
        SetRect(overlayBack.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        overlayTitleText = CreateText("Overlay Title", overlayRect, 54, new Color(1f, 0.82f, 0.38f), TextAlignmentOptions.Center);
        SetRect(overlayTitleText.rectTransform, new Vector2(0.5f, 0.54f), new Vector2(0.5f, 0.54f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(920f, 80f));

        overlayBodyText = CreateText("Overlay Body", overlayRect, 28, new Color(0.9f, 0.94f, 0.92f), TextAlignmentOptions.Center);
        overlayBodyText.enableWordWrapping = true;
        SetRect(overlayBodyText.rectTransform, new Vector2(0.5f, 0.42f), new Vector2(0.5f, 0.42f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(980f, 210f));
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        Image image = obj.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, int fontSize, Color color, TextAlignmentOptions alignment)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        TextMeshProUGUI text = obj.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.enableWordWrapping = false;
        return text;
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
    }
}
