# Lesson 3 打卡日志

> 说明：以下代码已按我当前项目中的实际实现整理。  
> 备注：原始记录里 `PlayerWeapon.cs` 段落误贴为了 `GameManager`，这里已修正为真实 `PlayerWeapon` 内容。

## 课上代码（我的版本）

### `PlayerInput.cs`

```csharp
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInput : MonoBehaviour
{
    [SerializeField]
    private float speed = 5f;
    [SerializeField]
    private float lookSensitivity = 8f;
    [SerializeField]
    private float thrusterForce = 20f;
    [SerializeField]
    private PlayerController controller;

    private ConfigurableJoint joint;

    // Start is called before the first frame update
    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        joint = GetComponent<ConfigurableJoint>();
    }

    // Update is called once per frame
    void Update()
    {
        float xMov = Input.GetAxisRaw("Horizontal");
        float yMov = Input.GetAxisRaw("Vertical");

        Vector3 velocity = (transform.right * xMov + transform.forward * yMov).normalized * speed;
        controller.Move(velocity);

        float xMouse = Input.GetAxisRaw("Mouse X");
        float yMouse = Input.GetAxisRaw("Mouse Y");

        Vector3 yRotation = new Vector3(0f, xMouse, 0f) * lookSensitivity;
        Vector3 xRotation = new Vector3(-yMouse, 0f, 0f) * lookSensitivity;
        controller.Rotate(yRotation, xRotation);

        Vector3 force = Vector3.zero;
        if (Input.GetButton("Jump"))
        {
            force = Vector3.up * thrusterForce;
            joint.yDrive = new JointDrive
            {
                positionSpring = 0f,
                positionDamper = 0f,
                maximumForce = 0f,
            };
        }
        else
        {
            joint.yDrive = new JointDrive
            {
                positionSpring = 20f,
                positionDamper = 0f,
                maximumForce = 40f,
            };
        }
        controller.Thrust(force);
    }
}
```

### `PlayerController.cs`

```csharp
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField]
    private Rigidbody rb;
    [SerializeField]
    private Camera cam;

    private Vector3 velocity = Vector3.zero;  // 速度：每秒移动的距离
    private Vector3 yRotation = Vector3.zero;  // 旋转角色
    private Vector3 xRotation = Vector3.zero;  // 旋转视角

    private float cameraRotationTotal = 0f;  // 累计转了多少度
    [SerializeField]
    private float cameraRotationLimit = 85f;

    private Vector3 thrusterForce = Vector3.zero;  // 向上的推力


    public void Move(Vector3 _velocity)
    {
        velocity = _velocity;
    }


    public void Rotate(Vector3 _yRotation, Vector3 _xRotation)
    {
        yRotation = _yRotation;
        xRotation = _xRotation;
    }

    public void Thrust(Vector3 _thrusterForce)
    {
        thrusterForce = _thrusterForce;
    }

    private void PerformMovement()
    {
        if (velocity != Vector3.zero)
        {
            rb.MovePosition(rb.position + velocity * Time.fixedDeltaTime);
        }

        if (thrusterForce != Vector3.zero)
        {
            rb.AddForce(thrusterForce);  // 作用Time.fixedDeltaTime秒：0.02s
        }
    }

    private void PerformRotation()
    {
        if (yRotation != Vector3.zero)
        {
            rb.transform.Rotate(yRotation);
        }

        if (xRotation != Vector3.zero)
        {
            cameraRotationTotal += xRotation.x;
            cameraRotationTotal = Mathf.Clamp(cameraRotationTotal, -cameraRotationLimit, cameraRotationLimit);
            cam.transform.localEulerAngles = new Vector3(cameraRotationTotal, 0f, 0f);
        }
    }

    private void FixedUpdate()
    {
        PerformMovement();
        PerformRotation();
    }
}
```

### `PlayerSetup.cs`

```csharp
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerSetup : NetworkBehaviour
{
    [SerializeField]
    private Behaviour[] componentsToDisable;

    private Camera sceneCamera;

    // Start is called before the first frame update
    void Start()
    {
        if (!IsLocalPlayer)
        {
            DisableComponents();
        }
        else
        {
            sceneCamera = Camera.main;
            if (sceneCamera != null)
            {
                sceneCamera.gameObject.SetActive(false);
            }
        }

        SetPlayerName();
    }

    private void SetPlayerName()
    {
        transform.name = "Player " + GetComponent<NetworkObject>().NetworkObjectId;
    }

    private void DisableComponents()
    {
        for (int i = 0; i < componentsToDisable.Length; i++)
        {
            componentsToDisable[i].enabled = false;
        }
    }

    // Update is called once per frame
    private void OnDisable()
    {
        if (sceneCamera != null)
        {
            sceneCamera.gameObject.SetActive(true);
        }
    }
}
```

### `PlayerShooting.cs`

```csharp
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerShooting : NetworkBehaviour
{
    [SerializeField]
    private PlayerWeapon weapon;
    [SerializeField]
    private LayerMask mask;

    private Camera cam;

    // Start is called before the first frame update
    void Start()
    {
        cam = GetComponentInChildren<Camera>();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetButtonDown("Fire1"))
        {
            Shoot();
        }
    }

    private void Shoot()
    {
        RaycastHit hit;
        if (Physics.Raycast(cam.transform.position, cam.transform.forward, out hit, weapon.range, mask))
        {
            ShootServerRpc(hit.collider.name);
        }
    }

    [ServerRpc]
    private void ShootServerRpc(string hittedName)
    {
        GameManager.UpdateInfo(transform.name + " hit " + hittedName);
    }
}
```

### `PlayerWeapon.cs`

```csharp
using UnityEngine;

[System.Serializable]
public class PlayerWeapon
{
    public string name = "M4A1";
    public float damage = 10f;
    public float range = 100f;
}
```

### `GameManager.cs`

```csharp
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    private static string info;

    public static void UpdateInfo(string _info)
    {
        info = _info;
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(200f, 200f, 200f, 400f));
        GUILayout.BeginVertical();

        GUILayout.Label(info);

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}
```
