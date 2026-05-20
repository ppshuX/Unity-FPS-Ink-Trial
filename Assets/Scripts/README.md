# Unity FPS 脚本说明（按当前代码同步）

> 本文档描述 `Assets/Scripts` 当前可见脚本，覆盖 Lesson 6 射击特效、**Lesson 10（弹药/换弹/HUD）**、Lesson 9.1（端口/专服背景）与 Lesson 9.2（HTTP 房间列表）。**课外补充（HTTPS / WebGL / nginx / WSS 多端口）** 见 **`Lesson_Log/extra_lesson.md`**（含 **§7 部署与联机检查清单**），不计入 yxc 正课编号。

## 当前脚本结构

```text
Assets/Scripts/
├─ GameManager/
│  ├─ GameManager.cs
│  └─ MatchingSettings.cs
├─ Network/
│  ├─ ClientNetworkTransform.cs
│  ├─ NetworkManagerUI.cs
│  └─ response/
│     ├─ BuildRoomResponse.cs
│     ├─ GetRoomListResponse.cs
│     ├─ RemoveRoomResponse.cs
│     └─ Room.cs
└─ Player/
   ├─ Player.cs
   ├─ PlayerController.cs
   ├─ PlayerInfo.cs
   ├─ PlayerInput.cs
   ├─ PlayerSetup.cs
   ├─ PlayerShooting.cs
   ├─ PlayerUI.cs
   ├─ PlayerWeapon.cs
   ├─ WeaponGraphics.cs
   └─ WeaponManager.cs
```

## 功能概览（对应现有实现）

- 网络菜单（**Lesson 9.2**）：`NetworkManagerUI` 通过 **`UnityEngine.Networking.UnityWebRequest`**（全限定名，避免 `using UnityEngine.Networking` 触发 **`NetworkTransport` 歧义**）访问房间服务。**WebGL** 部署在 `https://app7926.acapp.acwing.com.cn/webgl/fps/` 时使用**同源根路径**（`/fps/...`）；**编辑器 / 独立包**使用 `https://app7926.acapp.acwing.com.cn`。联机：**`UnityTransport`**（UTP）通过 **`SetConnectionData(host, port)`** 使用列表/建房返回的 **`host` + `port`**（课外 **WSS 外壳** 常为 **17777–17779**，见 **`Lesson_Log/extra_lesson.md`**）；**`DeployWebSocketUrl`**（`/wss`→5015）为单路径示例。仍支持 **`-port`**、**`-lauch-as-server`**。详见 `Lesson_Log/lesson-9.2.md`。
- **战斗 UI（Lesson 10）**：**`PlayerUI`** 显示本地弹药与血条（**`PlayerSetup.setPlayer`** 绑定）；**`PlayerInfo`** 头顶名字/血条朝向主相机；**`WeaponManager.Reload`** + **`PlayerWeapon`** 弹量/换弹；**`R` 换弹**、**`K` 自伤调试**；死亡时 **`StopShooting`** 取消连发。详见 `Lesson_Log/lesson-10.md`。
- 移动输入：`PlayerInput` 负责移动、视角与跳跃推力输入。
- 联机初始化：`PlayerSetup` 在本地/远端玩家上执行差异化组件开关与注册。
- 武器切换：`WeaponManager` 管理主副武器实例化、**`Reload` 协程与 `GetCurrentAudioSource()`**，并通过 `Q` + `ServerRpc/ClientRpc` 同步切枪。
- 射击判定：`PlayerShooting` 根据当前武器参数（射速、伤害、射程）、**弹量与换弹状态**执行射线检测并上报伤害。
- 射击特效（Lesson 6）：`PlayerShooting` 通过 `WeaponGraphics` 触发枪口火焰与命中材质特效。
- 生命与重生：`Player` 在服务端权威扣血，死亡后禁用组件并延时重生。
- 对局状态显示：`GameManager` 维护玩家字典；**`OnGUI`** 可作调试全员血量；本地 HUD 与头顶条见 **`PlayerUI`** / **`PlayerInfo`**（Lesson 10）。

