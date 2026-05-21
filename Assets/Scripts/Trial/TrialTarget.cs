using System.Collections;
using UnityEngine;

public class TrialTarget : MonoBehaviour
{
    public int ScoreValue { get; private set; }
    public bool IsAlive { get; private set; }

    private int maxHealth;
    private int health;
    private bool isMoving;
    private float seed;
    private float moveAmplitude;
    private float moveSpeed;
    private float spinSpeed;
    private float hitFlashTimer;
    private Vector3 basePosition;
    private Vector3 startScale;
    private Vector3 moveAxis;
    private Renderer[] renderers;
    private Collider[] colliders;
    private Material bodyMaterial;
    private Material ringMaterial;
    private Color bodyColor;
    private Color ringColor;

    public void Initialize(int wave, int healthValue, int scoreValue, bool moving, Vector3 focusPoint)
    {
        maxHealth = Mathf.Max(1, healthValue);
        health = maxHealth;
        ScoreValue = Mathf.Max(1, scoreValue);
        IsAlive = true;
        isMoving = moving;
        seed = Random.Range(0f, 100f);
        moveAmplitude = moving ? Random.Range(1.2f, 2.8f) : 0f;
        moveSpeed = Random.Range(0.8f, 1.45f) + wave * 0.03f;
        spinSpeed = Random.Range(22f, 44f) + wave * 2f;
        basePosition = transform.position;
        startScale = transform.localScale;

        Vector3 toFocus = focusPoint - transform.position;
        if (toFocus.sqrMagnitude < 0.01f)
        {
            toFocus = Vector3.forward;
        }

        transform.rotation = Quaternion.FromToRotation(Vector3.up, toFocus.normalized);
        moveAxis = Vector3.Cross(Vector3.up, toFocus.normalized).normalized;
        if (moveAxis.sqrMagnitude < 0.01f)
        {
            moveAxis = Vector3.right;
        }

        BuildVisuals(wave);
        ApplyHealthTint();
    }

    public float GetPrecision(Vector3 hitPoint)
    {
        Vector3 local = transform.InverseTransformPoint(hitPoint);
        float radius01 = Mathf.Clamp01(new Vector2(local.x, local.z).magnitude / 0.5f);
        return 1f - radius01;
    }

    public bool ApplyDamage(int damage, bool critical)
    {
        if (!IsAlive)
        {
            return false;
        }

        health -= Mathf.Max(1, damage);
        hitFlashTimer = critical ? 0.16f : 0.09f;

        if (health <= 0)
        {
            health = 0;
            StartCoroutine(Disappear());
            return true;
        }

        ApplyHealthTint();
        return false;
    }

    private void Update()
    {
        if (!IsAlive)
        {
            return;
        }

        float bob = Mathf.Sin((Time.time + seed) * 2.2f) * 0.22f;
        Vector3 strafe = isMoving ? moveAxis * (Mathf.Sin((Time.time + seed) * moveSpeed) * moveAmplitude) : Vector3.zero;
        transform.position = basePosition + Vector3.up * bob + strafe;
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.Self);

        if (hitFlashTimer > 0f)
        {
            hitFlashTimer -= Time.deltaTime;
            SetTint(Color.white, 1.8f);
        }
        else
        {
            ApplyHealthTint();
        }
    }

    private void BuildVisuals(int wave)
    {
        bodyColor = Color.Lerp(new Color(0.05f, 0.07f, 0.075f), new Color(0.16f, 0.21f, 0.23f), Mathf.Clamp01(wave / 8f));
        ringColor = Color.Lerp(new Color(0.9f, 0.64f, 0.22f), new Color(0.45f, 0.9f, 0.95f), Mathf.PingPong(wave * 0.18f, 1f));

        bodyMaterial = CreateMaterial(bodyColor, 0.25f);
        ringMaterial = CreateMaterial(ringColor, 0.75f);

        Renderer rootRenderer = GetComponent<Renderer>();
        if (rootRenderer != null)
        {
            rootRenderer.sharedMaterial = bodyMaterial;
        }

        AddDisc("Outer Ring", 1.1f, 0.012f, ringMaterial, 0.032f);
        AddDisc("Inner Seal", 0.46f, 0.016f, ringMaterial, 0.048f);
        AddBar("Seal Bar H", new Vector3(0.72f, 0.018f, 0.055f), ringMaterial, 0.065f);
        AddBar("Seal Bar V", new Vector3(0.055f, 0.018f, 0.72f), ringMaterial, 0.068f);

        renderers = GetComponentsInChildren<Renderer>();
        colliders = GetComponentsInChildren<Collider>();
    }

    private void AddDisc(string name, float radiusScale, float thickness, Material material, float localOffset)
    {
        GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = name;
        disc.transform.SetParent(transform, false);
        disc.transform.localPosition = Vector3.up * localOffset;
        disc.transform.localRotation = Quaternion.identity;
        disc.transform.localScale = new Vector3(radiusScale, thickness, radiusScale);

        Collider col = disc.GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = false;
            Destroy(col);
        }

        Renderer renderer = disc.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }
    }

    private void AddBar(string name, Vector3 localScale, Material material, float localOffset)
    {
        GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bar.name = name;
        bar.transform.SetParent(transform, false);
        bar.transform.localPosition = Vector3.up * localOffset;
        bar.transform.localRotation = Quaternion.identity;
        bar.transform.localScale = localScale;

        Collider col = bar.GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = false;
            Destroy(col);
        }

        Renderer renderer = bar.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }
    }

    private void ApplyHealthTint()
    {
        float health01 = maxHealth <= 0 ? 0f : Mathf.Clamp01((float)health / maxHealth);
        Color tint = Color.Lerp(new Color(0.65f, 0.12f, 0.08f), bodyColor, health01);
        SetTint(tint, 0.35f + (1f - health01) * 0.8f);
    }

    private void SetTint(Color color, float emission)
    {
        if (bodyMaterial != null)
        {
            SetMaterialColor(bodyMaterial, color, emission);
        }

        if (ringMaterial != null)
        {
            SetMaterialColor(ringMaterial, ringColor, emission + 0.45f);
        }
    }

    private IEnumerator Disappear()
    {
        IsAlive = false;

        if (colliders != null)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = false;
                }
            }
        }

        SpawnBurst();

        float t = 0f;
        Vector3 from = transform.localScale;
        while (t < 0.32f)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / 0.32f);
            transform.localScale = Vector3.Lerp(from * 1.15f, startScale * 0.05f, p);
            SetTint(Color.Lerp(Color.white, ringColor, p), 1.4f);
            yield return null;
        }

        Destroy(gameObject);
    }

    private void SpawnBurst()
    {
        GameObject burst = new GameObject("Ink Target Burst");
        burst.transform.position = transform.position;
        ParticleSystem ps = burst.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = ps.main;
        main.startLifetime = 0.42f;
        main.startSpeed = 5.8f;
        main.startSize = 0.085f;
        main.startColor = ringColor;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 28) });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.35f;

        ps.Play();
        Destroy(burst, 1.2f);
    }

    private static Material CreateMaterial(Color color, float emission)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader);
        SetMaterialColor(material, color, emission);
        return material;
    }

    private static void SetMaterialColor(Material material, Color color, float emission)
    {
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * emission);
        }
    }
}
