using UnityEngine;

[DisallowMultipleComponent]
public class TrialPlayerAbility : MonoBehaviour
{
    private const float DashCooldown = 2.4f;

    private Rigidbody rb;
    private Camera fpsCamera;
    private float dashCooldown;
    private float dashFlashTimer;
    private Vector3 lastDashDirection = Vector3.forward;

    public static TrialPlayerAbility Attach(GameObject playerObject)
    {
        if (playerObject == null)
        {
            return null;
        }

        TrialPlayerAbility ability = playerObject.GetComponent<TrialPlayerAbility>();
        if (ability == null)
        {
            ability = playerObject.AddComponent<TrialPlayerAbility>();
        }

        return ability;
    }

    public float GetDashReady01()
    {
        return Mathf.Clamp01(1f - dashCooldown / DashCooldown);
    }

    public bool IsDashReady()
    {
        return dashCooldown <= 0f;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        fpsCamera = GetComponentInChildren<Camera>(true);
    }

    private void Update()
    {
        if (TrialChallengeDirector.Singleton != null && TrialChallengeDirector.Singleton.IsPaused())
        {
            return;
        }

        dashCooldown -= Time.deltaTime;
        dashFlashTimer -= Time.deltaTime;

        if (Input.GetKeyDown(KeyCode.LeftControl) || Input.GetMouseButtonDown(2))
        {
            TryDash();
        }
    }

    private void LateUpdate()
    {
        if (fpsCamera == null || dashFlashTimer <= 0f)
        {
            return;
        }

        float kick = Mathf.Sin((dashFlashTimer / 0.22f) * Mathf.PI) * 0.035f;
        fpsCamera.transform.position -= lastDashDirection * kick;
    }

    private void TryDash()
    {
        if (rb == null || dashCooldown > 0f)
        {
            return;
        }

        Vector3 input = transform.right * Input.GetAxisRaw("Horizontal") + transform.forward * Input.GetAxisRaw("Vertical");
        if (input.sqrMagnitude < 0.01f)
        {
            input = transform.forward;
        }

        lastDashDirection = input.normalized;
        rb.AddForce(lastDashDirection * 11.5f, ForceMode.VelocityChange);
        dashCooldown = DashCooldown;
        dashFlashTimer = 0.22f;

        TrialEffects.SpawnBurst(transform.position + Vector3.up * 0.35f, new Color(0.45f, 0.92f, 1f), 26, 4.6f, 0.07f);

        if (TrialHud.Singleton != null)
        {
            TrialHud.Singleton.ShowFeedback("DASH");
        }
    }
}