## Lesson 9.2：HTTP 房间列表与建房（当前实现）

### 1) `Network/NetworkManagerUI.cs`

- **`DeployHttpsHost`** / **`ApiBase`**（条件编译）：HTTPS 主机与（非 WebGL 包下的）完整 API 根；WebGL 正式包里 **`ApiBase` 为空**，请求为 **`/fps/get_room_list/`**、**`/fps/build_room/`**、**`/fps/remove_room/?port=`**。**`DeployWebSocketUrl`**：`wss://…/wss`（5015）；**多房间 WSS** 见 **`extra_lesson.md` §3（17777–17779）**。
- 依赖 **`TMPro`**；与 **`Unity Transport`（`UnityTransport`）** 同物体挂载（**`NetworkManager`** 上 **Add Component → Unity Transport**）；脚本内通过 **`GetComponent<UnityTransport>()`** 配置连接。
- **Refresh**：清旧动态 `Button`，用 **`roomButtonPrefab`** 在 **`menuUI`** 下实例化；点击某房 → 设端口 → **`StartClient()`** → `DestroyAllButtons()`。
- **Build**：`error_message == "success"` 时记录 **`buildRoomPortForRemoveApi`**（退房 URL 用），**客户端连接**仅用返回的 **`port`（1777x）** + **`host`**，**`StartClient()`**。
- **退出**：`OnApplicationQuit` 若 **`buildRoomPortForRemoveApi != -1`** 则请求退房。

### 2) JSON DTO（`Network/response/`）

- **`Room`**：`name`、`port`。
- **`GetRoomListResponse`**：`error_message`、`rooms`。
- **`BuildRoomResponse`**：`error_message`、`name`、`port`。
- **`RemoveRoomResponse`**：`error_message`。

### 3) Inspector 绑定

1. 同 9.1：**`NetworkManager` + `Unity Transport`（`UnityTransport`）** + **`NetworkManagerUI`** 同一物体（或保证能 **`GetComponent<UnityTransport>()`**）。
2. **Refresh**、**Build** 按钮；**`menuUI`（Canvas）**；**房间条目预制体**（根上 `Button`，子级 **`TextMeshProUGUI`** 显示房名）。

### 4) 版本注意

- **`UnityTransport`**：**`packages-lock.json`** 当前为 **`com.unity.netcode.gameobjects` 1.12.2**、**`com.unity.transport` 1.4.0**。在此组合下 **`UseWebSockets` 往往不存在**，脚本会打 **`[UTP]`** 警告；WebGL/WSS 与专服 **`-port`** 需在 **Inspector** 启用 WebSocket（若有）或升级 **Transport 2.x** 及匹配的 NGO，见 `Lesson_Log/lesson-9.2.md`。

### 5) 玩家 / UI / 相机（与 Lesson 10 一致）

- **Tag、MainCamera、`Menu World Camera`、头顶条、射击 Mask**：打包与联机排查见 **`Lesson_Log/lesson-10.md`** 文末 **Inspector 检查清单**。

### 6) 云端与 Web（扩展）

- **HTTP 与专服分工、Linux 多端口无头进程、WebGL 安装与托管**：见 **`Lesson_Log/lesson-9.2.md`** 文末两节。**在 `nginx.conf` 的 `http { }` 内与 `listen 443` 的 `server` 平级**增加 **17777–17779 WSS 反代**（勿嵌套进 443）、安全组：见 **`Lesson_Log/extra_lesson.md` §3** 与 **§7**。

#### Nginx：`/webgl/fps/`（Unity WebGL 静态托管，含预压缩 `.gz`）

与 **`location /`（uWSGI）**、**`location /wss`（WebSocket 反代）** 并存时，`alias` 目录按服务器实际路径替换（示例为 `/home/ubuntu/acapp/webgl/fps/`）。

