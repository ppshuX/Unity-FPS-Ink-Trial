using UnityEngine;

public enum TrialPickupKind
{
    Health,
    Ammo,
    Time,
    Score,
}

public class TrialPickup : MonoBehaviour
{
    private TrialPickupKind kind;
    private int amount;
    private Color color;
    private float seed;
    private Vector3 basePosition;
    private bool collected;
    private Material material;

    public void Initialize(TrialPickupKind pickupKind, int pickupAmount)
    {
        kind = pickupKind;
        amount = Mathf.Max(1, pickupAmount);
        seed = Random.Range(0f, 100f);
        basePosition = transform.position;
        color = ResolveColor(kind);

        gameObject.name = "Trial Pickup - " + kind;
        BuildVisuals();
    }

    private void Update()
    {
        transform.Rotate(Vector3.up, 82f * Time.deltaTime, Space.World);
        transform.position = basePosition + Vector3.up * (Mathf.Sin(Time.time * 2.6f + seed) * 0.22f);

        if (material != null)
        {
            float pulse = 0.85f + Mathf.Sin(Time.time * 5f + seed) * 0.25f;
            TrialEffects.SetMaterialColor(material, color, 0.8f + pulse);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (collected)
        {
            return;
        }

        Player player = other.GetComponentInParent<Player>();
        if (player == null || !player.IsLocalPlayer)
        {
            return;
        }

        collected = true;
        if (TrialChallengeDirector.Singleton != null)
        {
            TrialChallengeDirector.Singleton.CollectPickup(this, player, kind, amount);
        }

        TrialEffects.SpawnBurst(transform.position, color, 24, 4.5f, 0.08f);
        Destroy(gameObject);
    }

    private void BuildVisuals()
    {
        SphereCollider trigger = gameObject.AddComponent<SphereCollider>();
        trigger.radius = 1.05f;
        trigger.isTrigger = true;

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        body.name = "Core";
        body.transform.SetParent(transform, false);
        body.transform.localScale = Vector3.one * 0.55f;
        DisableAndDestroyCollider(body);

        material = TrialEffects.CreateLitMaterial(color, 1.1f);
        Renderer bodyRenderer = body.GetComponent<Renderer>();
        if (bodyRenderer != null)
        {
            bodyRenderer.sharedMaterial = material;
        }

        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        if (ring != null)
        {
            ring.name = "Ring";
            ring.transform.SetParent(transform, false);
            ring.transform.localScale = new Vector3(0.9f, 0.035f, 0.9f);
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            DisableAndDestroyCollider(ring);
            Renderer ringRenderer = ring.GetComponent<Renderer>();
            if (ringRenderer != null)
            {
                ringRenderer.sharedMaterial = material;
            }
        }

        Light light = gameObject.AddComponent<Light>();
        light.color = color;
        light.range = 4f;
        light.intensity = 1.2f;
    }

    private static Color ResolveColor(TrialPickupKind pickupKind)
    {
        switch (pickupKind)
        {
            case TrialPickupKind.Health:
                return new Color(0.35f, 1f, 0.48f);
            case TrialPickupKind.Ammo:
                return new Color(0.35f, 0.78f, 1f);
            case TrialPickupKind.Time:
                return new Color(1f, 0.78f, 0.28f);
            default:
                return new Color(1f, 0.42f, 0.9f);
        }
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
