using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TrialChallengeDirector : MonoBehaviour
{
    private const string DirectorName = "[Trial] Challenge Director";
    private const string HighScoreKey = "InkTrialHighScore";

    public static TrialChallengeDirector Singleton { get; private set; }

    [SerializeField]
    private float runDuration = 90f;
    [SerializeField]
    private int baseTargetsPerWave = 4;
    [SerializeField]
    private int maxTargetsPerWave = 10;

    private readonly List<TrialTarget> activeTargets = new List<TrialTarget>();
    private readonly List<TrialPickup> activePickups = new List<TrialPickup>();
    private readonly List<TrialSpecter> activeSpecters = new List<TrialSpecter>();
    private readonly List<GameObject> activeDecor = new List<GameObject>();
    private Player localPlayer;
    private TrialPlayerAbility playerAbility;
    private int score;
    private int highScore;
    private int wave = 1;
    private int waveTargetCount;
    private int combo;
    private int maxCombo;
    private int targetsDestroyed;
    private int spectersDestroyed;
    private int eliteSpectersDestroyed;
    private int pickupsCollected;
    private int damageTaken;
    private int shotsFired;
    private int shotsHit;
    private float timeLeft;
    private float comboTimer;
    private float nextPlayerScanTime;
    private bool running;
    private bool runHasStarted;
    private bool waveTransitioning;
    private bool paused;

    public static void EnsureExists()
    {
        if (Singleton != null)
        {
            return;
        }

        TrialChallengeDirector existing = FindObjectOfType<TrialChallengeDirector>();
        if (existing != null)
        {
            Singleton = existing;
            return;
        }

        GameObject director = new GameObject(DirectorName);
        DontDestroyOnLoad(director);
        director.AddComponent<TrialChallengeDirector>();
    }

    public static void SetLocalPlayer(Player player)
    {
        EnsureExists();
        if (Singleton != null)
        {
            Singleton.AssignLocalPlayer(player);
        }
    }

    public static Vector3 GetRecommendedRespawnPosition(Vector3 fallback)
    {
        if (Singleton == null)
        {
            return fallback;
        }

        return Singleton.GetSpawnPositionNearTrial(fallback);
    }

    public bool IsPaused()
    {
        return paused;
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
        highScore = PlayerPrefs.GetInt(HighScoreKey, 0);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            RestartRun();
        }

        if (Input.GetKeyDown(KeyCode.P))
        {
            TogglePause();
        }

        if (localPlayer == null && Time.unscaledTime >= nextPlayerScanTime)
        {
            nextPlayerScanTime = Time.unscaledTime + 0.5f;
            TryFindLocalPlayer();
        }

        if (localPlayer != null && !runHasStarted)
        {
            StartRun();
        }

        if (!running)
        {
            if (!runHasStarted && TrialHud.Singleton != null)
            {
                TrialHud.Singleton.RefreshWaitingState();
            }
            return;
        }

        if (paused)
        {
            RefreshHud();
            return;
        }

        timeLeft -= Time.deltaTime;
        comboTimer -= Time.deltaTime;
        if (comboTimer <= 0f)
        {
            combo = 0;
        }

        activeTargets.RemoveAll(target => target == null || !target.IsAlive);
        activePickups.RemoveAll(pickup => pickup == null);
        activeSpecters.RemoveAll(specter => specter == null || !specter.IsAlive);
        UpdateThreatWarning();

        if (timeLeft <= 0f)
        {
            EndRun();
            return;
        }

        if (!waveTransitioning && activeTargets.Count == 0)
        {
            StartCoroutine(AdvanceWave());
        }

        RefreshHud();
    }

    public void RegisterShotFired()
    {
        if (!running)
        {
            return;
        }

        shotsFired++;
        RefreshHud();
    }

    public void RegisterTargetHit(TrialTarget target, int weaponDamage, Vector3 hitPoint)
    {
        if (!running || target == null || !activeTargets.Contains(target))
        {
            return;
        }

        shotsHit++;

        float precision = target.GetPrecision(hitPoint);
        bool critical = precision >= 0.72f;
        int damage = Mathf.Max(1, Mathf.RoundToInt(weaponDamage * Mathf.Lerp(1.35f, 3.2f, precision)));
        if (critical)
        {
            damage += Mathf.Max(1, weaponDamage);
        }

        bool destroyed = target.ApplyDamage(damage, critical);
        combo = Mathf.Clamp(combo + 1, 1, 99);
        maxCombo = Mathf.Max(maxCombo, combo);
        comboTimer = 2.4f;

        int gained = Mathf.RoundToInt(8f + precision * 34f + combo * 3f);
        if (critical)
        {
            gained += 25;
            timeLeft += 0.45f;
        }

        if (destroyed)
        {
            activeTargets.Remove(target);
            targetsDestroyed++;
            gained += target.ScoreValue + combo * 6;
            timeLeft += 2.2f;
        }

        score += gained;

        if (TrialHud.Singleton != null)
        {
            TrialHud.Singleton.ShowHitMarker(critical);
            TrialHud.Singleton.ShowFeedback((critical ? "PERFECT " : destroyed ? "BREAK " : "HIT ") + "+" + gained);
        }

        RefreshHud();
    }

    public void RegisterSpecterHit(TrialSpecter specter, int weaponDamage)
    {
        if (!running || specter == null || !activeSpecters.Contains(specter))
        {
            return;
        }

        shotsHit++;
        combo = Mathf.Clamp(combo + 1, 1, 99);
        maxCombo = Mathf.Max(maxCombo, combo);
        comboTimer = 2.4f;

        bool destroyed = specter.ApplyDamage(Mathf.Max(1, weaponDamage * 2));
        int gained = destroyed ? specter.GetScoreValue() + combo * 8 : 16 + combo * 2;
        score += gained;

        if (destroyed)
        {
            if (specter.IsElite())
            {
                eliteSpectersDestroyed++;
            }
            activeSpecters.Remove(specter);
            spectersDestroyed++;
            timeLeft += 1.3f;
        }

        if (TrialHud.Singleton != null)
        {
            TrialHud.Singleton.ShowHitMarker(destroyed);
            TrialHud.Singleton.ShowFeedback((destroyed ? "SPECTER DOWN " : "SPECTER HIT ") + "+" + gained);
        }

        RefreshHud();
    }

    public void RegisterPlayerDamaged(int damage)
    {
        if (!running)
        {
            return;
        }

        damageTaken += Mathf.Max(0, damage);
        RefreshHud();
    }

    public void CollectPickup(TrialPickup pickup, Player player, TrialPickupKind kind, int amount)
    {
        if (!running || player == null)
        {
            return;
        }

        activePickups.Remove(pickup);
        pickupsCollected++;

        string message;
        switch (kind)
        {
            case TrialPickupKind.Health:
                player.RestoreHealth(amount);
                message = "HEAL +" + amount;
                break;
            case TrialPickupKind.Ammo:
                WeaponManager weaponManager = player.GetComponent<WeaponManager>();
                if (weaponManager != null)
                {
                    weaponManager.RefillAllAmmo();
                }
                message = "AMMO FULL";
                break;
            case TrialPickupKind.Time:
                timeLeft += amount;
                message = "TIME +" + amount;
                break;
            default:
                score += amount;
                message = "BONUS +" + amount;
                break;
        }

        score += 18 + wave * 3;
        if (TrialHud.Singleton != null)
        {
            TrialHud.Singleton.ShowFeedback(message);
        }

        RefreshHud();
    }

    private void AssignLocalPlayer(Player player)
    {
        if (player == null)
        {
            return;
        }

        localPlayer = player;
        FpsPolishController.AttachToLocalPlayer(player.gameObject);
        playerAbility = TrialPlayerAbility.Attach(player.gameObject);

        if (!runHasStarted)
        {
            StartRun();
        }
    }

    private void TryFindLocalPlayer()
    {
        Player[] players = FindObjectsOfType<Player>();
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != null && players[i].IsLocalPlayer)
            {
                AssignLocalPlayer(players[i]);
                return;
            }
        }
    }

    private void StartRun()
    {
        if (localPlayer == null)
        {
            return;
        }

        ClearRuntimeActors();
        score = 0;
        wave = 1;
        combo = 0;
        maxCombo = 0;
        targetsDestroyed = 0;
        spectersDestroyed = 0;
        eliteSpectersDestroyed = 0;
        pickupsCollected = 0;
        damageTaken = 0;
        shotsFired = 0;
        shotsHit = 0;
        timeLeft = runDuration;
        running = true;
        runHasStarted = true;
        waveTransitioning = false;
        SetPaused(false);
        SpawnArenaDecor();
        SpawnWave();

        if (TrialHud.Singleton != null)
        {
            TrialHud.Singleton.ShowFeedback("TRIAL START");
        }
    }

    private void RestartRun()
    {
        if (localPlayer == null)
        {
            TryFindLocalPlayer();
        }

        runHasStarted = false;
        running = false;
        SetPaused(false);
        StopAllCoroutines();
        ClearRuntimeActors();

        if (localPlayer != null)
        {
            StartRun();
        }
        else if (TrialHud.Singleton != null)
        {
            TrialHud.Singleton.RefreshWaitingState();
            TrialHud.Singleton.ShowFeedback("START LOCAL TRIAL FIRST");
        }
    }

    private IEnumerator AdvanceWave()
    {
        waveTransitioning = true;

        int bonus = 12 + wave * 2;
        score += bonus;
        timeLeft += Mathf.Min(16f, 7f + wave * 1.5f);

        if (TrialHud.Singleton != null)
        {
            TrialHud.Singleton.ShowFeedback("WAVE CLEAR +" + bonus);
        }

        yield return new WaitForSeconds(1.1f);

        wave++;
        SpawnWave();
        waveTransitioning = false;
    }

    private void EndRun()
    {
        running = false;
        timeLeft = 0f;
        combo = 0;
        SetPaused(false);
        ClearRuntimeActors();

        string awards = BuildAwardSummary(out int awardBonus);
        score += awardBonus;
        bool newBest = score > highScore;
        if (score > highScore)
        {
            highScore = score;
            PlayerPrefs.SetInt(HighScoreKey, highScore);
            PlayerPrefs.Save();
        }

        if (TrialHud.Singleton != null)
        {
            TrialHud.Singleton.ShowFeedback("RUN COMPLETE  SCORE " + score);
            TrialHud.Singleton.ShowResult(
                score,
                highScore,
                wave,
                maxCombo,
                targetsDestroyed,
                spectersDestroyed,
                pickupsCollected,
                GetAccuracy(),
                CalculateRank(),
                newBest,
                awards);
        }

        RefreshHud();
    }

    private void SpawnWave()
    {
        activeTargets.Clear();
        Vector3 focusPoint = GetFocusPoint();
        int count = Mathf.Clamp(baseTargetsPerWave + wave, baseTargetsPerWave, maxTargetsPerWave);
        waveTargetCount = count;

        for (int i = 0; i < count; i++)
        {
            Vector3 position = PickTargetPosition(i, count);
            GameObject targetObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            targetObject.name = "Ink Seal Target W" + wave + "-" + (i + 1);
            targetObject.transform.position = position;
            targetObject.transform.localScale = Vector3.one * Random.Range(1.15f, 1.55f);
            targetObject.transform.localScale = new Vector3(targetObject.transform.localScale.x, 0.12f, targetObject.transform.localScale.z);

            TrialTarget target = targetObject.AddComponent<TrialTarget>();
            float pressure = 1f + Mathf.Clamp01(GetAccuracy() - 0.55f) * 0.35f + Mathf.Min(maxCombo, 20) * 0.01f;
            int health = Mathf.RoundToInt((10 + wave * 3 + (i % 3) * 2) * pressure);
            int value = Mathf.RoundToInt((60 + wave * 12 + i * 3) * pressure);
            bool moving = wave >= 2 && (i % 2 == 0 || pressure > 1.18f);
            target.Initialize(wave, health, value, moving, focusPoint);
            activeTargets.Add(target);
        }

        SpawnSpecters();
        SpawnPickups();
        RefreshHud();
    }

    private void SpawnSpecters()
    {
        if (localPlayer == null || wave < 2)
        {
            return;
        }

        int count = Mathf.Clamp(1 + wave / 3, 1, 4);
        for (int i = 0; i < count; i++)
        {
            Vector3 position = PickArenaPosition(9f + i * 2.5f, 18f + wave);
            position.y = SampleGroundY(position) + 1.2f;

            GameObject specterObject = new GameObject("Ink Specter W" + wave + "-" + (i + 1));
            specterObject.transform.position = position;
            TrialSpecter specter = specterObject.AddComponent<TrialSpecter>();
            bool elite = wave % 3 == 0 && i == 0;
            specter.Initialize(localPlayer, wave, elite);
            activeSpecters.Add(specter);

            if (elite && TrialHud.Singleton != null)
            {
                TrialHud.Singleton.ShowFeedback("ELITE SPECTER");
            }
        }
    }

    private void SpawnPickups()
    {
        int count = Mathf.Clamp(1 + wave / 2, 1, 4);
        for (int i = 0; i < count; i++)
        {
            TrialPickupKind kind = (TrialPickupKind)((wave + i) % 4);
            Vector3 position = PickArenaPosition(7f + i * 2f, 16f);
            position.y = SampleGroundY(position) + 1.15f;

            GameObject pickupObject = new GameObject("Trial Pickup");
            pickupObject.transform.position = position;
            TrialPickup pickup = pickupObject.AddComponent<TrialPickup>();
            pickup.Initialize(kind, ResolvePickupAmount(kind));
            activePickups.Add(pickup);
        }
    }

    private Vector3 PickTargetPosition(int index, int count)
    {
        Vector3 center = localPlayer != null ? localPlayer.transform.position : Vector3.zero;
        float angle = ((float)index / Mathf.Max(1, count)) * Mathf.PI * 2f + wave * 0.37f;
        float radius = Random.Range(13f, 25f) + Mathf.Min(wave * 0.7f, 8f);
        Vector3 position = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        position.y = SampleGroundY(position) + Random.Range(1.6f, 4.2f);
        return position;
    }

    private Vector3 PickArenaPosition(float minRadius, float maxRadius)
    {
        Vector3 center = localPlayer != null ? localPlayer.transform.position : Vector3.zero;
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float radius = Random.Range(minRadius, maxRadius);
        return center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
    }

    private Vector3 GetFocusPoint()
    {
        if (localPlayer == null)
        {
            return Vector3.up * 1.6f;
        }

        return localPlayer.transform.position + Vector3.up * 1.4f;
    }

    private Vector3 GetSpawnPositionNearTrial(Vector3 fallback)
    {
        Vector3 center = fallback;
        if (localPlayer != null)
        {
            center = localPlayer.transform.position;
        }

        Vector3 spawn = center + Vector3.up * 8f;
        spawn.y = SampleGroundY(spawn) + 2.4f;
        return spawn;
    }

    private float SampleGroundY(Vector3 position)
    {
        Vector3 origin = new Vector3(position.x, 90f, position.z);
        RaycastHit hit;
        if (Physics.Raycast(origin, Vector3.down, out hit, 180f, ~0, QueryTriggerInteraction.Ignore))
        {
            return hit.point.y;
        }

        return localPlayer != null ? localPlayer.transform.position.y : 1.5f;
    }

    private int ResolvePickupAmount(TrialPickupKind kind)
    {
        switch (kind)
        {
            case TrialPickupKind.Health:
                return 28 + wave * 2;
            case TrialPickupKind.Ammo:
                return 1;
            case TrialPickupKind.Time:
                return 8 + Mathf.Min(wave, 8);
            default:
                return 120 + wave * 18;
        }
    }

    private void ClearRuntimeActors()
    {
        for (int i = 0; i < activeTargets.Count; i++)
        {
            if (activeTargets[i] != null)
            {
                Destroy(activeTargets[i].gameObject);
            }
        }

        activeTargets.Clear();

        for (int i = 0; i < activePickups.Count; i++)
        {
            if (activePickups[i] != null)
            {
                Destroy(activePickups[i].gameObject);
            }
        }

        activePickups.Clear();

        for (int i = 0; i < activeSpecters.Count; i++)
        {
            if (activeSpecters[i] != null)
            {
                Destroy(activeSpecters[i].gameObject);
            }
        }

        activeSpecters.Clear();

        for (int i = 0; i < activeDecor.Count; i++)
        {
            if (activeDecor[i] != null)
            {
                Destroy(activeDecor[i]);
            }
        }

        activeDecor.Clear();
    }

    private void RefreshHud()
    {
        if (TrialHud.Singleton == null)
        {
            return;
        }

        float accuracy = shotsFired <= 0 ? 1f : Mathf.Clamp01((float)shotsHit / shotsFired);
        TrialHud.Singleton.RefreshChallenge(
            score,
            highScore,
            wave,
            timeLeft,
            activeTargets.Count,
            waveTargetCount,
            activeSpecters.Count,
            activePickups.Count,
            combo,
            accuracy,
            running);

        if (localPlayer != null)
        {
            if (playerAbility == null)
            {
                playerAbility = localPlayer.GetComponent<TrialPlayerAbility>();
            }

            float dashReady01 = playerAbility != null ? playerAbility.GetDashReady01() : 1f;
            bool dashReady = playerAbility == null || playerAbility.IsDashReady();
            TrialHud.Singleton.RefreshVitals(localPlayer.GetHealth(), localPlayer.GetMaxHealth(), dashReady01, dashReady);
        }

        TrialHud.Singleton.RefreshMissions(BuildMissionSummary());
    }

    private void TogglePause()
    {
        if (!running)
        {
            return;
        }

        SetPaused(!paused);
    }

    private void SetPaused(bool value)
    {
        paused = value;
        Time.timeScale = paused ? 0f : 1f;
        if (TrialHud.Singleton != null)
        {
            TrialHud.Singleton.ShowPause(paused);
            if (!paused && !running)
            {
                TrialHud.Singleton.HideOverlay();
            }
        }
    }

    private float GetAccuracy()
    {
        return shotsFired <= 0 ? 1f : Mathf.Clamp01((float)shotsHit / shotsFired);
    }

    private string CalculateRank()
    {
        float accuracy = GetAccuracy();
        int performance = score + maxCombo * 45 + targetsDestroyed * 25 + spectersDestroyed * 80 + pickupsCollected * 15;
        if (performance >= 5200 && accuracy >= 0.68f)
        {
            return "S";
        }

        if (performance >= 3600 && accuracy >= 0.52f)
        {
            return "A";
        }

        if (performance >= 2300)
        {
            return "B";
        }

        if (performance >= 1200)
        {
            return "C";
        }

        return "D";
    }

    private string BuildMissionSummary()
    {
        string accuracy = GetAccuracy() >= 0.6f ? "[OK] Accuracy 60%" : "[  ] Accuracy 60%";
        string comboGoal = maxCombo >= 8 ? "[OK] Combo x8" : "[  ] Combo x8";
        string pickupGoal = pickupsCollected >= 3 ? "[OK] 3 Pickups" : "[  ] 3 Pickups";
        string eliteGoal = eliteSpectersDestroyed >= 1 ? "[OK] Elite Down" : "[  ] Elite Down";
        return "Missions: " + accuracy + "   " + comboGoal + "   " + pickupGoal + "   " + eliteGoal;
    }

    private string BuildAwardSummary(out int awardBonus)
    {
        awardBonus = 0;
        List<string> awards = new List<string>();

        if (GetAccuracy() >= 0.6f)
        {
            awardBonus += 350;
            awards.Add("Sharpshooter");
        }

        if (maxCombo >= 8)
        {
            awardBonus += 300;
            awards.Add("Ink Chain");
        }

        if (pickupsCollected >= 3)
        {
            awardBonus += 220;
            awards.Add("Field Runner");
        }

        if (eliteSpectersDestroyed >= 1)
        {
            awardBonus += 500;
            awards.Add("Exorcist");
        }

        if (damageTaken <= 20)
        {
            awardBonus += 260;
            awards.Add("Untouchable");
        }

        if (awards.Count == 0)
        {
            return "Awards: None";
        }

        return "Awards +" + awardBonus + ": " + string.Join(", ", awards.ToArray());
    }

    private void UpdateThreatWarning()
    {
        if (localPlayer == null || TrialHud.Singleton == null || activeSpecters.Count == 0)
        {
            return;
        }

        float nearest = float.MaxValue;
        for (int i = 0; i < activeSpecters.Count; i++)
        {
            if (activeSpecters[i] == null)
            {
                continue;
            }

            float distance = Vector3.Distance(localPlayer.transform.position, activeSpecters[i].transform.position);
            if (distance < nearest)
            {
                nearest = distance;
            }
        }

        if (nearest <= 5.5f)
        {
            TrialHud.Singleton.ShowThreatWarning("SPECTER CLOSE  " + Mathf.CeilToInt(nearest) + "m");
        }
    }

    private void SpawnArenaDecor()
    {
        if (localPlayer == null)
        {
            return;
        }

        Vector3 center = localPlayer.transform.position;
        for (int i = 0; i < 10; i++)
        {
            float angle = i / 10f * Mathf.PI * 2f;
            Vector3 position = center + new Vector3(Mathf.Cos(angle) * 29f, 0f, Mathf.Sin(angle) * 29f);
            position.y = SampleGroundY(position) + 0.65f;

            GameObject beacon = new GameObject("Ink Trial Beacon");
            beacon.transform.position = position;

            GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = "Beacon Pillar";
            pillar.transform.SetParent(beacon.transform, false);
            pillar.transform.localScale = new Vector3(0.28f, 0.65f, 0.28f);
            pillar.transform.localPosition = Vector3.zero;
            Collider pillarCollider = pillar.GetComponent<Collider>();
            if (pillarCollider != null)
            {
                pillarCollider.enabled = false;
            }

            Renderer renderer = pillar.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = TrialEffects.CreateLitMaterial(new Color(0.07f, 0.09f, 0.1f), 0.15f);
            }

            Light light = beacon.AddComponent<Light>();
            light.color = i % 2 == 0 ? new Color(0.35f, 0.85f, 1f) : new Color(1f, 0.72f, 0.28f);
            light.range = 6.5f;
            light.intensity = 1.15f;

            activeDecor.Add(beacon);
        }
    }
}