- **前缀 `location` 不用 `^~`**：以便与后面正则 `location` 配合；**正则块需写在普通前缀 `location /webgl/fps/` 前面**（或靠 `location` 匹配顺序，文档里放前更直观）。
- **`.wasm.gz` / `.js.gz` / `.data.gz`**：`gzip off` + `Content-Encoding: gzip`，避免 nginx 对已压缩内容再压一层；脚本需能通过 **Accept-Encoding** 或构建产物实际提供这些文件（Unity WebGL 可开 **Decompression Fallback** / 预压缩管线视版本而定）。
- **`try_files $uri $uri/ =404;`**：未命中预压缩规则时仍走前缀 `location`；错误路径 **404**，避免误进 Django。
- **`Cache-Control: no-cache`**：开发期方便；上线后可只对非 `.gz` 或加长缓存视需求改。

```nginx
location = /webgl/fps {
    return 301 /webgl/fps/;
}

location ~ ^/webgl/fps/Build/.*\.wasm\.gz$ {
    root /home/ubuntu/acapp;
    gzip off;
    types { application/wasm gz; }
    add_header Content-Encoding gzip always;
    add_header Vary Accept-Encoding always;
    add_header Cache-Control "no-cache" always;
}

location ~ ^/webgl/fps/Build/.*\.js\.gz$ {
    root /home/ubuntu/acapp;
    gzip off;
    types { application/javascript gz; }
    add_header Content-Encoding gzip always;
    add_header Vary Accept-Encoding always;
    add_header Cache-Control "no-cache" always;
}

location ~ ^/webgl/fps/Build/.*\.data\.gz$ {
    root /home/ubuntu/acapp;
    gzip off;
    default_type application/octet-stream;
    add_header Content-Encoding gzip always;
    add_header Vary Accept-Encoding always;
    add_header Cache-Control "no-cache" always;
}

location /webgl/fps/ {
    alias /home/ubuntu/acapp/webgl/fps/;
    index index.html;
    try_files $uri $uri/ =404;

    add_header Cache-Control "no-cache";
}
```

## Lesson 9.1：端口与专服（课件前置，菜单已由 9.2 替换）

- 9.1 课为固定 **room1/room2**、Host/Server/Client 按钮；**当前工程 `NetworkManagerUI` 已升级为 9.2**，不再使用五个固定房间按钮。
- **`-port` / `-lauch-as-server`** 行为仍在上面的 `NetworkManagerUI` 中保留；专服说明与 9.1 笔记见 **`Lesson_Log/lesson-9.1.md`**。

## Lesson 10：弹药、换弹与 UI（最后一课）

### 1) `PlayerWeapon.cs`

- **`maxBullets` / `bullets` / `reloadTime`**；**`isReloading`**（换弹中禁止开火）。

### 2) `WeaponManager.cs`

- **`Reload(PlayerWeapon)`** + **`ReloadCoroutine`**；**`GetCurrentAudioSource()`** 供射击音效；本地玩家 **`spatialBlend = 0`**。

### 3) `PlayerShooting.cs`

- **`R`** 换弹；**`K`** 调试自伤；**`Shoot()`** 扣弹、空弹匣自动换弹；连发松键 **Fire1** 或 **Q** 时 **`CancelInvoke("Shoot")`**；**`StopShooting()`**。

### 4) `Player.cs`

- **`Die()`** 内 **`StopShooting()`**，避免死亡后连发仍触发。

### 5) `PlayerUI.cs` / `PlayerInfo.cs`

- **`PlayerUI`**：场景唯一 **`Singleton`**，**`setPlayer`** 由 **`PlayerSetup`** 绑定本地玩家；TMP 弹药文案 + 血条缩放。
- **`PlayerInfo`**：头顶名字与血条，Billboard 朝向 **`Camera.main`**。

### 6) Inspector 与场景

