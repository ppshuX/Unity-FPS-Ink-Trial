using UnityEngine;

[DisallowMultipleComponent]
public class FpsPolishController : MonoBehaviour
{
    private Camera fpsCamera;
    private Transform weaponHolder;
    private Rigidbody rb;
    private Vector3 cameraBaseLocalPosition;
    private Vector3 weaponBaseLocalPosition;
    private Quaternion weaponBaseLocalRotation;
    private Vector3 previousPosition;
    private float baseFieldOfView;
    private float bobTimer;

    public static void AttachToLocalPlayer(GameObject playerObject)
    {
        if (playerObject == null || playerObject.GetComponent<FpsPolishController>() != null)
        {
            return;
        }

        playerObject.AddComponent<FpsPolishController>();
    }

    private void Awake()
    {
        fpsCamera = GetComponentInChildren<Camera>(true);
        rb = GetComponent<Rigidbody>();
        weaponHolder = FindChildByName(transform, "WeaponHolder");
        previousPosition = transform.position;

        if (fpsCamera != null)
        {
            cameraBaseLocalPosition = fpsCamera.transform.localPosition;
            baseFieldOfView = fpsCamera.fieldOfView;
        }

        if (weaponHolder != null)
        {
            weaponBaseLocalPosition = weaponHolder.localPosition;
            weaponBaseLocalRotation = weaponHolder.localRotation;
        }
    }

    private void Update()
    {
        HandleCursor();

        if (fpsCamera == null || !fpsCamera.enabled)
        {
            previousPosition = transform.position;
            return;
        }

        float speed = ((transform.position - previousPosition).magnitude / Mathf.Max(Time.deltaTime, 0.0001f));
        previousPosition = transform.position;

        UpdateFieldOfView(speed);
        UpdateCameraBob(speed);
        UpdateWeaponSway(speed);
    }

    private void HandleCursor()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else if (Input.GetMouseButtonDown(0))
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void UpdateFieldOfView(float speed)
    {
        bool aiming = Input.GetMouseButton(1);
        bool sprinting = Input.GetKey(KeyCode.LeftShift) && speed > 4.8f;

        float targetFov = baseFieldOfView;
        if (aiming)
        {
            targetFov -= 10f;
        }
        else if (sprinting)
        {
            targetFov += 7f;
        }

        fpsCamera.fieldOfView = Mathf.Lerp(fpsCamera.fieldOfView, targetFov, Time.deltaTime * 9f);
    }

    private void UpdateCameraBob(float speed)
    {
        bool grounded = rb == null || Mathf.Abs(rb.velocity.y) < 1.2f;
        float move01 = Mathf.Clamp01(speed / 7.5f);
        if (move01 > 0.08f && grounded)
        {
            bobTimer += Time.deltaTime * Mathf.Lerp(6f, 11f, move01);
        }
        else
        {
            bobTimer = Mathf.Lerp(bobTimer, 0f, Time.deltaTime * 4f);
        }

        Vector3 bob = new Vector3(
            Mathf.Sin(bobTimer * 0.5f) * 0.035f,
            Mathf.Sin(bobTimer) * 0.045f,
            0f) * move01;
        fpsCamera.transform.localPosition = Vector3.Lerp(
            fpsCamera.transform.localPosition,
            cameraBaseLocalPosition + bob,
            Time.deltaTime * 10f);
    }

    private void UpdateWeaponSway(float speed)
    {
        if (weaponHolder == null)
        {
            return;
        }

        float mouseX = Mathf.Clamp(Input.GetAxisRaw("Mouse X"), -3f, 3f);
        float mouseY = Mathf.Clamp(Input.GetAxisRaw("Mouse Y"), -3f, 3f);
        float sprintDip = Input.GetKey(KeyCode.LeftShift) && speed > 4.8f ? 0.04f : 0f;
        Vector3 targetPos = weaponBaseLocalPosition + new Vector3(-mouseX * 0.01f, -sprintDip - mouseY * 0.004f, 0f);
        Quaternion targetRot = weaponBaseLocalRotation * Quaternion.Euler(-mouseY * 2.4f, mouseX * 3.2f, -mouseX * 1.4f);

        weaponHolder.localPosition = Vector3.Lerp(weaponHolder.localPosition, targetPos, Time.deltaTime * 9f);
        weaponHolder.localRotation = Quaternion.Slerp(weaponHolder.localRotation, targetRot, Time.deltaTime * 9f);
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == childName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildByName(root.GetChild(i), childName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
