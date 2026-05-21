using UnityEngine;

public class TrialSpecter : MonoBehaviour
{
    public bool IsAlive { get; private set; }

    private Player targetPlayer;
    private int health;
    private int scoreValue;
    private float speed;
    private float attackCooldown;
    private float wobbleSeed;
    private bool isElite;
    private Color color;
    private Material material;
    private Renderer[] renderers;
    private Collider hitCollider;

    public void Initialize(Player target, int wave)
    {
        Initialize(target, wave, false);
    }

    public void Initialize(Player target, int wave, bool elite)
    {
        targetPlayer = target;
        isElite = elite;
        health = 18 + wave * 7;
        scoreValue = 90 + wave * 18;
        speed = 2.3f + wave * 0.22f;
        if (isElite)
        {
            health = Mathf.RoundToInt(health * 2.65f);
            scoreValue = Mathf.RoundToInt(scoreValue * 2.4f);
            speed *= 0.82f;
        }
        wobbleSeed = Random.Range(0f, 100f);
        color = isElite
            ? new Color(1f, 0.34f, 0.18f)
            : Color.Lerp(new Color(0.12f, 0.72f, 0.95f), new Color(0.9f, 0.18f, 0.96f), Mathf.PingPong(wave * 0.17f, 1f));
        IsAlive = true;

        BuildVisuals();
    }

    public bool ApplyDamage(int damage)
    {
        if (!IsAlive)
        {
            return false;
        }

        health -= Mathf.Max(1, damage);
        TrialEffects.SpawnBurst(transform.position + Vector3.up * 0.8f, color, 12, 3.8f, 0.055f);

        if (health <= 0)
        {
            Die();
            return true;
        }

        TrialEffects.SetMaterialColor(material, Color.Lerp(Color.white, color, 0.55f), 1.7f);
        return false;
    }

    public int GetScoreValue()
    {
        return scoreValue;
    }

    public bool IsElite()
    {
        return isElite;
    }

    private void Update()
    {
        if (!IsAlive || targetPlayer == null || targetPlayer.IsDead())
        {
            return;
        }

        Vector3 target = targetPlayer.transform.position + Vector3.up * 1.2f;
        Vector3 toTarget = target - transform.position;
        float distance = toTarget.magnitude;

        if (distance > 0.1f)
        {
            Vector3 move = toTarget.normalized * speed * Time.deltaTime;
            transform.position += move;
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(toTarget.normalized, Vector3.up),
                Time.deltaTime * 7f);
        }

        float hover = Mathf.Sin(Time.time * 3f + wobbleSeed) * 0.015f;
        transform.position += Vector3.up * hover;

        attackCooldown -= Time.deltaTime;
        if (distance < 2.2f && attackCooldown <= 0f)
        {
            attackCooldown = 1.15f;
            AttackPlayer();
        }

        float pulse = 1.05f + Mathf.Sin(Time.time * 4f + wobbleSeed) * 0.28f;
        TrialEffects.SetMaterialColor(material, color, pulse);
    }

    private void AttackPlayer()
    {
        if (targetPlayer == null || !targetPlayer.IsServer)
        {
            return;
        }

        int damage = 8;
        if (isElite)
        {
            damage = 14;
        }
        targetPlayer.TakeDamage(damage);

        if (TrialChallengeDirector.Singleton != null)
        {
            TrialChallengeDirector.Singleton.RegisterPlayerDamaged(damage);
        }

        if (TrialHud.Singleton != null)
        {
            TrialHud.Singleton.ShowDamageFlash();
            TrialHud.Singleton.ShowFeedback("HIT -" + damage);
        }

        TrialEffects.SpawnBurst(targetPlayer.transform.position + Vector3.up * 1.4f, new Color(1f, 0.18f, 0.12f), 18, 2.8f, 0.075f);
    }

    private void Die()
    {
        IsAlive = false;
        if (hitCollider != null)
        {
            hitCollider.enabled = false;
        }

        TrialEffects.SpawnBurst(transform.position + Vector3.up * 0.7f, color, 42, 5.2f, 0.09f);
        Destroy(gameObject, 0.05f);
    }

    private void BuildVisuals()
    {
        gameObject.name = isElite ? "Elite Ink Specter" : "Ink Specter";

        CapsuleCollider capsule = gameObject.AddComponent<CapsuleCollider>();
        capsule.height = 1.8f;
        capsule.radius = 0.45f;
        capsule.center = new Vector3(0f, 0.8f, 0f);
        if (isElite)
        {
            capsule.height = 2.5f;
            capsule.radius = 0.65f;
            capsule.center = new Vector3(0f, 1.08f, 0f);
        }
        hitCollider = capsule;

        material = TrialEffects.CreateLitMaterial(color, 1.2f);

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(transform, false);
        body.transform.localPosition = new Vector3(0f, 0.8f, 0f);
        body.transform.localScale = new Vector3(0.62f, 0.82f, 0.62f);
        if (isElite)
        {
            body.transform.localPosition = new Vector3(0f, 1.08f, 0f);
            body.transform.localScale = new Vector3(0.9f, 1.2f, 0.9f);
        }
        DisableAndDestroyCollider(body);

        GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        core.name = "Core";
        core.transform.SetParent(transform, false);
        core.transform.localPosition = new Vector3(0f, 1.32f, 0.18f);
        core.transform.localScale = Vector3.one * 0.28f;
        if (isElite)
        {
            core.transform.localPosition = new Vector3(0f, 1.86f, 0.26f);
            core.transform.localScale = Vector3.one * 0.42f;
        }
        DisableAndDestroyCollider(core);

        renderers = GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].sharedMaterial = material;
        }

        Light light = gameObject.AddComponent<Light>();
        light.color = color;
        light.range = isElite ? 8f : 5f;
        light.intensity = isElite ? 1.8f : 1.1f;
    }

    private static void DisableAndDestroyCollider(GameObject obj)
    {
        Collider col = obj.GetComponent<Collider>();
        if (col == null)
        {
            return;
        }

        col.enabled = false;
        Destroy(col);
    }
}
