# Lesson 9.1 打卡日志：UNet 传输、多端口房间与命令行专服

> **注（本仓库）**：HTTP 房间菜单与 `NetworkManagerUI` 已按 **Lesson 9.2** 使用 **`UnityTransport`（UTP）**；下文 **`UNetTransport`** 为课件原文，便于对照 **`-port` / `ConnectPort`** 等概念，与当前场景里 Transport 组件不一定一致。

> 主题：用 `UNetTransport` 统一配置监听/连接端口，通过 UI 进入不同「房间」（端口），并支持启动参数指定端口与无界面起服。

## 本节核心知识点

### 1) `UNetTransport` 与端口

- Netcode 的 **`ConnectPort`**：客户端连服务端时使用的目标端口。
- **`ServerListenPort`**：服务端监听端口；Host 同时充当服务端与客户端时，两者通常设为一致。
- 课上演示：点「房间 1」连 **7777**，点「房间 2」连 **7778**，便于本地开两个服务端实例分别监听不同端口。

### 2) `GetComponent<UNetTransport>()`

- 课件写法与 **`NetworkManagerUI` 挂在同一物体** 上，且该物体上同时有 **`UNetTransport`**，才能直接 `GetComponent` 取到传输层并改端口。
- 常见做法：与 `NetworkManager` 同一 GameObject（或按你场景实际挂载方式保持一致）。

### 3) 命令行参数：`Environment.GetCommandLineArgs()`

- **`-port <整数>`**：在 `Start()` 最早阶段解析，把 `ConnectPort` 与 `ServerListenPort` 都设为该值；专服或客户端 exe 可用同一套构建，只改参数区分端口。
  - **本仓库 `NetworkManagerUI`**：等价逻辑为 **`UnityTransport.SetConnectionData("0.0.0.0", port, "0.0.0.0")`**（见 9.2 脚本）。
- **`-lauch-as-server`**：课件拼写（launch 的笔误），匹配后调用 `NetworkManager.Singleton.StartServer()`，销毁菜单按钮并起纯服务端。

### 4) UI：`room1` / `room2` 与原有 Host / Server / Client

- `room1`、`room2`：在改端口后调用 `StartClient()`，进入对应端口上的房间。
- `hostBtn` / `serverBtn` / `clientBtn`：逻辑与前几课一致；**未**在点击前强制改端口时，使用 Inspector 里 `UNetTransport` 已配置的默认端口。

---

## 本节关键脚本（课上原始代码，与 AcWing 逐字一致）

### `Network/NetworkManagerUI.cs`

```csharp
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UNET;
using UnityEngine;
using UnityEngine.UI;

public class NetworkManagerUI : MonoBehaviour
{
    [SerializeField]
    private Button hostBtn;
    [SerializeField]
    private Button serverBtn;
    [SerializeField]
    private Button clientBtn;

    [SerializeField]
    private Button room1;
    [SerializeField]
    private Button room2;

    // Start is called before the first frame update
    void Start()
    {
        var args = System.Environment.GetCommandLineArgs();

        for (int i = 0; i < args.Length; i ++ )
        {
            if (args[i] == "-port")
            {
                int port = int.Parse(args[i + 1]);
                var transport = GetComponent<UNetTransport>();
                transport.ConnectPort = transport.ServerListenPort = port;
            }
        }

        for (int i = 0; i < args.Length; i ++ )
        {
            if (args[i] == "-lauch-as-server")
            {
                NetworkManager.Singleton.StartServer();
                DestroyAllButtons();
            }
        }

        room1.onClick.AddListener(() =>
        {
            var transport = GetComponent<UNetTransport>();
            transport.ConnectPort = transport.ServerListenPort = 7777;
            NetworkManager.Singleton.StartClient();
            DestroyAllButtons();
        });

        room2.onClick.AddListener(() =>
        {
            var transport = GetComponent<UNetTransport>();
            transport.ConnectPort = transport.ServerListenPort = 7778;
            NetworkManager.Singleton.StartClient();
            DestroyAllButtons();
        });

        hostBtn.onClick.AddListener(() =>
        {
            NetworkManager.Singleton.StartHost();
            DestroyAllButtons();
        });
        serverBtn.onClick.AddListener(() =>
        {
            NetworkManager.Singleton.StartServer();
            DestroyAllButtons();
        });
        clientBtn.onClick.AddListener(() =>
        {
            NetworkManager.Singleton.StartClient();
            DestroyAllButtons();
        });
    }

    private void DestroyAllButtons()
    {
        Destroy(hostBtn.gameObject);
        Destroy(serverBtn.gameObject);
        Destroy(clientBtn.gameObject);
        Destroy(room1.gameObject);
        Destroy(room2.gameObject);
    }
}
```

---

## Inspector / 场景检查清单

1. `NetworkManager` 物体上挂载 **`UNetTransport`**（与 Netcode 文档一致），端口默认值与联调约定一致。
2. 同一物体挂载 **`NetworkManagerUI`**，保证 `GetComponent<UNetTransport>()` 能取到组件。
3. 将 **Host / Server / Client / Room1 / Room2** 共五个按钮分别拖入对应 `[SerializeField]`。
4. 专服示例：`Game.exe -lauch-as-server -port 7777`（参数名以课件为准：`lauch`）。

## 版本说明（升级 Unity 时留意）

- **`UNetTransport`** 依赖旧版 UNet 可用时的编译条件；在部分 **Unity 2022.2+** 工程里可能被关闭，需改用 **Unity Transport (UTP)** 等方案。当前工程为 **Unity 2021.3 LTS** 时可按课件使用 `UNetTransport`。

---

*参考：AcWing 作者 yxc — [课上代码片段](https://www.acwing.com/activity/content/code/content/6240288/)*
