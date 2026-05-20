# Lesson 10 打卡日志（最后一课）：弹药、换弹、本地 HUD 与头顶信息

> 主题：在 NGO 射击基础上加入弹量与 **`Reload` 协程**；**`R` 手动换弹**、打空自动换弹；死亡时 **`StopShooting`** 停掉连发；**`PlayerUI`** 显示本地弹药与血量条；**`PlayerInfo`** 在世界空间中显示名字与血条并朝向主相机。

## 本节核心知识点

### 1) `PlayerWeapon` 扩展字段

- **`maxBullets` / `bullets`**：弹匣容量与当前弹量。
- **`reloadTime`**：换弹等待秒数。
- **`isReloading`**：`[HideInInspector]`，换弹中禁止开火（**`PlayerShooting.Shoot()`** 开头判断）。

### 2) `WeaponManager.Reload` 与协程

- **`Reload(PlayerWeapon playerWeapon)`**：若已在换弹则 return；否则 **`isReloading = true`** 并 **`StartCoroutine(ReloadCoroutine)`**。
- **`ReloadCoroutine`**：等待 **`reloadTime`** 后 **`bullets = maxBullets`**，**`isReloading = false`**。
- 课件实现为 **本地调度**（协程挂在玩家 **`WeaponManager`** 上），与 **网络同步弹量** 未做强一致；商用可向 `NetworkVariable` 或 **`ServerRpc`** 换弹收敛。

### 3) `PlayerShooting`：输入与弹量逻辑

- **`R`**：**`weaponManager.Reload(currentWeapon)`**，并 **return** 本帧后续射击分支。
- **`K`**：调试向自己扣血，**`ShootServerRpc(transform.name, 10)`**（依赖 **`GameManager.RegisterPlayer`** 后 **`transform.name`** 与字典键一致）。
- **`Shoot()`**：`bullets <= 0` 或 **`isReloading`** 则不打；否则 **`bullets--`**；若 **`bullets <= 0`** 再 **`Reload`**；再走后坐力、**`OnShootServerRpc`**、射线与伤害。
- **命中（与 yxc 课件一致）**：**`hit.collider.tag == "Player"`**，**`ShootServerRpc(hit.collider.name, ...)`**。预制体须保证 **射线打中的 Collider** 所在物体 **Tag 为 Player**，且 **GameObject 名** 与 **`GameManager.RegisterPlayer`** 登记的 **`Player x` 根名** 一致（常见把主 Collider 放在根上）；**`LayerMask`** 须包含 **Remote Player**，否则打不中其它客户端角色。
- **连发**：松开 **Fire1** 或按 **Q** 切枪时 **`CancelInvoke("Shoot")`**（课件用 **`GetButtonUp("Fire1")`**，与此前仅用“按住”判断的版本不同）。
- **`StopShooting()`**：**`CancelInvoke("Shoot")`**，供死亡流程调用。

### 4) `Player.Die` 与射击

- 进入 **`Die()`** 时先 **`GetComponent<PlayerShooting>().StopShooting()`**，避免死亡后 **`InvokeRepeating`** 仍在触发 **`Shoot`**。

### 5) `PlayerUI`（本地 HUD）

- **`Singleton`** 在 **`Awake`** 赋值；场景中需有唯一 **`PlayerUI`** 实例。
- **`setPlayer(Player localPlayer)`**：由 **`PlayerSetup`** 在 **本地玩家 `OnNetworkSpawn`** 时调用；缓存 **`WeaponManager`**，显示子弹/血条父物体。
- **`Update`**：换弹中显示 **`Reloading...`**，否则 **`Bullets: cur/max`**；血条 **`localScale.x = GetHealth() / 100f`**（与课件一致，默认满血按 100 比例）。

### 6) `PlayerInfo`（头顶 UI）

