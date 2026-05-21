# Unity FPS Ancient Trial 课程进度链

> 这份文档用于快速理解本项目的构建过程。  
> 详细课时笔记仍以同目录下的 `lesson-*.md` 与 `extra_lesson.md` 为准；本文只做主线梳理，方便复盘、答辩准备和新成员上手。

## 快速阅读顺序

1. 先读本文，建立项目从 0 到完整 Demo 的整体脉络。
2. 想看某个功能的细节，再跳到对应课时日志。
3. 想对照当前代码，优先看 `Assets/Scripts/README.md` 和 `Assets/Scripts` 下的实际脚本。

## 项目最终形态

当前项目是一个基于 Unity 的中国风水墨场景第一人称射击试炼 Demo。它把课程中的 FPS、网络同步、房间管理、UI、武器系统等内容整合到一个可运行的项目中：

- 第一人称移动、视角控制与跳跃。
- 射线检测式射击、伤害、死亡与重生。
- 主副武器切换、弹药、换弹、后坐力、音效和命中特效。
- 本地 HUD、头顶玩家名与血条。
- 基于 Netcode for GameObjects 的多人基础框架。
- 基于 HTTP JSON 的房间列表、建房、退房流程。
- 结合 Django、nginx、HTTPS、WebGL/WSS 的部署思路。
- 中国风水墨山水、亭台、栈道、桥梁等场景包装。

## 课程主线

### 1. Lesson 3：FPS 基础骨架

对应日志：`lesson-3.md`

这一阶段先搭出 FPS 的最小可运行骨架，重点是把输入、控制、射击和网络对象分开：

- `PlayerInput` 读取键盘、鼠标与跳跃输入。
- `PlayerController` 执行移动、相机俯仰与角色旋转。
- `PlayerShooting` 从玩家相机发射 Raycast。
- `PlayerWeapon` 保存武器基础参数。
- `PlayerSetup` 根据 `IsLocalPlayer` 区分本地玩家和远端玩家。
- `GameManager` 负责简单运行信息显示。

这一课解决的是“玩家能不能动、能不能看、能不能开枪、联机对象能不能区分本地和远端”的问题。

### 2. Lesson 4：战斗闭环

对应日志：`lesson-4.md`

在基础射击之上补上完整战斗流程：

- 用 `NetworkVariable<int>` 同步血量。
- 用 `NetworkVariable<bool>` 表示死亡状态。
- 命中后通过 `ServerRpc` 在服务端扣血。
- 血量归零后通过 `ClientRpc` 广播死亡表现。
- 死亡后禁用控制、碰撞等组件，再按设定时间重生。
- `MatchingSettings` 开始承担对局参数配置，例如重生时间。

这一课让“射击”从单纯命中提示变成了“命中 -> 扣血 -> 死亡 -> 重生”的玩法闭环。

### 3. Lesson 5：武器管理与切枪同步

对应日志：`lesson-5.md`

这一阶段把武器从单个固定配置扩展为主副武器体系：

- `PlayerWeapon` 扩展为可配置的武器数据。
- `WeaponManager` 负责当前武器、主武器、副武器和模型实例化。
- `Q` 键切换武器。
- 切枪通过 `ServerRpc + ClientRpc` 同步到其它客户端。
- `PlayerShooting` 不再依赖单一武器字段，而是从 `WeaponManager.GetCurrentWeapon()` 读取当前武器参数。

这一课解决的是“不同武器拥有不同射速、伤害、射程、模型，并且多人能看到切换结果”的问题。

### 4. Lesson 6：射击反馈、后坐力与特效

对应日志：`lesson-6.md`

这一阶段开始完善射击手感和表现：

- 支持单发与连发射击。
- 使用射速和冷却时间控制开火节奏。
- `PlayerController` 接入后坐力，让连续射击产生视角扰动。
- 武器模型上挂 `WeaponGraphics`，统一提供枪口火焰和命中特效。
- 开火音效从当前武器实例的 `AudioSource` 播放。
- 命中点根据材质类型播放金属或石头特效。
- 开火与命中特效通过 RPC 同步给其它客户端。

这一课让项目从“能射击”进化为“射击有反馈、别人也能看到反馈”。

### 5. Lesson 8.1：移动动画与本地控制隔离