- 详见 **`Lesson_Log/lesson-10.md`**；**必须有场景内 `PlayerUI`**，否则本地玩家生成时空引用。

## Lesson 6：射击特效链路

### 1) 武器特效容器：`Player/WeaponGraphics.cs`

- `muzzleFlash`：开枪瞬间播放的枪口火焰粒子。
- `metalHitEffectPrefab`：命中金属目标时实例化的粒子预制体。
- `stoneHitEffectPrefab`：命中石头/默认目标时实例化的粒子预制体。

### 2) 特效触发：`Player/PlayerShooting.cs`

- 每次 `Shoot()` 先调用 `OnShootServerRpc()`，再广播 `OnShootClientRpc()`，统一播放枪口火焰。
- 射线命中后调用 `OnHitServerRpc(hit.point, hit.normal, material)`，由各端执行 `OnHit(...)`。
- `OnHit(...)` 使用 `Quaternion.LookRotation(normal)` 让命中特效朝向法线，并在 1 秒后销毁。
- 当前材质枚举为 `Metal/Stone`，玩家命中走 `Metal`，其余命中走 `Stone`。

### 3) 武器模型与特效绑定：`Player/WeaponManager.cs`

- `EquipWeapon(...)` 实例化武器模型后缓存 `currentGraphics` 与 **武器物体上的 `AudioSource`**。
- `GetCurrentGraphics()` / **`GetCurrentAudioSource()`** 供 `PlayerShooting` 读取。
- 切枪后会自动切换到新武器的特效与音效配置；**`Reload`** 仅重置 **序列化武器数据**上的弹量（见 Lesson 10 边界说明）。

## Inspector 配置检查（武器 / 射击 / Lesson 10 UI）

1. 每把武器预制体根节点挂载 `WeaponGraphics`，并配置 **`AudioSource`**（射击 clip）。
2. `WeaponGraphics.muzzleFlash` 已绑定粒子系统。
3. `WeaponGraphics.metalHitEffectPrefab` / `stoneHitEffectPrefab` 已绑定特效预制体。
4. 玩家物体同时挂载 `PlayerShooting` 与 `WeaponManager`。
5. `PlayerShooting.mask` 能命中目标层（Player 与场景碰撞体）。
6. **`PlayerWeapon`**（主/副）：填 **`maxBullets`、`bullets`、`reloadTime`** 等。
7. **场景**：**`PlayerUI`**（TMP 弹药、子弹区、血条 Fill、血条区）；玩家预制体 **`PlayerInfo`**（名字 TMP、血条 Transform、**`infoUI`** 根）。

## 当前代码边界

- 命中材质判定暂为简化版（玩家=Metal，其他=Stone），未按 Tag/Material 自动识别真实材质。
- 命中特效仅做本地播放与 1 秒销毁，未做对象池优化。
- **`GameManager.OnGUI`**：**yxc 课件即有**（左上角红字列全员血量），不是漏删；本工程增加 **`Show Debug Player List In Gui`**，**默认关闭** 便于正式包；需要对照课时再勾选。
- **正式本地/头顶 UI** 依赖 **`PlayerUI` / `PlayerInfo`**；**`PlayerUI` 血条比例按 100 写死**，与 `Player.maxHealth` 不一致时需自行统一。
- 弹量与 **`isReloading`** 为课件向本地逻辑，未做 NGO 强同步。
- 房间 HTTP 仅做连接错误等粗判；`JsonUtility` 对 JSON 格式敏感，需与服务端字段一致。

## 后续建议

- 将命中材质改为按 `Tag` 或 `PhysicMaterial` 自动判定。
- 对 `muzzleFlash` 与命中特效使用对象池，降低频繁 `Instantiate/Destroy` 开销。
- 增加音效、后坐力、换弹与击中标记，完善射击反馈闭环。
- 房间 API 可改为 HTTPS、Token 校验与错误码统一展示。