- 挂在玩家预制体上：**`playerName`**（TMP）、**`playerHealth`**（缩放条）、**`infoUI`**（Billboard 根）。
- **`Update`**：名字用 **`transform.name`**（与 **`RegisterPlayer`** 一致）；**本仓库**用 **`GetViewCamera()`**（优先 **`Camera.main`**，否则取已启用且 **`depth` 最大** 的相机）；朝向为 **仅绕 Y 轴的水平 `LookRotation`**，避免课件里全角度 **`LookAt` + `Rotate(180,Y,0)`** 在第三人称下 **TMP 左右镜像**。
- **Tag 与扣血**：**`Player` 根节点 Tag 保持 `Player`**（供 **`PlayerShooting`** 射线判断）；**第一人称子相机** Tag 设 **`MainCamera`** 以便 `Camera.main`；二者不要混到同一物体上。

### 7) `PlayerSetup`（本仓库相对课件的相机关闭方式）

- 课件用 **`Camera.main` 再关掉**，误认为一定是场景相机；若 **FPS 子相机** 已标 **`MainCamera`**，会把 **第一人称相机关掉**，出现 **第三人称或黑屏**。
- **本仓库**：**`Menu World Camera`** 序列化引用（推荐拖 **场景里的 `SceneCamera`**）；若为空则 **`GameObject.Find("SceneCamera")`** 取组件；**只关掉这一台**，不关 **`Camera.main` 当前是谁**。
- **本地玩家**仍会 **`PlayerUI.Singleton.setPlayer(...)`**；无场景 **`PlayerUI`** 会空引用。

---

## 相关脚本路径

| 文件 | 作用 |
|------|------|
| `Player/PlayerWeapon.cs` | 武器数据与弹量、换弹标记 |
| `Player/WeaponManager.cs` | 装备模型、`Reload`、切枪 RPC |
| `Player/PlayerShooting.cs` | 开火、换弹键、弹量、RPC |
| `Player/Player.cs` | 死亡时 `StopShooting` |
| `Player/PlayerUI.cs` | 本地弹药与血条 HUD |
| `Player/PlayerInfo.cs` | 头顶名字与血条 |
| `Player/PlayerSetup.cs` | 注册 `PlayerUI` 目标玩家 |

---

## Inspector / 场景检查清单（必打勾）

1. **场景中**：Canvas 下 **`PlayerUI`**，绑定 **TMP 弹药文本**、**子弹区域 GameObject**、**血条 Fill Transform**、**血条区域 GameObject**；进入前可将子弹/血条父物体 **默认隐藏**，`setPlayer` 会 **`SetActive(true)`**。
2. **玩家预制体**：**主/副武器** 在 **`WeaponManager`** 中配置 **`PlayerWeapon`**（含 **`maxBullets` / `bullets` / `reloadTime`**）；武器 **`graphics`** 预制体含 **`WeaponGraphics`** 与 **`AudioSource`**。
3. **头顶**：**`PlayerInfo`** 三引用齐全；**`infoUI`** 为世界空间子层级根节点之一；若字镜像，检查 **`Info` 层级 Scale X 是否为 -1**。
4. **`PlayerSetup`**：**`Menu World Camera`** 推荐拖 **场景 `SceneCamera`**；或保证场景里存在名为 **`SceneCamera`** 的物体（空槽时自动查找）。
5. **相机与 Tag**：**`Player` 根** Tag = **`Player`**；**`Player` 下子物体 Camera** Tag = **`MainCamera`**；**`SceneCamera` 不要**再占 **`MainCamera`**（避免与 FPS 抢 `Camera.main`）。
6. **`PlayerShooting`**：**Mask** 勾选 **Player** 与 **Remote Player**（或等价可命中人体的层）。
7. **按键**：**Fire1**、**R**、**Q**、**K**（调试）与 Input Manager / 新 Input 体系一致即可。

## 当前代码边界（扩展）

- **`GameManager.OnGUI` 红字列表**：**AcWing 课件原版即包含**（调试用）；本仓库用 **`showDebugPlayerListInGui`** 控制，**默认关闭**。
- 弹量、换弹状态主要为 **本地逻辑**；多机严格一致需服务端权威或同步字段。
- **`PlayerUI`** 血量比例写死 **除以 100**；若 **`maxHealth`** 不是 100，需与 **`Player`** 对齐或改为引用 **`maxHealth`**。

---

*参考：AcWing 作者 yxc — [课上代码](https://www.acwing.com/activity/content/code/content/6433057/)*
