# Lesson 8.2 打卡日志：腾空状态、死亡动画与联机动画同步

> 主题：在 8.1 方向动画基础上扩展「离地 / 死亡」状态，并拆分本地与远端的动画更新节奏；完善玩家死亡、关重力与重生恢复。

## 本节核心知识点

### 1) `PlayerController`：`distToGround` 与腾空判定

- 在 `Start()` 中读取碰撞体半高：`GetComponent<Collider>().bounds.extents.y`，与射线判地长度一致思路（与 `PlayerInput` 跳跃可对照）。
- `PerformAnimation()` 末尾：若向下射线未命中地面（离地），将 `direction` 设为 **8**（需在 Animator 中配置对应状态）。

### 2) `PlayerController`：死亡时强制播放死亡方向

- 通过 `GetComponent<Player>().IsDead()` 读取网络同步的死亡标记。
- 若已死亡，将 `direction` 设为 **-1**（需在 Animator 中配置死亡动画），优先级覆盖移动方向推断。

### 3) 本地 / 远端分帧更新动画（联机同步观感）

- **本地玩家**：在 `FixedUpdate()` 中调用 `PerformAnimation()`（与物理步长一致，减少与 `MovePosition` 的时序撕裂）。
- **远端玩家**：仅在 `Update()` 中调用 `PerformAnimation()`（远端位置由网络同步驱动，每帧根据最新 `transform.position` 更新动画更稳）。
- 移动与旋转仍仅对 `IsLocalPlayer` 在 `FixedUpdate()` 中执行，逻辑与 8.1 一致。

### 4) `Player`：`IsDead()` 与 `NetworkVariable<bool> isDead`

- 对外提供 `IsDead()`，供 `PlayerController` 等脚本查询，避免各脚本重复造状态。
- 血量与死亡仍为服务端权威：`TakeDamage` 仅在服务端逻辑路径中扣血；致死时 `isDead.Value = true`，并走 `DieClientRpc` 全端表现。

### 5) 死亡表现与重生恢复（与 Animator / 物理衔接）

- **`Die()`**：先设 Animator `direction = -1`，再 `Rigidbody.useGravity = false`，禁用 `componentsToDisable` 与自身 `Collider`，最后 `StartCoroutine(Respawn())`。
- **`Respawn()`**：等待 `GameManager.Singleton.MatchingSettings.respawnTime`，`SetDefaults()` 恢复组件与碰撞体及服务端血量/死亡标记；将 Animator `direction` 置 **0**，`useGravity = true`；**本地玩家**将位置重置为 `(0, 10, 0)`。

### 6) 与 8.1 的衔接

- 8.1 已完成：本地移动旋转隔离、位移方向动画、跳跃射线判地。
- 8.2 在不动 `PlayerInput` / `PlayerSetup` 的前提下，补强：**腾空状态码、死亡状态码、动画更新分流、死亡与重生流水线**。

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

    private float distToGround = 0f;


    private void Start()
    {
        lastFramePosition = transform.position;
        animator = GetComponentInChildren<Animator>();

        distToGround = GetComponent<Collider>().bounds.extents.y;
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

        if (!Physics.Raycast(transform.position, -Vector3.up, distToGround + 0.1f))
        {
            direction = 8;
        }

        if (GetComponent<Player>().IsDead())
        {
            direction = -1;
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

        if (IsLocalPlayer)
        {
            PerformAnimation();
        }
    }

    private void Update()
    {
        if (!IsLocalPlayer)
        {
            PerformAnimation();
        }
    }
}
```

### `Player.cs`

```csharp
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class Player : NetworkBehaviour
{
    [SerializeField]
    private int maxHealth = 100;
    [SerializeField]
    private Behaviour[] componentsToDisable;
    private bool[] componentsEnabled;
    private bool colliderEnabled;

    private NetworkVariable<int> currentHealth = new NetworkVariable<int>();
    private NetworkVariable<bool> isDead = new NetworkVariable<bool>();


    public void Setup()
    {
        componentsEnabled = new bool[componentsToDisable.Length];
        for (int i = 0; i < componentsToDisable.Length; i ++ )
        {
            componentsEnabled[i] = componentsToDisable[i].enabled;
        }
        Collider col = GetComponent<Collider>();
        colliderEnabled = col.enabled;

        SetDefaults();
    }

    private void SetDefaults()
    {
        for (int i = 0; i < componentsToDisable.Length; i ++ )
        {
            componentsToDisable[i].enabled = componentsEnabled[i];
        }
        Collider col = GetComponent<Collider>();
        col.enabled = colliderEnabled;

        if (IsServer)
        {
            currentHealth.Value = maxHealth;
            isDead.Value = false;
        }
    }

    public bool IsDead()
    {
        return isDead.Value;
    }

    public void TakeDamage(int damage)  // 收到了伤害，只在服务器端被调用
    {
        if (isDead.Value) return;

        currentHealth.Value -= damage;

        if (currentHealth.Value <= 0)
        {
            currentHealth.Value = 0;
            isDead.Value = true;

            if (!IsHost)
            {
                DieOnServer();
            }
            DieClientRpc();
        }
    }

    private IEnumerator Respawn()  // 重生
    {
        yield return new WaitForSeconds(GameManager.Singleton.MatchingSettings.respawnTime);

        SetDefaults();
        GetComponentInChildren<Animator>().SetInteger("direction", 0);
        GetComponent<Rigidbody>().useGravity = true;

        if (IsLocalPlayer)
        {
            transform.position = new Vector3(0f, 10f, 0f);
        }
    }

    private void DieOnServer()
    {
        Die();
    }

    [ClientRpc]
    private void DieClientRpc()
    {
        Die();
    }

    private void Die()
    {
        GetComponentInChildren<Animator>().SetInteger("direction", -1);
        GetComponent<Rigidbody>().useGravity = false;

        for (int i = 0; i < componentsToDisable.Length; i++)
        {
            componentsToDisable[i].enabled = false;
        }
        Collider col = GetComponent<Collider>();
        col.enabled = false;

        StartCoroutine(Respawn());
    }

    public int GetHealth()
    {
        return currentHealth.Value;
    }
}
```


