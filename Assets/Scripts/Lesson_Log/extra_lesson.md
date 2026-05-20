# Extra Lesson（课外补充）

> **非 yxc 正课编号内容**：把房间 HTTP、WebGL 页面与 nginx **统一到自己的 HTTPS 域名**下，避免混合内容与写死内网 IP。正课笔记仍以 `lesson-*.md` 为准。

## 1. 为什么要改

- 页面在 **`https://app7926.acapp.acwing.com.cn/webgl/fps/`** 时，再用 **`http://49.xxx:8000`** 拉房间列表会触发浏览器 **混合内容（HTTPS 页面请求 HTTP）** 拦截或额外 CORS 成本。
- 联机若走浏览器 **WSS**：多房间时常用 **`wss://app7926.acapp.acwing.com.cn:17777`**（及 17778、17779）由 nginx **TLS 终结** 后反代到本机 **7777–7779**；不要用 **`ws://公网IP:7777`**。

## 2. Unity 侧（`NetworkManagerUI`）

- **`DeployHttpsHost`**：`app7926.acapp.acwing.com.cn`（换部署只改这一处主机名）。
- **`ApiBase`（条件编译）**
  - **WebGL 正式包**：空字符串 → 请求 **`/fps/get_room_list/`** 等同源根路径。
  - **编辑器 / 非 WebGL 包**：**`https://{DeployHttpsHost}`**。
- **`DeployWebSocketUrl`**：**`wss://app7926.acapp.acwing.com.cn/wss`**（5015 单路径用法）；**三房间**见 **`extra_lesson.md` §3 的 17777–17779。

## 3. nginx：`nginx.conf` 里增加 WSS 外壳（17777–17779）

### 改哪里

1. 编辑主配置：
   ```bash
   sudo vim /etc/nginx/nginx.conf
   ```
2. 找到 **`http {`** … **`}`**，在里面找到你**原来的**整段：
   ```nginx
   server {
       listen 443 ssl;
       ...
   }
   ```
3. 在这整段 **`server { listen 443 ssl; ... }` 的后面**（或前面也行，但**必须同在 `http { }` 内**），**再粘贴下面三个 `server { }`**。  
   **禁止**：把这三段缩进、嵌套进 **443** 那个 `server` 的大括号里——它们与 443 是**兄弟**关系，不是父子关系。
4. 保存后执行：
   ```bash
   sudo nginx -t && sudo systemctl reload nginx
   ```
5. **腾讯云安全组**：放行 **17777–17779 TCP**（公网连 WSS）；**7777–7779** 不必对公网开放。

### 粘贴内容（与 `listen 443 ssl` 的 `server` 平级）

```nginx
server {
    listen 17777 ssl;
    server_name app7926.acapp.acwing.com.cn;

    ssl_certificate     /etc/nginx/cert/acapp.pem;
    ssl_certificate_key /etc/nginx/cert/acapp.key;

    location / {
        proxy_pass http://127.0.0.1:7777;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_read_timeout 86400;
        proxy_send_timeout 86400;
        proxy_buffering off;
    }
}

server {
    listen 17778 ssl;
    server_name app7926.acapp.acwing.com.cn;

    ssl_certificate     /etc/nginx/cert/acapp.pem;
    ssl_certificate_key /etc/nginx/cert/acapp.key;

    location / {
        proxy_pass http://127.0.0.1:7778;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_read_timeout 86400;
        proxy_send_timeout 86400;
        proxy_buffering off;
    }
}

server {
    listen 17779 ssl;
    server_name app7926.acapp.acwing.com.cn;

    ssl_certificate     /etc/nginx/cert/acapp.pem;
    ssl_certificate_key /etc/nginx/cert/acapp.key;

    location / {
        proxy_pass http://127.0.0.1:7779;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_read_timeout 86400;
        proxy_send_timeout 86400;
        proxy_buffering off;
    }
}
```

### 其它（WebGL、443 站点）

- **`/webgl/fps/`** 静态与 **`.gz`**：仍在 **443** 的 `server` 里配置，见 **`README.md`** 对应小节。
- **`location /wss`（5015）**：若仍用可保留；与 **17777–17779** 不冲突。

## 4. Django / JSON 与 Unity

- 房间列表、建房返回需提供客户端连接信息：**`host`**（如 **`app7926.acapp.acwing.com.cn`**）、**`port`**（**17777–17779**）。退房 **`remove_room`** 仍应对 **`internal_port`**（**7777–7779**）登记，故 JSON 中 **`internal_port`** 与对外的 **`port`** 并存时，Unity **`NetworkManagerUI`** 会用 **`internal_port`** 调用退房。
- **`Room` / `BuildRoomResponse`** 已扩展 **`host`、`secure`、`internal_port`、`url`**；旧接口只返回 **`name`+`port`** 时行为与以前接近（`host` 空则客户端用 **`DeployHttpsHost`**），但 **WebGL + WSS** 时请务必让 **`port`** 为 **1777x**，否则浏览器无法连 **7777**。
- 本工程 **`NetworkManagerUI`** 已改为 **`UnityTransport`**：**`SetConnectionData(host, port)`** 用于浏览器 **WSS 外壳**（**1777x**）； **`NetworkManager` 上只挂一个 `Unity Transport`**，勿再与 **`UNetTransport`** 同挂。
- 若 **`com.unity.transport` 低于 2.0**：Inspector/C# 未必有 **Use WebSockets**，以当前 NGO/Transport 文档为准（或升级包）；旧 **`UNetTransport` + `AddHost` UDP** 无法在 WebGL HTTPS 页与 **`wss://`** 对齐。

