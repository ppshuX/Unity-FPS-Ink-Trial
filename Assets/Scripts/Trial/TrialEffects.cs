using System.Collections;
using UnityEngine;

public class TrialEffects : MonoBehaviour
{
    private const string EffectsName = "[Trial] Effects";

    private static TrialEffects instance;
    private static Material trailMaterial;
    private static Material additiveMaterial;

    public static void SpawnBulletTrail(Vector3 start, Vector3 end, Color color)
    {
        EnsureInstance();
        instance.StartCoroutine(instance.BulletTrailCoroutine(start, end, color));
    }

    public static void SpawnBurst(Vector3 position, Color color, int count, float speed, float size)
    {
        GameObject burst = new GameObject("Trial Burst");
        burst.transform.position = position;
        ParticleSystem ps = burst.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = ps.main;
        main.startLifetime = 0.45f;
        main.startSpeed = speed;
        main.startSize = size;
        main.startColor = color;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Clamp(count, 1, 120)) });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.2f;

        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = GetAdditiveMaterial(color);

        ps.Play();
        Destroy(burst, 1.4f);
    }

    public static Material CreateLitMaterial(Color color, float emission)
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

    public static void SetMaterialColor(Material material, Color color, float emission)
    {
        if (material == null)
        {
            return;
        }

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

    private static void EnsureInstance()
    {
        if (instance != null)
        {
            return;
        }

        GameObject go = new GameObject(EffectsName);
        DontDestroyOnLoad(go);
        instance = go.AddComponent<TrialEffects>();
    }

    private IEnumerator BulletTrailCoroutine(Vector3 start, Vector3 end, Color color)
    {
        GameObject trail = new GameObject("Bullet Trail");
        LineRenderer line = trail.AddComponent<LineRenderer>();
        line.sharedMaterial = GetTrailMaterial(color);
        line.positionCount = 2;
        line.useWorldSpace = true;
        line.widthMultiplier = 0.035f;
        line.numCapVertices = 4;
        line.SetPosition(0, start);
        line.SetPosition(1, end);

        float timer = 0.09f;
        while (timer > 0f)
        {
            timer -= Time.deltaTime;
            float a = Mathf.Clamp01(timer / 0.09f);
            line.startColor = new Color(color.r, color.g, color.b, a);
            line.endColor = new Color(color.r, color.g, color.b, a * 0.15f);
            line.widthMultiplier = Mathf.Lerp(0.005f, 0.035f, a);
            yield return null;
        }

        Destroy(trail);
    }

    private static Material GetTrailMaterial(Color color)
    {
        if (trailMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            trailMaterial = new Material(shader);
        }

        trailMaterial.color = color;
        return trailMaterial;
    }

    private static Material GetAdditiveMaterial(Color color)
    {
        if (additiveMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            additiveMaterial = new Material(shader);
        }

        additiveMaterial.color = color;
        return additiveMaterial;
    }
}