对应日志：`lesson-8.1.md`

这一阶段开始处理角色动画和网络本地权威：

- `PlayerController` 升级为 `NetworkBehaviour`。
- 只有本地玩家在 `FixedUpdate` 中执行移动与视角控制。
- 通过位移方向计算动画状态，例如前进、后退、左移、右移。
- 跳跃从早期的关节推力逻辑调整为射线判地后施加向上力。
- `PlayerSetup` 区分本地层与远端层，方便射线、渲染和命中判断。

这一课解决的是“本地玩家自己控制自己，远端玩家只显示同步结果，同时角色动画要跟动作一致”的问题。

### 6. Lesson 8.2：腾空、死亡动画与重生恢复

对应日志：`lesson-8.2.md`

在 8.1 的动画基础上继续补状态：

- 通过 `distToGround` 和 Raycast 判断是否离地。
- 离地时切换到腾空动画状态。
- 死亡时强制播放死亡动画。
- 死亡时关闭重力和相关组件，避免死亡角色继续参与正常控制。
- 重生时恢复组件、碰撞、重力和默认动画状态。
- 本地玩家和远端玩家的动画更新节奏分开处理。

这一课让角色状态更完整，避免联机时“死了还在动、跳起来动画不对”等问题。

### 7. Lesson 9.1：多端口房间与命令行专服

对应日志：`lesson-9.1.md`

这一阶段从单一联机入口扩展到多房间思路：

- 每个房间对应一个端口，例如 `7777`、`7778`、`7779`。
- 客户端点击不同房间按钮后连接对应端口。
- 同一个构建可以通过命令行参数决定监听端口。
- `-port <端口>` 用于设置服务端监听端口或客户端目标端口。
- `-lauch-as-server` 用于无界面启动专服。

这一课的核心思路是：HTTP 房间系统还没接入前，先用“端口 = 房间”的方式理解多人房间模型。

### 8. Lesson 9.2：HTTP 房间列表、建房与退房

对应日志：`lesson-9.2.md`

这一阶段接入后端服务，让房间不再写死在 UI 中：

- Unity 客户端用 `UnityWebRequest.Get()` 请求后端接口。
- 用 `JsonUtility.FromJson<T>()` 把 JSON 转成 DTO。
- `GET /fps/get_room_list/` 获取房间列表。
- `GET /fps/build_room/` 创建房间并返回连接端口。
- `GET /fps/remove_room/?port=<端口>` 在房主退出时通知后端移除房间。
- `Room`、`GetRoomListResponse`、`BuildRoomResponse`、`RemoveRoomResponse` 作为接口数据结构。
- 房间按钮由后端返回的数据动态生成。

这一课让项目具备“Unity 客户端 + Django 后端房间服务”的结构，课程展示时可以说明客户端与服务端的协作关系。

### 9. Extra Lesson：部署、HTTPS、WebGL 与 WSS

对应日志：`extra_lesson.md`

这部分是课外补充，用于把课程项目部署到更接近真实展示的环境：

- 页面部署在 HTTPS 域名下时，房间接口也改为同源 HTTPS，避免浏览器混合内容拦截。
- WebGL 页面通过 `/webgl/fps/` 静态托管。
- Django 继续提供 `/fps/...` 房间接口。
- nginx 负责 HTTPS、静态资源和 WSS 反向代理。
- 对 WebGL 联机，外部端口可使用 `17777-17779`，再由 nginx 反代到本机 `7777-7779`。
- JSON 中区分 `port` 和 `internal_port`：前者给客户端连接，后者给退房或进程管理使用。

这一部分可以放到报告或答辩的“后端与部署扩展”里，体现项目不是只在本地 Editor 里跑。

### 10. Lesson 10：弹药、换弹、HUD 与头顶信息

对应日志：`lesson-10.md`

最后一课补齐 FPS 常见 UI 和武器体验：

- `PlayerWeapon` 增加 `maxBullets`、`bullets`、`reloadTime`、`isReloading`。
- `WeaponManager.Reload()` 使用协程实现换弹等待。
- `R` 键手动换弹，弹匣打空后自动换弹。
- 死亡时 `StopShooting()` 停掉连发，避免死亡后仍然开火。
- `PlayerUI` 显示本地玩家弹药和血条。
- `PlayerInfo` 在角色头顶显示玩家名和血条，并朝向主相机。