## 5. 自查

- Scripts 内已无 **`49.232.*`** 等旧 HTTP API 基址。
- WebGL 不要用 **`file://`** 打开 `index.html` 测同源 API；用部署后的 **HTTPS URL**。

## 6. 与正课关系

| 正课 | 本补充 |
|------|--------|
| Lesson 9.2：HTTP 房间列表思路 | 基址从「课件 IP」→ **HTTPS 域名 / 同源根路径** |
| Lesson 9.2：云端端口与专服 | **进程仍监听 7777–7779（或自设）**；对外由 **17777–17779 WSS** 接入 |

---

## 7. 部署与联机检查清单

### A. 云与安全组

- [ ] **443**：HTTPS 站点（Django / 静态 / Web **`/webgl/fps/`**）。
- [ ] **17777–17779 TCP**：浏览器 **WSS 外壳**（nginx TLS → 本机 **7777–7779**）。
- [ ] **不必**对公网放开 **7777–7779**（仅本机进程监听即可）。
- [ ] （按需）**80**：HTTP 跳转 HTTPS。

### B. nginx（与 `http {}` 内其它 `server` 平级、不重复 `server_name` 冲突）

- [ ] **`listen 443 ssl`**：`/` → uWSGI；**`/static/`**、**`/media/`**；**`/webgl/fps/`**（含 **`.gz`** 正则段，见 **`README.md`**）；**`/wss`** → `5015`（若仍用）。
- [ ] **三个 `server { listen 17777|17778|17779 ssl; }`**：在 **`nginx.conf` 的 `http { }` 内**与 **`listen 443 ssl`** 的 **`server` 平级粘贴**，**不要**嵌套进 443 的 `server`；**`proxy_pass http://127.0.0.1:7777|7778|7779`**，**`Upgrade` / `Connection`**、超时、`proxy_buffering off` 同 **`extra_lesson.md` §3**。
- [ ] **`sudo nginx -t`** 通过后再 **`reload`**。

### C. 进程与端口

- [ ] 三实例（或等价）专服：**`-lauch-as-server -port 7777`**（及 7778、7779），与 HTTP 房间列表登记一致。
- [ ] Django **退房** `remove_room` 使用 **进程端口（7777–7779）**；列表/建房 JSON 建议含 **`internal_port`**，与对外 **`port`（1777x）** 区分。

### D. Django / JSON

- [ ] **`GET /fps/get_room_list/`**：每个 **`room`** 含 **`port`**（客户端连接，推荐 **17777–17779**）；可选 **`host`**、**`internal_port`**。
- [ ] **`GET /fps/build_room/`**：成功时 **`port`** + 可选 **`host` / `internal_port`**（退房用 **非 0 的 `internal_port`** 优先）。
- [ ] 字段名与 Unity **`Room` / `BuildRoomResponse`** 一致（**`internal_port`** 等）。

### E. Unity 工程（本仓库）

- [ ] **`NetworkManagerUI.DeployHttpsHost`** 与线上一致。
- [ ] **专服命令行**：**`-port`** 时 **`UnityTransport.SetConnectionData("0.0.0.0", port, "0.0.0.0")`**（见 **`ApplyCommandLineConfig`**）。
- [ ] **进房 / 建房**：**`ApplyUnityTransportClient`** 使用 API 的 **公网 `port`（1777x）** 与 **`host`**（**`SetConnectionData`**），**绝不**把 **`internal_port`** 当连接端口。
- [ ] **退房**：**`buildRoomPortForRemoveApi`** = **`internal_port` 非 0 则用之，否则 `port`**（须与 Django **`remove_room`** 约定一致；**连接**始终用公网 **`port`**，见 **`NetworkManagerUI`**）。
- [ ] **WebGL**：场景 **`NetworkManager`** 使用 **`Unity Transport`（`UnityTransport`）**，**勿**再挂 **`UNetTransport`** / 混合 Transport；若仍 **`ws://`** 或连接失败，确认 **WebSockets**、WebGL / Linux 专服包与 nginx **1777x** 一致。

### F. WebGL 与浏览器

- [ ] 页面从 **`https://…/webgl/fps/`** 打开，勿 **`file://`**。
- [ ] **`.wasm` MIME**、**预压缩 `.gz`**（若使用）与 nginx 一致。

### G. 文档索引

- [ ] 正课流程：**`Lesson_Log/lesson-9.2.md`**
- [ ] 课外部署：**本文 §1–§7（本清单）** + **`README.md`**（Nginx WebGL 代码块）

---

*可按课程要求把本文件当作「Extra / 部署附录」，不计入必须提交的课次清单。*
