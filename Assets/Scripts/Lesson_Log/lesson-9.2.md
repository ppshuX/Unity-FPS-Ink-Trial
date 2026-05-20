# Lesson 9.2 打卡日志：HTTP 房间列表、建房与退房

> 主题：用 `UnityWebRequest` 访问房间列表 Web 服务，动态生成房间按钮；建房时向服务端申请端口并用 NGO `StartClient` 进入；房主退出游戏时通知服务端移除房间。

## 本节核心知识点

### 1) HTTP 与 `JsonUtility`

- **`UnityWebRequest.Get(uri)`** 拉取 JSON，通过 **`JsonUtility.FromJson<T>(text)`** 反序列化。
- DTO 类型需带 **`[System.Serializable]`**，字段名与 JSON 键一致（区分大小写）。
- 本工程 DTO：`Room`、`GetRoomListResponse`、`BuildRoomResponse`、`RemoveRoomResponse`（均在 `Assets/Scripts/Network/response/`）。

### 2) 三个接口（本仓库 HTTPS / 同源）

- 主机常量：`NetworkManagerUI.DeployHttpsHost` = **`app7926.acapp.acwing.com.cn`**（改部署时只改此处及相关 nginx）。
- **`ApiBase`**（`private`）：**`UNITY_WEBGL && !UNITY_EDITOR`** 下为 **空字符串**，请求走**根路径**（与页面 `https://…/webgl/fps/` 同源：`/fps/...` → `https://…/fps/...`）；**编辑器 / 独立包**下为 **`https://{DeployHttpsHost}`**，避免写死 `http://IP:8000` 造成混合内容或跨域。
- **`GET /fps/get_room_list/`** → `GetRoomListResponse`，含 `rooms` 数组。`Room` 除 **`name`、`port`** 外可含 **`host`、`secure`、`internal_port`、`url`**（课外 **WebGL + WSS**：`port` 常为 **17777–17779**，`internal_port` 为进程口 **7777–7779** 供退房接口使用；见 **`Lesson_Log/extra_lesson.md`**）。
- **`GET /fps/build_room/`** → `BuildRoomResponse`，`error_message == "success"` 时取 **`port`、`name`**, 及可选 **`host` / `internal_port`**。
- **`GET /fps/remove_room/?port=<端口>`** → `RemoveRoomResponse`，退房上报；Unity 侧优先上报建房返回的 **`internal_port`（非 0）**，否则 **`port`**。
- **WebSocket**：课外部署可为 **`wss://app7926...:17777`** 等（nginx **17777–17779** → 本机 **7777–7779**）；**`/wss`→5015** 为另一路反代；勿再用 **`ws://IP:7777`**。

### 3) UI：刷新、建房与动态房间按钮

- **Refresh**：重新请求列表，销毁旧动态按钮，按课件布局 `localPosition`（`-21`, `92 - k*60`, `0`）实例化 **`roomButtonPrefab`** 到 **`menuUI`** 下。
- 预制体需含 **`Button`**，子物体上有 **`TextMeshProUGUI`** 显示 `room.name`。
- **Build**：请求成功后 **`ApplyUnityTransportClient`**（**公网 `port`**，不用 **`internal_port`**）、记录 **`buildRoomPortForRemoveApi`**（退房用 **`internal_port`** 优先）、**`StartClient()`** 并收起菜单。
- 点击某一房间按钮：**`host`** 空则用 **`DeployHttpsHost`**；**连接端口必须用 `room.port`（1777x）**，**不得**用 **`internal_port`**；按钮文案可用 **`internal_port`** 显示 **Room 7777**。

### 4) 房主退房：`OnApplicationQuit`

- 若 **`buildRoomPortForRemoveApi != -1`**（本局曾成功建房），退出时 **`RemoveRoom()`** 请求服务端删房；协程在退出阶段可能无法跑完，属 Unity 限制，课件同样思路。

### 5) 与 9.1 共用：命令行 `-port` / `-lauch-as-server`

- **`ApplyCommandLineConfig()`** 优先处理 `-port`、**`-lauch-as-server`**（课件拼写）。
- 专服模式下 **`StartServer()`** 后 **`DestroyAllButtons()`** 并 **return**，不再执行 **`InitButtons` / `RefreshRoomList`**，避免 UI 已销毁仍访问引用。

