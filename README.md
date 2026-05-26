# Airport Security Sucks Cheat

基于 BepInEx 6 (IL2CPP) 框架与 Harmony 补丁构建的《Airport Security Sucks!》Demo 辅助工具。

## 功能特性

### 1. 移动与物理控制 (Movement & Physics)
* **飞行穿墙 (Noclip)**: 绕过 CharacterController 的坐标碰撞限制，支持无级调节飞行速度。
* **移动速度 (Speed Hack)**: 劫持 `MetaPlayerConfig.walkSpeed` 和 `MetaPlayerConfig.sprintSpeed` 的 Getter，按配置倍率缩放人物移速。
* **超级跳跃 (Super Jump)**: 劫持 `nominalJumpHeight` 实现跳跃高度自定义。

### 2. 视觉辅助 (Visuals)
* **玩家透视 (Player ESP)**: 绘制绿色 bounding box，展示名称及距离。
* **违禁品透视 (Contraband ESP)**: 绘制红色 bounding box，并可强制开启违禁品 `Outline` 组件渲染。
* **射线辅助 (Tracers)**: 从屏幕中下方向目标绘制引导线。

### 3. 网络实体控制与生成 (Network & Spawner)
* **载具控制 (Segway Control)**:
  * 远程上车 (`CmdInteract`)
  * 强制弹射 (`CmdDismount`)
  * 触发载具爆炸力场 (`RpcExplosionForce`)
  * 触发载具崩溃 (`RpcCrash`)
  * 鸣笛 (`CmdBeep`)
* **玩家状态修改**:
  * 一键获取 $1,000,000（修改本地 `syncedMoney` 变量，并使用 `CmdSetMoney` 同步）。
  * 局内玩家改名（通过 `CmdSetPlayerName`）。
  * 修改历史胜场 (`CmdSetLifetimeWins`)。
* **全局控制**:
  * 触发警察或走私者胜利 RPC 封包。
  * 强制开始游戏或重置返回大厅.
* **NPC 生成**:
  * 房主状态下直接调用 `NpcManager.ServerInstance.ServerSpawnNpc` 权威生成。
  * 客机状态下通过 `CmdDevSpawnInteractable` 绕过权限申请生成。
* **交互物控制**:
  * 远程互动并释放警犬笼 (`DogCageInteractable.CmdInteract`)。
  * 远程触发自动贩卖机出货 (`VendingMachineInteractable`)。
  * 远程触发愿望单看板 (`WishlistInteractable`)。
  * 远程引爆所有 C4 炸药 (`C4Charge.RpcExplode`)。
  * 远程呼叫所有电梯 (`ElevatorCallButtonInteractable.CmdInteract`)。
  * 远程开关所有休息室门 (`BreakRoomDoor.CmdTriggerDoorUnityEvent` / `CmdResetDoorUnityEvent`)。
  * 远程拉响警报触发封锁 (`LockdownButtonInteractable.CmdInteract`)。

### 4. 逮捕绕过与栽赃 (Arrest & Scapegoating)
* **防踢机制**: Harmony 拦截 `SteamMatchmakingTest.OnLobbyKicked` 阻断自动退房。
* **栽赃机制 (ExecuteWithSpoofedName)**:
  实现了全局栽赃装饰器。在调用任何可能暴露身份的网络交互包（包括监禁、物理拉人、远程扑倒、NPC扑倒、强制自爆、放倒玩家、强制退场等）之前，算法自动提取当前场景中除了操作者和目标外的随机无辜玩家名字，临时伪装本地 `playerName` 发包，随后立即恢复。使全场通知和行为判定均指向该无辜玩家。

### 5. 秒退补丁 (Process Exit Patch)
* 挂钩 `UnityEngine.Application.Quit` 并在拦截后执行 `GetCurrentProcess().Kill()`，避免 conhost/Steamworks API 同步锁死导致 Steam 持续显示游戏在运行的 Bug。

## 安装说明

1. 下载 Release 页面发布的 `AirportSecurityMod.zip`。
2. 解压压缩包内容至游戏根目录（与 `Airport Security Sucks!.exe` 同级）。
3. 启动游戏，按 `Insert` 键打开/关闭控制菜单，按 `F1` 切换穿墙飞行。

## 编译指南

* **环境需求**: .NET 6.0 SDK。
* 依赖的程序集已内置于 `lib/` 文件夹中。
* 执行编译:
  ```bash
  dotnet build -c Release
  ```
  在 Windows 环境下编译会自动将生成的 DLL 拷贝至游戏对应的 `BepInEx/plugins/` 目录。
