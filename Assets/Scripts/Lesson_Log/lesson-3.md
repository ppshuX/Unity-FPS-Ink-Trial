# Unity 3D FPS 学习代码仓库（课程复盘）

> 基于 Unity 3D FPS 课程学习过程整理的代码仓库。  
> 目标是沉淀脚本实现、模块拆分思路与联机基础能力，不以“完整商业游戏”发布为目的。

### 我的代码仓库地址：
## https://github.com/ppshuX/Unity-3D/

## 项目简介

这是一个以 **Unity 3D + C#** 为核心的学习型 FPS 工程。当前代码重点覆盖：

- 玩家移动与视角旋转
- 跳跃推力与物理控制
- 射线检测式射击（Raycast）
- 基于 Netcode for GameObjects 的基础联机框架
- 简单的运行时信息显示与联机启动 UI

从现有脚本看，项目更偏向“**课程知识点落地与验证**”，而非完整玩法闭环（如完整伤害系统、回合/结算系统等）。

---

## 技术栈

- **引擎**：Unity `2022.3.62f3c1`（LTS）
- **语言**：C#
- **网络**：`com.unity.netcode.gameobjects`（NGO）
- **多人调试工具**：`com.unity.multiplayer.tools`
- **渲染管线**：URP（`com.unity.render-pipelines.universal`）
- **UI**：UGUI（`com.unity.ugui`）
- **其他已安装包（当前脚本未明显直接使用）**：
  - Cinemachine
  - TextMeshPro
  - VisualScripting 等

---

## 项目结构

```text
fps/
├─ Assets/
│  ├─ Scenes/
│  │  └─ SampleScene.unity
│  ├─ Prefabs/
│  │  └─ Player.prefab
│  ├─ Scripts/
│  │  ├─ Player/
│  │  │  ├─ PlayerInput.cs
│  │  │  ├─ PlayerController.cs
│  │  │  ├─ PlayerShooting.cs
│  │  │  ├─ PlayerSetup.cs
│  │  │  ├─ PlayerWeapon.cs
│  │  │  └─ GameManager.cs
│  │  └─ Network/
│  │     ├─ NetworkManagerUI.cs
│  │     └─ ClientNetworkTransform.cs
│  └─ ThirdParty/   # 第三方资源与示例素材
├─ Packages/
│  └─ manifest.json
└─ ProjectSettings/
   └─ ProjectVersion.txt
```

---

## 核心功能模块（基于当前代码）

### 1) 玩家控制（移动/跳跃/视角）

- `PlayerInput` 读取键鼠输入：
  - `Horizontal` / `Vertical` 控制平面移动
  - `Mouse X` / `Mouse Y` 控制朝向与俯仰
  - `Jump` 触发向上推力
- `PlayerController` 在 `FixedUpdate` 中执行：
  - `Rigidbody.MovePosition` 进行移动
  - `Rigidbody.AddForce` 施加推力
  - 角色水平旋转 + 相机俯仰旋转
  - 相机俯仰角限制（默认 `±85°`）

### 2) 武器 / 射击逻辑

- `PlayerWeapon` 定义武器基础参数（名称、伤害、射程）
- `PlayerShooting`：
  - 监听 `Fire1`
  - 从玩家相机发射 Raycast，按 LayerMask 过滤命中
  - 命中后调用 `ServerRpc` 上报命中信息

> 当前代码显示：射击命中后主要是记录文本信息，**未看到完整血量扣减/死亡重生**等闭环逻辑。

### 3) 网络相关模块

- `NetworkManagerUI` 提供 Host / Server / Client 三个按钮入口，调用 NGO 启动方法
- `ClientNetworkTransform` 继承 `NetworkTransform` 并返回 `false`，采用 **Client Authority** 思路
- `PlayerSetup` 基于 `IsLocalPlayer`：
  - 关闭非本地玩家组件
  - 仅本地玩家关闭场景主相机并使用玩家相机
  - 按 `NetworkObjectId` 设置玩家名

### 4) 游戏管理与信息显示

- `GameManager` 提供静态 `UpdateInfo` 更新字符串
- 通过 `OnGUI` 将运行信息绘制到屏幕（如“谁命中了谁”）

---

## 关键脚本说明

- `Assets/Scripts/Player/PlayerInput.cs`  
  输入层，负责采集操作并转换为运动/旋转/推力指令。
- `Assets/Scripts/Player/PlayerController.cs`  
  控制层，负责基于刚体执行实际运动和视角旋转。
- `Assets/Scripts/Player/PlayerShooting.cs`  
  射击与命中检测（Raycast）及命中信息上报。
- `Assets/Scripts/Player/PlayerSetup.cs`  
  联机场景下本地玩家与远端玩家组件启停管理。
- `Assets/Scripts/Player/PlayerWeapon.cs`  
  武器参数数据结构（轻量配置类）。
- `Assets/Scripts/Player/GameManager.cs`  
  简单全局信息显示。
- `Assets/Scripts/Network/NetworkManagerUI.cs`  
  联机模式启动 UI 入口。
- `Assets/Scripts/Network/ClientNetworkTransform.cs`  
  客户端权威位姿同步策略封装。

---

## 学习收获（可用于复盘）

- 理解了输入层与控制层分离：`Input -> Controller` 的职责划分更清晰
- 熟悉了刚体驱动移动与相机俯仰限制的 FPS 常见实现
- 实践了 NGO 基础流程：Host/Server/Client 启动、`NetworkBehaviour`、`ServerRpc`
- 学到了联机场景里“本地玩家组件启用、远端玩家组件禁用”的常见处理方式
- 建立了“先跑通核心交互，再逐步补玩法闭环”的迭代思路

---

## 后续可优化方向

- 补全战斗闭环：血量、受击、死亡、重生、击杀统计
- 射击体验优化：开火间隔、后坐力、子弹扩散、换弹系统
- 网络同步完善：命中判定一致性、插值/预测、延迟补偿（根据课程深度逐步引入）
- UI 升级：从 `OnGUI` 迁移到 UGUI/TMP，增加 HUD（血量/弹药/准星）
- 代码结构优化：配置数据 ScriptableObject 化、模块事件化
- 增加基础测试与调试工具（输入、网络状态、命中日志可视化）

---

## 如何运行项目

> 当前仓库信息显示为标准 Unity 工程。以下为通用运行方式，具体以你的本地工程配置为准。

1. 使用 **Unity Hub** 安装并选择 `2022.3.x LTS`（建议与项目版本一致）。
2. 在 Unity Hub 中 `Open` 此项目根目录（包含 `Assets/`, `Packages/`, `ProjectSettings/`）。
3. 等待包与资源导入完成。
4. 打开场景：`Assets/Scenes/SampleScene.unity`（如你实际入口场景不同，请改为对应场景）。
5. 进入 Play 模式：
   - 通过场景中的联机 UI 选择 `Host` / `Server` / `Client` 进行联机测试。
6. 若需多实例联调，可使用：
   - Unity 多开（不同编辑器实例）
   - 或 Build 一个客户端后与 Editor 联调

---

## 说明

- 本仓库定位为 **课程学习与代码复盘**。
- 会优先保留“核心脚本与概念验证”价值，而非产品化完整度。
- 第三方资源/示例脚本可能存在于工程中，但核心学习代码以 `Assets/Scripts` 为主。


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