### 6) 本仓库相对课表的实现细节（便于维护）

- **`foreach` + 按钮 `onClick`**：使用循环内局部变量 **`roomPort` / `roomName`** 绑定，避免闭包全部指向最后一个房间。
- **`using (UnityWebRequest ...)`** 释放原生请求。
- 传输层、`NetworkManager.Singleton`、预制体/`menuUI` 等 **判空与日志**，减少打包场景漏绑时的静默失败。

---

## 相关脚本路径

| 文件 | 作用 |
|------|------|
| `Network/NetworkManagerUI.cs` | 菜单逻辑、HTTP、专服入口 |
| `Network/response/Room.cs` | 单房间数据结构 |
| `Network/response/GetRoomListResponse.cs` | 列表接口响应 |
| `Network/response/BuildRoomResponse.cs` | 建房接口响应 |
| `Network/response/RemoveRoomResponse.cs` | 退房接口响应 |

---

## Inspector / 场景检查清单

1. `NetworkManager` 物体：`NetworkManager`、**`Unity Transport`（`UnityTransport`）**、**`NetworkManagerUI`**（同物体以便 `GetComponent<UnityTransport>()`）。
2. `NetworkManagerUI`：**Refresh**、**Build** 按钮；**菜单 Canvas**（`menuUI`）；**房间按钮预制体**（含 Button + TMP 文本）。
3. 工程已引用 **TextMesh Pro**。
4. 若使用 **Android** 等非编辑器平台，明文 **HTTP** 可能需在 Player 设置中允许 **Cleartext**（视系统策略而定）。

## 版本说明

- 本仓库 **`NetworkManagerUI`** 使用 **`UnityTransport`**（**`Unity.Netcode.Transports.UTP`**），HTTP 用 **`UnityEngine.Networking.UnityWebRequest`** 全限定名，避免与旧 **`UnityEngine.Networking.NetworkTransport`** 歧义。房间列表/建房返回的 **`host` + `port`（1777x）** 经 **`SetConnectionData`** 写入；**`-port` 专服** 使用 **`SetConnectionData("0.0.0.0", port, "0.0.0.0")`**。**锁文件**：**`com.unity.transport` 1.4.0** 时常无 **`UseWebSockets`**：脚本用反射，失败时客户端/专服会打 **`[UTP]`** 警告；需 **Inspector** 或升级 **Transport 2.x**。

---

## 云端与端口：HTTP「黑板」≠ 游戏进程

- **HTTP(S) 房间接口**（见上 `ApiBase` / 同域 `/fps/...`）：只做**房间列表 / 建房登记 / 退房**等，返回的每条记录里通常带 **端口**。
- **真正能联机的是 Netcode 监听**：需要在某台机（常见为 **Linux VPS**）上，对每个端口起一个 **无头专服**，与 HTTP 里登记的端口**一致**。
- **Linux 无头示例**（课件参数拼写 **`lauch`**）：
  ```bash
  ./YourBuild.x86_64 -batchmode -nographics -lauch-as-server -port 7777 -logFile server-7777.log
  ```
- **多房间**：**一个进程占一个端口**，例如 7777、7778、7779 各一条命令。**PC/专服直连**时防火墙可放行 **7777–7779**；**浏览器 WebGL** 经 nginx **WSS** 时常只对公网放行 **17777–17779**，**7777–7779** 不必对公网开放，见 **`Lesson_Log/extra_lesson.md` §3、§7**。

---

## Web 版（WebGL）补充说明

- **Build Settings → WebGL**：需先在 **Unity Hub** 为该编辑器版本安装 **WebGL Build Support**，再 **Switch Platform**、**Build**；产物为 **`index.html` + 静态资源**，可放到 **Nginx / OSS / 静态托管** 上通过 **URL** 访问。
- **与本工程联机**：浏览器内网络能力与 **PC + UNet 专服** 差异大，**不要默认**认为与 Windows/Linux 单机包行为一致；往往要单独评估 **传输、HTTPS、跨域（CORS）**、**`wss://…/wss`** 反代等。

---

*参考：AcWing 作者 yxc — [课上代码](https://www.acwing.com/activity/content/code/content/6331705/)*
