# Lesson 5 打卡日志：武器管理与切换同步

> 主题：完成双武器切换（主武器/副武器）并接入射击参数驱动。

## 本节核心知识点

### 1) 武器数据抽象：`PlayerWeapon`

- 用可序列化类统一描述武器参数：
  - `name`
  - `damage`
  - `range`
  - `shootRate`
  - `graphics`
- `shootRate <= 0` 表示单发，`shootRate > 0` 表示连发。

### 2) 武器管理器：`WeaponManager`

- 在本地角色初始化时默认装备主武器（`primaryWeapon`）。
- 切换武器时：
  - 更新 `currentWeapon`
  - 销毁 `weaponHolder` 下旧武器模型
  - 实例化新武器模型并挂载到 `weaponHolder`

### 3) 联机切枪同步：`ServerRpc + ClientRpc`

- 仅本地玩家可按 `Q` 发起请求（`IsLocalPlayer`）。
- 本地调用 `ToggleWeaponServerRpc()` 将切枪请求发给服务器。
- 服务器调用 `ToggleWeaponClientRpc()` 广播给所有客户端执行 `ToggleWeapon()`。
- 当前实现特点：服务器负责转发，切换执行发生在客户端。

### 4) 射击系统接入当前武器

- `PlayerShooting` 从 `WeaponManager.GetCurrentWeapon()` 读取当前武器。
- 开火逻辑按武器射速分支：
  - 单发：`Input.GetButtonDown("Fire1")`
  - 连发：`InvokeRepeating("Shoot", 0f, 1f / currentWeapon.shootRate)`
- 命中后通过 `ShootServerRpc(name, damage)` 由服务器权威扣血。

---

## 本节实现结果（当前项目同步版）

### 关键流程

1. 本地玩家按 `Q`。
2. `WeaponManager`（仅本地玩家）调用 `ToggleWeaponServerRpc()`。
3. 服务器调用 `ToggleWeaponClientRpc()`。
4. 所有客户端执行 `ToggleWeapon()`，统一更新武器显示。
5. `PlayerShooting` 每帧读取最新 `currentWeapon`，按对应参数开火。

---

## 本节关键脚本（当前项目）

### `Player/PlayerWeapon.cs`

```csharp
using System;
using UnityEngine;

[Serializable]
public class PlayerWeapon
{
    public string name = "M16A1";
    public int damage = 10;
    public float range = 100f;
    public float shootRate = 10f;
    public GameObject graphics;
}
```

### `Player/WeaponManager.cs`

```csharp
using UnityEngine;
using Unity.Netcode;

public class WeaponManager : NetworkBehaviour
{
    [SerializeField] private PlayerWeapon primaryWeapon;
    [SerializeField] private PlayerWeapon secondaryWeapon;
    [SerializeField] private GameObject weaponHolder;

    private PlayerWeapon currentWeapon;

    void Start()
    {
        EquipWeapon(primaryWeapon);
    }

    public PlayerWeapon GetCurrentWeapon() => currentWeapon;

    private void ToggleWeapon()
    {
        if (currentWeapon == primaryWeapon) EquipWeapon(secondaryWeapon);
        else EquipWeapon(primaryWeapon);
    }

    [ClientRpc]
    private void ToggleWeaponClientRpc() => ToggleWeapon();

    [ServerRpc]
    private void ToggleWeaponServerRpc() => ToggleWeaponClientRpc();

    void Update()
    {
        if (IsLocalPlayer && Input.GetKeyDown(KeyCode.Q))
        {
            ToggleWeaponServerRpc();
        }
    }
}
```

### `Player/PlayerShooting.cs`（武器参数接入）

```csharp
private WeaponManager weaponManager;
private PlayerWeapon currentWeapon;

void Start()
{
    cam = GetComponentInChildren<Camera>();
    weaponManager = GetComponent<WeaponManager>();
}

void Update()
{
    currentWeapon = weaponManager.GetCurrentWeapon();

    if (currentWeapon.shootRate <= 0)
    {
        if (Input.GetButtonDown("Fire1")) Shoot();
    }
    else
    {
        if (Input.GetButtonDown("Fire1"))
            InvokeRepeating("Shoot", 0f, 1f / currentWeapon.shootRate);
        else if (Input.GetButtonUp("Fire1"))
            CancelInvoke("Shoot");
    }
}
```

---

## Inspector 配置检查（本节重点）

- `Player` 预制体挂载 `WeaponManager (Script)`。
- `WeaponManager` 的 `Primary Weapon` / `Secondary Weapon` 均已配置。
- 两把武器的 `graphics` 预制体不能为空。
- `weaponHolder` 已拖入正确挂点。
- `PlayerShooting` 与 `WeaponManager` 在同一玩家对象上。

---

## 本节复盘

- 已完成武器从“固定单把参数”升级为“可切换的双武器系统”。
- 已打通“输入 -> ServerRpc -> ClientRpc -> 全端切换显示”的同步链路。
- 已让射击逻辑按当前武器参数动态生效，为后续“换弹/弹药/UI”扩展打好基础。
