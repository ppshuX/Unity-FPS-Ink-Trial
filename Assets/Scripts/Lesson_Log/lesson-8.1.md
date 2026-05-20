# Lesson 8.1 打卡日志：跳跃改造、移动动画与本地控制隔离

> 主题：将玩家控制升级为网络本地权威驱动，并加入方向动画与射线判地跳跃。

## 本节核心知识点

### 1) `PlayerController` 升级为 `NetworkBehaviour`

- 只允许本地玩家在 `FixedUpdate()` 中执行移动与视角旋转：
  - `if (IsLocalPlayer) { PerformMovement(); PerformRotation(); }`
- 防止远端玩家重复执行本地输入逻辑导致抖动/错位。

### 2) 后坐力与跳跃力处理细节

- 保留开火后坐力叠加：`AddRecoilForce()`。
- 后坐力每帧衰减：`recoilForce *= 0.5f`。
- 跳跃推力在 `PerformMovement()` 用完即清零：
  - `thrusterForce = Vector3.zero;`

### 3) 基于位移方向驱动动画

- 记录上一帧位置：`lastFramePosition`。
- 用点积计算前后、左右方向：
  - `forward = Dot(deltaPosition, transform.forward)`
  - `right = Dot(deltaPosition, transform.right)`
- 根据阈值 `eps` 映射方向编号，写入 Animator 参数 `direction`。

### 4) `PlayerInput` 跳跃改为射线判地

- 启动时读取胶囊半高：`distToGround = GetComponent<Collider>().bounds.extents.y`。
- 按住跳跃键时，只有射线命中地面才施加向上推力：
  - `Physics.Raycast(transform.position, -Vector3.up, distToGround + 0.1f)`

### 5) `PlayerSetup` 本地/远端层级区分

- 本地玩家设置为 `Player` 层。
- 远端玩家设置为 `Remote Player` 层并禁用本地控制组件。
- 通过递归函数统一设置所有子物体 Layer，避免子节点漏设。

---

## 本节关键脚本（课上原始代码）

### `PlayerController.cs`

```csharp
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerController : NetworkBehaviour
{
    [SerializeField]
    private Rigidbody rb;
    [SerializeField]
    private Camera cam;

    private Vector3 velocity = Vector3.zero;  // 速度：每秒移动的距离
    private Vector3 yRotation = Vector3.zero;  // 旋转角色
    private Vector3 xRotation = Vector3.zero;  // 旋转视角
    private float recoilForce = 0f;  // 后坐力

    private float cameraRotationTotal = 0f;  // 累计转了多少度
    [SerializeField]
    private float cameraRotationLimit = 85f;

    private Vector3 thrusterForce = Vector3.zero;  // 向上的推力

    private float eps = 0.01f;
    private Vector3 lastFramePosition = Vector3.zero;  // 记录上一帧的位置
    private Animator animator;


    private void Start()
    {
        lastFramePosition = transform.position;
        animator = GetComponentInChildren<Animator>();
    }

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

    public void AddRecoilForce(float newRecoilForce)
    {
        recoilForce += newRecoilForce;
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
            thrusterForce = Vector3.zero;
        }
    }

    private void PerformRotation()
    {
        if (recoilForce < 0.1)
        {
            recoilForce = 0f;
        }

        if (yRotation != Vector3.zero || recoilForce > 0)
        {
            rb.transform.Rotate(yRotation + rb.transform.up * Random.Range(-2f * recoilForce, 2f * recoilForce));
        }

        if (xRotation != Vector3.zero || recoilForce > 0)
        {
            cameraRotationTotal += xRotation.x - recoilForce;
            cameraRotationTotal = Mathf.Clamp(cameraRotationTotal, -cameraRotationLimit, cameraRotationLimit);
            cam.transform.localEulerAngles = new Vector3(cameraRotationTotal, 0f, 0f);
        }

        recoilForce *= 0.5f;
    }

    private void PerformAnimation()
    {
        Vector3 deltaPosition = transform.position - lastFramePosition;
        lastFramePosition = transform.position;

        float forward = Vector3.Dot(deltaPosition, transform.forward);
        float right = Vector3.Dot(deltaPosition, transform.right);

        int direction = 0;  // 静止
        if (forward > eps)
        {
            direction = 1;  // 前
        }
        else if (forward < -eps)
        {
            if (right > eps)
            {
                direction = 4;  // 右后
            }
            else if (right < -eps)
            {
                direction = 6;  // 左后
            }
            else
            {
                direction = 5;  // 后
            }
        }
        else if (right > eps)
        {
            direction = 3;  // 右
        }
        else if (right < -eps)
        {
            direction = 7;  // 左
        }

        animator.SetInteger("direction", direction);
    }

    private void FixedUpdate()
    {
        if (IsLocalPlayer)
        {
            PerformMovement();
            PerformRotation();
        }
    }

    private void Update()
    {
        PerformAnimation();
    }
}
```

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

    private float distToGround = 0f;

    // Start is called before the first frame update
    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        distToGround = GetComponent<Collider>().bounds.extents.y;

        print(distToGround);
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

        if (Input.GetButton("Jump"))
        {
            if (Physics.Raycast(transform.position, -Vector3.up, distToGround + 0.1f))
            {
                Vector3 force = Vector3.up * thrusterForce;
                controller.Thrust(force);
            }
        }
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
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsLocalPlayer)
        {
            SetLayerMaskForAllChildren(transform, LayerMask.NameToLayer("Remote Player"));
            DisableComponents();
        } else
        {
            SetLayerMaskForAllChildren(transform, LayerMask.NameToLayer("Player"));
            sceneCamera = Camera.main;
            if (sceneCamera != null)
            {
                sceneCamera.gameObject.SetActive(false);
            }
        }

        string name = "Player " + GetComponent<NetworkObject>().NetworkObjectId.ToString();
        Player player = GetComponent<Player>();
        player.Setup();

        GameManager.Singleton.RegisterPlayer(name, player);
    }

    private void SetLayerMaskForAllChildren(Transform transform, LayerMask layerMask)
    {
        transform.gameObject.layer = layerMask;
        for (int i = 0; i < transform.childCount; i++)
        {
            SetLayerMaskForAllChildren(transform.GetChild(i), layerMask);
        }
    }

    private void DisableComponents()
    {
        for (int i = 0; i < componentsToDisable.Length; i++)
        {
            componentsToDisable[i].enabled = false;
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        if (sceneCamera != null)
        {
            sceneCamera.gameObject.SetActive(true);
        }

        GameManager.Singleton.UnRegisterPlayer(transform.name);
    }
}
```