这一课把项目从“战斗逻辑能跑”推进到“玩家能看懂当前战斗状态”。

## 当前脚本模块地图

### Player

- `PlayerInput.cs`：读取键鼠输入。
- `PlayerController.cs`：执行移动、旋转、跳跃、动画方向和后坐力。
- `Player.cs`：血量、死亡、重生和组件启停。
- `PlayerSetup.cs`：本地/远端玩家初始化、层级设置、相机与 UI 绑定。
- `PlayerShooting.cs`：开火输入、射线命中、伤害 RPC、枪口和命中特效。
- `PlayerWeapon.cs`：武器参数、弹量、换弹状态。
- `WeaponManager.cs`：当前武器、主副武器、模型实例化、切枪和换弹。
- `WeaponGraphics.cs`：武器特效引用。
- `PlayerUI.cs`：本地 HUD。
- `PlayerInfo.cs`：头顶名字和血条。

### Network

- `NetworkManagerUI.cs`：房间菜单、HTTP 请求、动态房间按钮、建房/退房、专服命令行参数。
- `ClientNetworkTransform.cs`：客户端权威同步策略。
- `response/Room.cs`：房间数据。
- `response/GetRoomListResponse.cs`：房间列表响应。
- `response/BuildRoomResponse.cs`：建房响应。
- `response/RemoveRoomResponse.cs`：退房响应。

### GameManager

- `GameManager.cs`：玩家注册表、玩家查找、调试血量列表。
- `MatchingSettings.cs`：对局参数配置。

## 后端与部署主线

本项目中的后端不是单纯写在 Unity 里的逻辑，而是通过课程日志记录了完整设计：

1. Django 后端维护房间信息。
2. Unity 客户端请求 `/fps/get_room_list/` 获取可加入房间。
3. Unity 客户端请求 `/fps/build_room/` 创建房间。
4. Unity 客户端退出时请求 `/fps/remove_room/?port=...` 通知后端移除房间。
5. 专服进程按端口启动，房间记录和进程端口保持一致。
6. WebGL/公网部署时通过 nginx、HTTPS、WSS 解决浏览器网络限制。

当前代码备注：`NetworkManagerUI.cs` 实际使用的是 `UNetTransport` 的 `ConnectAddress`、`ConnectPort`、`ServerListenPort`。部分日志中记录过迁移到 `UnityTransport / SetConnectionData` 的方案，写报告或答辩时应以当前代码为准说明，除非后续再次切换传输层。

## 与大作业主题的关系

课程代码主线解决的是“可运行、可联机、可交互”的技术骨架；大作业主题包装则通过场景和叙事完成：

- 场景资源使用中国风水墨山水、桥、亭台、栈道等视觉元素。
- 玩法定位为“水墨秘境试炼”，把 FPS 射击练习放入古风建筑与山水环境。
- 报告中可以把技术部分写成“基于 Unity 的第一人称试炼系统”，把主题部分写成“中国传统建筑文化与水墨场景表达”。

## 新成员上手建议

1. 先运行主场景 `Assets/Scenes/SampleScene.unity`，确认 Demo 的实际表现。
2. 阅读 `README.md`，了解项目定位、运行方式和展示口径。
3. 阅读本文，理解课程实现链路。
4. 阅读 `Assets/Scripts/README.md`，对照当前脚本结构。
5. 按需阅读对应 Lesson：
   - 想看战斗：`lesson-4.md`、`lesson-6.md`、`lesson-10.md`
   - 想看武器：`lesson-5.md`、`lesson-10.md`
   - 想看移动动画：`lesson-8.1.md`、`lesson-8.2.md`
   - 想看后端/房间：`lesson-9.1.md`、`lesson-9.2.md`、`extra_lesson.md`

## 可用于答辩的项目演进概括

本项目先完成 FPS 基础操作，再逐步补全射击、伤害、死亡重生、武器切换、后坐力和特效；随后加入多人联机、房间端口、HTTP 房间列表和后端管理；最后通过弹药 HUD、头顶信息和中国风水墨场景完成可演示 Demo。整个过程体现了从核心交互到网络协作，再到主题化场景包装的完整 Unity 项目开发流程。
