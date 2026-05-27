using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Logging;
using UnityEngine;
using Il2CppInterop.Runtime.Injection;
using System.Reflection;
using System.Linq;
using Metater;
using HarmonyLib;

namespace AirportSecurityMod
{
    public partial class HackController : MonoBehaviour
    {
        private void OnGUI()
        {
            try
            {
                DrawESP();

                if (showMenu)
                {
                    windowRect = GUI.Window(999, windowRect, (GUI.WindowFunction)DrawMenu, "<b>超神辅助 v5.1 - Insert = 菜单 | F1 = 飞行</b>");
                }
            }
            catch (System.Exception ex)
            {
                Plugin.LogSource.LogError("OnGUI 异常: " + ex.Message);
            }
        }

        // ===== 异常安全检查 =====
        private void DrawMenu(int windowID)
        {
            try
            {
                // 用背景框隔离开侧边栏和主控制区域 (改为 800x600 布局)
                GUI.Box(new Rect(0, 0, 150, 600), ""); // 侧边栏背景
                GUI.Box(new Rect(150, 0, 650, 600), ""); // 右侧背景

                // 侧边栏 Logo 与状态
                GUI.Label(new Rect(15, 20, 120, 20), "<b>⚡ VAPE</b>");
                GUI.Label(new Rect(15, 38, 120, 15), "SUPER CHEAT v5.2");

                // 选项卡切换按钮
                if (GUI.Button(new Rect(5, 70, 140, 35), currentTab == 0 ? "■ COMBAT ■" : "COMBAT")) currentTab = 0;
                if (GUI.Button(new Rect(5, 110, 140, 35), currentTab == 1 ? "■ VISUAL ■" : "VISUAL")) currentTab = 1;
                if (GUI.Button(new Rect(5, 150, 140, 35), currentTab == 2 ? "■ WORLD ■" : "WORLD")) currentTab = 2;
                if (GUI.Button(new Rect(5, 190, 140, 35), currentTab == 3 ? "■ ONLINE ■" : "ONLINE")) currentTab = 3;
                if (GUI.Button(new Rect(5, 230, 140, 35), currentTab == 4 ? "■ VEHICLE ■" : "VEHICLE")) currentTab = 4;
                if (GUI.Button(new Rect(5, 270, 140, 35), currentTab == 5 ? "■ TROLL ■" : "TROLL")) currentTab = 5;

                string role = (localPlayerName != null && SafeCheckAlive(localPlayerName)) ? (localPlayerName.isServer ? "Server Host" : "Client") : "Offline";
                GUI.Label(new Rect(15, 560, 120, 20), "Role: " + role);

                // 右侧功能页绘制
                DrawTabContent();
            }
            catch (System.Exception ex)
            {
                Plugin.LogSource.LogError("DrawMenu 异常: " + ex.Message);
            }
        }

        private void DrawTabContent()
        {
            float y = 20f;
            if (currentTab == 0)
            {
                GUI.Label(new Rect(170, y, 610, 20), "<b>== 战斗与移动功能 ==</b>"); y += 30f;
                
                if (GUI.Button(new Rect(170, y, 610, 30), (enableFly ? "[ ON ]" : "[ OFF ]") + " 飞行穿墙 (Noclip) [F1]")) ToggleFly();
                y += 35f;

                GUI.Label(new Rect(170, y, 200, 30), "飞行速度: " + flySpeed.ToString("0.0") + " m/s");
                if (GUI.Button(new Rect(380, y, 40, 30), "-")) flySpeed = Mathf.Max(2f, flySpeed - 1f);
                if (GUI.Button(new Rect(430, y, 40, 30), "+")) flySpeed = Mathf.Min(40f, flySpeed + 1f);
                y += 35f;

                if (GUI.Button(new Rect(170, y, 610, 30), (enableSpeedHack ? "[ ON ]" : "[ OFF ]") + " 移速修改")) enableSpeedHack = !enableSpeedHack;
                y += 35f;

                GUI.Label(new Rect(170, y, 200, 30), "移速倍率: " + speedMultiplier.ToString("0.0") + "x");
                if (GUI.Button(new Rect(380, y, 40, 30), "-")) speedMultiplier = Mathf.Max(1.0f, speedMultiplier - 0.5f);
                if (GUI.Button(new Rect(430, y, 40, 30), "+")) speedMultiplier = Mathf.Min(6.0f, speedMultiplier + 0.5f);
                y += 35f;

                if (GUI.Button(new Rect(170, y, 610, 30), (enableSuperJump ? "[ ON ]" : "[ OFF ]") + " 超级跳跃")) enableSuperJump = !enableSuperJump;
                y += 35f;

                GUI.Label(new Rect(170, y, 200, 30), "跳跃倍率: " + jumpMultiplier.ToString("0.0") + "x");
                if (GUI.Button(new Rect(380, y, 40, 30), "-")) jumpMultiplier = Mathf.Max(1.0f, jumpMultiplier - 0.5f);
                if (GUI.Button(new Rect(430, y, 40, 30), "+")) jumpMultiplier = Mathf.Min(6.0f, jumpMultiplier + 0.5f);
                y += 35f;
            }
            else if (currentTab == 1)
            {
                GUI.Label(new Rect(170, y, 610, 20), "<b>== ESP 视觉透视设置 ==</b>"); y += 30f;

                if (GUI.Button(new Rect(170, y, 290, 28), (espPlayer ? "[ ON ]" : "[ OFF ]") + " 透视玩家")) espPlayer = !espPlayer;
                if (GUI.Button(new Rect(480, y, 290, 28), (espNpc ? "[ ON ]" : "[ OFF ]") + " 透视 NPC")) espNpc = !espNpc;
                y += 32f;
                if (GUI.Button(new Rect(170, y, 290, 28), (espContraband ? "[ ON ]" : "[ OFF ]") + " 违禁品透视 (ESP)")) espContraband = !espContraband;
                if (GUI.Button(new Rect(480, y, 290, 28), (autoExposeContraband ? "[ ON ]" : "[ OFF ]") + " 自动标记违禁品 Outline")) autoExposeContraband = !autoExposeContraband;
                y += 32f;
                if (GUI.Button(new Rect(170, y, 290, 28), (espBoxes ? "[ ON ]" : "[ OFF ]") + " ESP 方框 (Boxes)")) espBoxes = !espBoxes;
                if (GUI.Button(new Rect(480, y, 290, 28), (espTracers ? "[ ON ]" : "[ OFF ]") + " ESP 射线 (Tracers)")) espTracers = !espTracers;
                y += 32f;
                if (GUI.Button(new Rect(170, y, 290, 28), (espShowDistance ? "[ ON ]" : "[ OFF ]") + " 显示距离")) espShowDistance = !espShowDistance;
                if (GUI.Button(new Rect(480, y, 290, 28), "手动标记所有红框 Outline"))
                {
                    int n = 0;
                    foreach (var c in cachedContrabands)
                    {
                        if (!SafeCheckAlive(c)) continue;
                        try { if (c.contrabandRedOutline != null) { c.contrabandRedOutline.enabled = true; n++; } } catch { }
                    }
                    Plugin.LogSource.LogInfo("标记Outline数量: " + n);
                }
                y += 32f;

                GUI.Label(new Rect(170, y, 200, 28), "透视最大范围: " + espMaxDistance.ToString("0") + "m");
                if (GUI.Button(new Rect(380, y, 40, 28), "-")) espMaxDistance = Mathf.Max(20f, espMaxDistance - 20f);
                if (GUI.Button(new Rect(430, y, 40, 28), "+")) espMaxDistance = Mathf.Min(500f, espMaxDistance + 20f);
                y += 36f;
            }
            else if (currentTab == 2)
            {
                GUI.Label(new Rect(170, y, 610, 20), "<b>== 场景互动与生成设置 ==</b>"); y += 30f;

                if (GUI.Button(new Rect(170, y, 140, 28), "释放警犬")) ReleaseAllDogs();
                if (GUI.Button(new Rect(320, y, 140, 28), "贩卖机出货")) VendAllMachines();
                if (GUI.Button(new Rect(470, y, 140, 28), "互动愿望单")) InteractWishlist();
                if (GUI.Button(new Rect(620, y, 160, 28), "生娃 (生成NPC)")) SpawnBabyNpc();
                y += 32f;

                if (GUI.Button(new Rect(170, y, 140, 28), "引爆所有C4")) DetonateAllC4();
                if (GUI.Button(new Rect(320, y, 140, 28), "呼叫所有电梯")) SummonAllElevators();
                if (GUI.Button(new Rect(470, y, 140, 28), "开启休息室门")) TriggerBreakRoomDoors(true);
                if (GUI.Button(new Rect(620, y, 160, 28), "一键拉响警报")) TriggerLockdown();
                y += 32f;

                // 新增：第三排场景特权交互
                bool isHost = localPlayerName != null && localPlayerName.isServer;
                if (GUI.Button(new Rect(170, y, 140, 28), isHost ? "一键通关胜利" : "[仅房主] 一键通关"))
                {
                    if (isHost) TriggerInstantWin();
                    else Plugin.LogSource.LogWarning("该功能仅限房主(Host)使用，客机调用会因权限问题被服务器断线！已安全拦截。");
                }
                if (GUI.Button(new Rect(320, y, 140, 28), isHost ? "切换关卡地图" : "[仅房主] 切换地图"))
                {
                    if (isHost) TriggerDevMapSwitch();
                    else Plugin.LogSource.LogWarning("该功能仅限房主(Host)使用，客机调用会因权限问题被服务器断线！已安全拦截。");
                }
                if (GUI.Button(new Rect(470, y, 140, 28), isHost ? "开发者劫机" : "[仅房主] 触发劫机"))
                {
                    if (isHost) TriggerDevHijack();
                    else Plugin.LogSource.LogWarning("该功能仅限房主(Host)使用，客机调用会因权限问题被服务器断线！已安全拦截。");
                }
                if (GUI.Button(new Rect(620, y, 160, 28), isHost ? "激怒所有警犬" : "[仅房主] 激怒警犬"))
                {
                    if (isHost) TriggerDogAbuse();
                    else Plugin.LogSource.LogWarning("该功能仅限房主(Host)使用，客机调用会因权限问题被服务器断线！已安全拦截。");
                }
                y += 32f;

                // 新增：第四排安检扫描仪指示灯控制
                if (GUI.Button(new Rect(170, y, 290, 28), isHost ? "安检扫描仪全部报警 (RED)" : "[仅房主] 扫描仪全部红色报警"))
                {
                    if (isHost) SetAllScannersState(1);
                    else Plugin.LogSource.LogWarning("该功能仅限房主(Host)使用，客机调用会因权限问题被服务器断线！已安全拦截。");
                }
                if (GUI.Button(new Rect(480, y, 300, 28), isHost ? "安检扫描仪全部放行 (GREEN)" : "[仅房主] 扫描仪全部绿色放行"))
                {
                    if (isHost) SetAllScannersState(2);
                    else Plugin.LogSource.LogWarning("该功能仅限房主(Host)使用，客机调用会因权限问题被服务器断线！已安全拦截。");
                }
                y += 38f;

                GUI.Label(new Rect(170, y, 610, 20), "<b>实体生成器 (仅限主机)</b>"); y += 25f;
                if (spawnablePrefabs == null && Mirror.NetworkManager.singleton != null)
                {
                    try { spawnablePrefabs = Mirror.NetworkManager.singleton.spawnPrefabs; } catch { }
                }

                if (spawnablePrefabs == null || spawnablePrefabs.Count == 0)
                {
                    GUI.Label(new Rect(170, y, 300, 25), "Prefab 列表未就绪...");
                    if (GUI.Button(new Rect(480, y, 100, 25), "手动刷新")) spawnablePrefabs = null;
                    y += 30f;
                }
                else
                {
                    GUI.Label(new Rect(170, y, 50, 24), "搜索:");
                    spawnFilter = GUI.TextField(new Rect(230, y, 200, 24), spawnFilter ?? "");
                    if (GUI.Button(new Rect(440, y, 60, 24), "清空")) spawnFilter = "";
                    y += 30f;

                    var filtered = new System.Collections.Generic.List<string>();
                    string flt = (spawnFilter ?? "").ToLower();
                    for (int i = 0; i < spawnablePrefabs.Count; i++)
                    {
                        var pf = spawnablePrefabs[i];
                        if (pf == null) continue;
                        string pname = pf.name ?? "";
                        if (flt.Length == 0 || pname.ToLower().Contains(flt)) filtered.Add(pname);
                    }

                    int spawnPages = (filtered.Count + 7) / 8;
                    if (spawnPages < 1) spawnPages = 1;
                    if (spawnPage >= spawnPages) spawnPage = spawnPages - 1;
                    if (spawnPage < 0) spawnPage = 0;

                    GUI.Label(new Rect(170, y, 200, 25), "列表 (页 " + (spawnPage + 1) + "/" + spawnPages + ")");
                    if (GUI.Button(new Rect(380, y, 40, 22), "◀")) spawnPage = Mathf.Max(0, spawnPage - 1);
                    if (GUI.Button(new Rect(430, y, 40, 22), "▶")) spawnPage = Mathf.Min(spawnPages - 1, spawnPage + 1);
                    y += 28f;

                    int startPF = spawnPage * 8;
                    for (int i = 0; i < 8; i++)
                    {
                        int idx = startPF + i;
                        if (idx >= filtered.Count) break;
                        string pname = filtered[idx];
                        if (GUI.Button(new Rect(170, y, 610, 22), pname)) SpawnByName(pname);
                        y += 24f;
                    }
                }
            }
            else if (currentTab == 3)
            {
                GUI.Label(new Rect(170, y, 610, 20), "<b>== 联机与指令绕过 ==</b>"); y += 30f;

                GUI.Label(new Rect(170, y, 60, 24), "改名称:");
                newPlayerName = GUI.TextField(new Rect(240, y, 200, 24), newPlayerName ?? "");
                if (GUI.Button(new Rect(450, y, 80, 24), "修改"))
                {
                    if (localPlayerName != null)
                    {
                        try
                        {
                            ulong steamIdToUse = localPlayerName.steamId;
                            if (enableAntiKick)
                            {
                                ulong hostId = GetHostSteamId();
                                if (hostId != 0) steamIdToUse = hostId;
                            }
                            localPlayerName.CmdSetPlayerName(newPlayerName, steamIdToUse);
                        }
                        catch { }
                    }
                }
                y += 30f;

                if (GUI.Button(new Rect(170, y, 610, 30), (enableAntiKick ? "[ ON ]" : "[ OFF ]") + " 房主 SteamID/Carson 伪装 (第一阶段-温和防踢)"))
                {
                    enableAntiKick = !enableAntiKick;
                }
                y += 35f;

                if (enableAntiKick)
                {
                    if (GUI.Button(new Rect(170, y, 290, 30), (enableAntiKickLayout ? "[ ON ]" : "[ OFF ]") + " 排版溢出防踢 (推荐-推开踢人按钮)"))
                    {
                        enableAntiKickLayout = !enableAntiKickLayout;
                        if (enableAntiKickLayout) enableAntiKickCrash = false;
                    }
                    if (GUI.Button(new Rect(490, y, 290, 30), (enableAntiKickCrash ? "[ ON ]" : "[ OFF ]") + " 瘫痪房主 Tab 菜单 (强力防踢)"))
                    {
                        enableAntiKickCrash = !enableAntiKickCrash;
                        if (enableAntiKickCrash) enableAntiKickLayout = false;
                    }
                    y += 35f;
                }

                if (GUI.Button(new Rect(170, y, 610, 30), (enableNameSpam ? "[ ON ]" : "[ OFF ]") + " 循环随机乱码改名 (防踢/防选择)"))
                {
                    enableNameSpam = !enableNameSpam;
                }
                y += 35f;

                if (GUI.Button(new Rect(170, y, 290, 30), (enableInstantRecovery ? "[ ON ]" : "[ OFF ]") + " 瞬间倒地解控 / 秒爬"))
                {
                    enableInstantRecovery = !enableInstantRecovery;
                }
                if (GUI.Button(new Rect(490, y, 290, 30), (enableNoTackleCooldown ? "[ ON ]" : "[ OFF ]") + " 扑倒/抓人无冷却 (无限连扑)"))
                {
                    enableNoTackleCooldown = !enableNoTackleCooldown;
                }
                y += 35f;

                GUI.Label(new Rect(170, y, 200, 20), "<b>联机越权房间控制:</b>"); y += 22f;
                if (GUI.Button(new Rect(170, y, 290, 30), "【 越权强制开始游戏 】"))
                {
                    if (GameManager.Instance != null)
                    {
                        try { GameManager.Instance.CmdRequestStartGame(null); } catch { }
                    }
                    else
                    {
                        var gm = UnityEngine.Object.FindObjectOfType<GameManager>();
                        if (gm != null) { try { gm.CmdRequestStartGame(null); } catch { } }
                    }
                }
                if (GUI.Button(new Rect(490, y, 290, 30), "【 越权强制返回大厅 】"))
                {
                    if (GameManager.Instance != null)
                    {
                        try { GameManager.Instance.CmdRequestResetToLobby(null); } catch { }
                    }
                    else
                    {
                        var gm = UnityEngine.Object.FindObjectOfType<GameManager>();
                        if (gm != null) { try { gm.CmdRequestResetToLobby(null); } catch { } }
                    }
                }
                y += 35f;

                GUI.Label(new Rect(170, y, 60, 24), "改胜场:");
                winsAmount = GUI.TextField(new Rect(240, y, 200, 24), winsAmount ?? "");
                if (GUI.Button(new Rect(450, y, 80, 24), "修改"))
                {
                    if (localPlayerName != null && int.TryParse(winsAmount, out int wins)) { try { localPlayerName.CmdSetLifetimeWins(wins); } catch { } }
                }
                y += 30f;

                GUI.Label(new Rect(170, y, 60, 24), "刷钱数:");
                moneyAmount = GUI.TextField(new Rect(240, y, 200, 24), moneyAmount ?? "");
                if (GUI.Button(new Rect(450, y, 80, 24), "加给我")) { if (int.TryParse(moneyAmount, out int amt)) AddMoneyToSelf(amt); }
                if (GUI.Button(new Rect(540, y, 80, 24), "给全场")) { if (int.TryParse(moneyAmount, out int amt)) CreditAllPlayers(amt); }
                y += 30f;

                if (GUI.Button(new Rect(170, y, 290, 30), "一键获取 $1,000,000")) AddMoneyToSelf(1000000);
                if (GUI.Button(new Rect(480, y, 290, 30), "播放全房放屁声 (Fart)")) PlayButtSoundForLobby();
                y += 40f;

                GUI.Label(new Rect(170, y, 610, 20), "<b>控制中心 (仅限主机生效)</b>"); y += 25f;
                if (GUI.Button(new Rect(170, y, 190, 28), "切换警匪")) ToggleRoleBypass();
                if (GUI.Button(new Rect(380, y, 190, 28), "开始登机")) BeginBoardingBypass();
                if (GUI.Button(new Rect(590, y, 190, 28), "结束登机")) EndBoardingBypass();
                y += 32f;

                if (GUI.Button(new Rect(170, y, 190, 28), "警察胜利")) EndGameCopsBypass();
                if (GUI.Button(new Rect(380, y, 190, 28), "走私胜利")) EndGameSmugglersBypass();
                if (GUI.Button(new Rect(590, y, 190, 28), "无人生还")) EndGameNoWinnersBypass();
            }
            else if (currentTab == 4)
            {
                GUI.Label(new Rect(170, y, 610, 20), "<b>== 载具远程控制 (Vehicles) ==</b>"); y += 30f;

                if (GUI.Button(new Rect(170, y, 610, 30), "生成平衡车 (Segway)"))
                {
                    SpawnSegwayPrefab();
                }
                y += 35f;

                int maxSegwayPages = (cachedSegways.Count + 7) / 8;
                if (maxSegwayPages < 1) maxSegwayPages = 1;
                if (vehiclePage >= maxSegwayPages) vehiclePage = maxSegwayPages - 1;
                if (vehiclePage < 0) vehiclePage = 0;

                GUI.Label(new Rect(170, y, 200, 25), "<b>场景平衡车 (页 " + (vehiclePage + 1) + "/" + maxSegwayPages + ")</b>");
                if (GUI.Button(new Rect(380, y, 40, 22), "◀")) vehiclePage = Mathf.Max(0, vehiclePage - 1);
                if (GUI.Button(new Rect(430, y, 40, 22), "▶")) vehiclePage = Mathf.Min(maxSegwayPages - 1, vehiclePage + 1);
                y += 28f;

                int startIndex = vehiclePage * 8;
                for (int i = 0; i < 8; i++)
                {
                    int idx = startIndex + i;
                    if (idx >= cachedSegways.Count) break;
                    var s = cachedSegways[idx];
                    if (!SafeCheckAlive(s)) continue;

                    string sname = "平衡车 #" + s.GetInstanceID();
                    float dist = 0f;
                    if (localMeta != null) dist = Vector3.Distance(localMeta.transform.position, s.transform.position);

                    GUI.Label(new Rect(170, y, 150, 22), sname + " [" + dist.ToString("0") + "m]");
                    
                    if (GUI.Button(new Rect(330, y, 50, 22), "上车")) MountSegway(s);
                    if (GUI.Button(new Rect(385, y, 50, 22), "弹射")) EjectSegway(s);
                    if (GUI.Button(new Rect(440, y, 50, 22), "自爆")) ExplodeSegway(s);
                    if (GUI.Button(new Rect(495, y, 50, 22), "崩溃")) CrashSegway(s);
                    if (GUI.Button(new Rect(550, y, 50, 22), "传送")) Teleport(s.transform.position + Vector3.up * 1f);
                    if (GUI.Button(new Rect(605, y, 50, 22), "吸附")) BringSegway(s);
                    if (GUI.Button(new Rect(660, y, 50, 22), "鸣笛")) BeepSegway(s);
                    if (GUI.Button(new Rect(715, y, 50, 22), "销毁")) DestroySegway(s);

                    y += 26f;
                }
            }
            else if (currentTab == 5)
            {
                GUI.Label(new Rect(170, y, 610, 20), "<b>== 房间恶搞与控制面板 (Troll) ==</b>"); y += 25f;

                var gm = UnityEngine.Object.FindObjectOfType<GameManager>();
                if (gm != null)
                {
                    if (GUI.Button(new Rect(170, y, 190, 26), "强制开启游戏"))
                    {
                        try { gm.CmdRequestStartGame(null); Plugin.LogSource.LogInfo("已发送强制开启游戏命令"); } catch { }
                    }
                    if (GUI.Button(new Rect(380, y, 190, 26), "强制切换模式"))
                    {
                        try { gm.CmdCycleGameMode(null); Plugin.LogSource.LogInfo("已发送强制切换模式命令"); } catch { }
                    }
                    if (GUI.Button(new Rect(590, y, 190, 26), "强制返回大厅"))
                    {
                        try { gm.CmdRequestResetToLobby(null); Plugin.LogSource.LogInfo("已发送强制返回大厅命令"); } catch { }
                    }
                }
                else
                {
                    GUI.Label(new Rect(170, y, 610, 22), "未找到 GameManager...");
                }
                y += 35f;

                // --- 左右分栏布局 ---
                // 左侧栏：玩家列表 & 地标传送 & NPC扑倒 (x: 170 to 450, w: 280)
                // 右侧栏：选中玩家的属性详情与单人操控面板 & 一键群体操控 (x: 470 to 785, w: 315)

                // 【左侧栏绘制】
                int maxTrollPages = (cachedPlayers.Count + 7) / 8;
                if (maxTrollPages < 1) maxTrollPages = 1;
                if (trollPlayerPage >= maxTrollPages) trollPlayerPage = maxTrollPages - 1;
                if (trollPlayerPage < 0) trollPlayerPage = 0;

                GUI.Label(new Rect(170, y, 160, 22), "<b>房间玩家 (" + (trollPlayerPage + 1) + "/" + maxTrollPages + ")</b>");
                if (GUI.Button(new Rect(340, y, 30, 20), "◀")) trollPlayerPage = Mathf.Max(0, trollPlayerPage - 1);
                if (GUI.Button(new Rect(375, y, 30, 20), "▶")) trollPlayerPage = Mathf.Min(maxTrollPages - 1, trollPlayerPage + 1);
                
                float listY = y + 25f;
                int startTrollIdx = trollPlayerPage * 8;
                for (int i = 0; i < 8; i++)
                {
                    int idx = startTrollIdx + i;
                    if (idx >= cachedPlayers.Count) break;
                    var p = cachedPlayers[idx];
                    if (!SafeCheckAlive(p)) continue;

                    string name = "(未知)";
                    try { name = p.playerName ?? p.gameObject.name; } catch { }

                    string roleTag = p.IsAgent ? "[警]" : "[匪]";
                    string authTag = p.isLocalPlayer ? "[我]" : (p.isHostPlayer ? "[房主]" : "[客机]");
                    string btnText = $"{name} {authTag}{roleTag}";

                    if (selectedTrollPlayer == p)
                    {
                        btnText = $"▶ {btnText} ◀";
                    }

                    if (GUI.Button(new Rect(170, listY, 280, 24), btnText))
                    {
                        selectedTrollPlayer = p;
                    }
                    listY += 26f;
                }

                // 左下角地标瞬移
                GUI.Label(new Rect(170, 320, 280, 20), "<b>== 本地玩家地标瞬移 ==</b>");
                if (GUI.Button(new Rect(170, 340, 135, 24), "前台/大厅"))
                {
                    Vector3 dest = GetLocationCoords("前台/大厅");
                    if (dest != Vector3.zero) Teleport(dest);
                    else Plugin.LogSource.LogWarning("未在当前关卡找到前台大厅交互点！");
                }
                if (GUI.Button(new Rect(315, 340, 135, 24), "安检休息室"))
                {
                    Vector3 dest = GetLocationCoords("休息室");
                    if (dest != Vector3.zero) Teleport(dest);
                    else Plugin.LogSource.LogWarning("未在当前关卡找到休息室交互点！");
                }
                if (GUI.Button(new Rect(170, 370, 135, 24), "登机口/飞机"))
                {
                    Vector3 dest = GetLocationCoords("登机口/飞机");
                    if (dest != Vector3.zero) Teleport(dest);
                    else Plugin.LogSource.LogWarning("未在当前关卡找到登机口飞机交互点！");
                }
                if (GUI.Button(new Rect(315, 370, 135, 24), "禁闭监狱"))
                {
                    Vector3 dest = GetLocationCoords("监狱");
                    if (dest != Vector3.zero) Teleport(dest);
                    else Plugin.LogSource.LogWarning("未在当前关卡找到监狱交互点！");
                }

                // 左侧NPC控制
                int maxNpcPages = (cachedNpcs.Count + 4) / 5;
                if (maxNpcPages < 1) maxNpcPages = 1;
                if (npcPage >= maxNpcPages) npcPage = maxNpcPages - 1;
                if (npcPage < 0) npcPage = 0;

                GUI.Label(new Rect(170, 405, 160, 20), "<b>场景 NPC (" + (npcPage + 1) + "/" + maxNpcPages + ")</b>");
                if (GUI.Button(new Rect(340, 405, 30, 20), "◀")) npcPage = Mathf.Max(0, npcPage - 1);
                if (GUI.Button(new Rect(375, 405, 30, 20), "▶")) npcPage = Mathf.Min(maxNpcPages - 1, npcPage + 1);

                float npcY = 430f;
                int startNpcIdx = npcPage * 5;
                for (int i = 0; i < 5; i++)
                {
                    int idx = startNpcIdx + i;
                    if (idx >= cachedNpcs.Count) break;
                    var n = cachedNpcs[idx];
                    if (!SafeCheckAlive(n)) continue;

                    string nname = "NPC #" + n.GetInstanceID();
                    float dist = 0f;
                    if (localMeta != null) dist = Vector3.Distance(localMeta.transform.position, n.transform.position);

                    GUI.Label(new Rect(170, npcY, 150, 22), nname + " [" + dist.ToString("0") + "m]");
                    if (GUI.Button(new Rect(325, npcY, 60, 22), "扑倒")) TackleNpc(n);
                    if (GUI.Button(new Rect(390, npcY, 60, 22), "传送")) Teleport(n.transform.position + Vector3.up * 1f);
                    npcY += 24f;
                }

                // 【右侧栏绘制】
                if (selectedTrollPlayer == null || !SafeCheckAlive(selectedTrollPlayer))
                {
                    GUI.Box(new Rect(470, 80, 315, 490), "");
                    GUI.Label(new Rect(485, 90, 290, 20), "<b>【 未选择单人目标 】</b>");
                    GUI.Label(new Rect(485, 115, 290, 40), "<color=yellow>请在左侧选择玩家进行单人控制。\n或使用下方的一键群体恶搞功能：</color>");

                    DrawGroupActionsPanel(165f);
                }
                else
                {
                    GUI.Box(new Rect(470, 80, 315, 490), "");
                    float detailsY = 90f;
                    GUI.Label(new Rect(485, detailsY, 290, 20), "<b>【 目标玩家详情 】</b>"); detailsY += 22f;

                    string tName = "(未知)";
                    int tPing = 0;
                    int tMoney = 0;
                    bool tIsAgent = false;
                    bool tIsHost = false;
                    bool tIsLocal = false;
                    try
                    {
                        tName = selectedTrollPlayer.playerName;
                        tPing = selectedTrollPlayer.pingMs;
                        tMoney = selectedTrollPlayer.syncedMoney;
                        tIsAgent = selectedTrollPlayer.IsAgent;
                        tIsHost = selectedTrollPlayer.isHostPlayer;
                        tIsLocal = selectedTrollPlayer.isLocalPlayer;
                    }
                    catch { }

                    string roleStr = tIsAgent ? "<color=cyan>警卫 / 警察</color>" : "<color=orange>走私犯 / 平民</color>";
                    string authStr = tIsLocal ? "本地玩家 (自己)" : (tIsHost ? "房主 (Host)" : "客机 (Client)");

                    GUI.Label(new Rect(485, detailsY, 290, 20), $"玩家姓名: <b>{tName}</b>"); detailsY += 20f;
                    GUI.Label(new Rect(485, detailsY, 290, 20), $"身份阵营: <b>{roleStr}</b>"); detailsY += 20f;
                    GUI.Label(new Rect(485, detailsY, 290, 20), $"房间权限: <b>{authStr}</b>"); detailsY += 20f;
                    GUI.Label(new Rect(485, detailsY, 290, 20), $"网络延迟: <b>{tPing} ms</b>"); detailsY += 20f;
                    GUI.Label(new Rect(485, detailsY, 290, 20), $"携带资金: <b>$ {tMoney:N0}</b>"); detailsY += 25f;

                    GUI.Label(new Rect(485, detailsY, 290, 20), "<b>【 远程恶搞与操控指令 】</b>"); detailsY += 25f;

                    if (GUI.Button(new Rect(485, detailsY, 135, 26), "瞬移到他"))
                    {
                        if (selectedTrollPlayer.transform != null)
                            Teleport(selectedTrollPlayer.transform.position + Vector3.up * 1f);
                    }
                    if (GUI.Button(new Rect(635, detailsY, 135, 26), "拉他过来"))
                    {
                        PullPlayer(selectedTrollPlayer);
                    }
                    detailsY += 30f;

                    if (GUI.Button(new Rect(485, detailsY, 135, 26), "远程监禁"))
                    {
                        JailPlayer(selectedTrollPlayer);
                    }
                    if (GUI.Button(new Rect(635, detailsY, 135, 26), "远程扑倒"))
                    {
                        TacklePlayer(selectedTrollPlayer);
                    }
                    detailsY += 30f;

                    // 第三排 (三分列)
                    if (GUI.Button(new Rect(485, detailsY, 90, 26), "强行放倒"))
                    {
                        RagdollPlayer(selectedTrollPlayer);
                    }
                    bool isLocked = infiniteRagdollList.Contains(selectedTrollPlayer);
                    if (GUI.Button(new Rect(580, detailsY, 95, 26), (isLocked ? "[锁]" : "[开]") + "锁定倒地"))
                    {
                        if (isLocked)
                        {
                            infiniteRagdollList.Remove(selectedTrollPlayer);
                            Plugin.LogSource.LogInfo("已移除对 " + selectedTrollPlayer.playerName + " 的无限倒地锁定");
                        }
                        else
                        {
                            infiniteRagdollList.Add(selectedTrollPlayer);
                            Plugin.LogSource.LogInfo("已开启对 " + selectedTrollPlayer.playerName + " 的无限倒地锁定");
                        }
                    }
                    if (GUI.Button(new Rect(680, detailsY, 90, 26), "远程自爆"))
                    {
                        ExplodePlayer(selectedTrollPlayer);
                    }
                    detailsY += 30f;

                    // 第四排 (踢出/排泄)
                    if (GUI.Button(new Rect(485, detailsY, 135, 26), "物理踢出"))
                    {
                        KickPlayer(selectedTrollPlayer);
                    }
                    if (GUI.Button(new Rect(635, detailsY, 135, 26), "屁股排泄"))
                    {
                        DumpPlayerButt(selectedTrollPlayer);
                    }
                    detailsY += 30f;

                    // 第五排 (阵营设置与改名，三分列)
                    if (GUI.Button(new Rect(485, detailsY, 90, 26), "设为警卫"))
                    {
                        if (localPlayerName != null && selectedTrollPlayer != null && selectedTrollPlayer.PlayerModeManager != null)
                        {
                            var localTeleporter = localPlayerName.GetComponent<PlayerTeleporter>();
                            if (localTeleporter == null) localTeleporter = localPlayerName.GetComponentInChildren<PlayerTeleporter>();
                            if (localTeleporter == null) localTeleporter = localPlayerName.GetComponentInParent<PlayerTeleporter>();

                            if (localTeleporter != null)
                            {
                                try
                                {
                                    localTeleporter.CmdSetIsAgent(selectedTrollPlayer.PlayerModeManager, true);
                                    Plugin.LogSource.LogInfo($"已远程越权将 {selectedTrollPlayer.playerName} 设为警卫阵营！");
                                }
                                catch (System.Exception ex)
                                {
                                    Plugin.LogSource.LogError("CmdSetIsAgent 异常: " + ex.Message);
                                }
                            }
                        }
                    }
                    if (GUI.Button(new Rect(580, detailsY, 95, 26), "设为平民"))
                    {
                        if (localPlayerName != null && selectedTrollPlayer != null && selectedTrollPlayer.PlayerModeManager != null)
                        {
                            var localTeleporter = localPlayerName.GetComponent<PlayerTeleporter>();
                            if (localTeleporter == null) localTeleporter = localPlayerName.GetComponentInChildren<PlayerTeleporter>();
                            if (localTeleporter == null) localTeleporter = localPlayerName.GetComponentInParent<PlayerTeleporter>();

                            if (localTeleporter != null)
                            {
                                try
                                {
                                    localTeleporter.CmdSetIsAgent(selectedTrollPlayer.PlayerModeManager, false);
                                    Plugin.LogSource.LogInfo($"已远程越权将 {selectedTrollPlayer.playerName} 设为平民阵营！");
                                }
                                catch (System.Exception ex)
                                {
                                    Plugin.LogSource.LogError("CmdSetIsAgent 异常: " + ex.Message);
                                }
                            }
                        }
                    }
                    if (GUI.Button(new Rect(680, detailsY, 90, 26), "伪装名字"))
                    {
                        if (localPlayerName != null)
                        {
                            try
                            {
                                if (!isNameSpoofed)
                                {
                                    originalPlayerName = localPlayerName.playerName;
                                    isNameSpoofed = true;
                                }
                                nameRestoreTime = float.MaxValue;
                                ulong steamIdToUse = localPlayerName.steamId;
                                if (enableAntiKick)
                                {
                                    ulong hostId = GetHostSteamId();
                                    if (hostId != 0) steamIdToUse = hostId;
                                }
                                localPlayerName.CmdSetPlayerName(tName, steamIdToUse);
                                Plugin.LogSource.LogInfo($"已手动伪装名称为: {tName}，原名为: {originalPlayerName}");
                            }
                            catch (System.Exception ex)
                            {
                                Plugin.LogSource.LogError("手动伪装名字失败: " + ex.Message);
                            }
                        }
                    }
                    detailsY += 30f;

                    if (isNameSpoofed && nameRestoreTime == float.MaxValue)
                    {
                        if (GUI.Button(new Rect(485, detailsY, 285, 26), "恢复原名"))
                        {
                            if (localPlayerName != null && !string.IsNullOrEmpty(originalPlayerName))
                            {
                                try
                                {
                                    ulong steamIdToUse = localPlayerName.steamId;
                                    if (enableAntiKick)
                                    {
                                        ulong hostId = GetHostSteamId();
                                        if (hostId != 0) steamIdToUse = hostId;
                                    }
                                    localPlayerName.CmdSetPlayerName(originalPlayerName, steamIdToUse);
                                    Plugin.LogSource.LogInfo($"已手动恢复真实姓名: {originalPlayerName}");
                                }
                                catch (System.Exception ex)
                                {
                                    Plugin.LogSource.LogError("手动恢复真实姓名失败: " + ex.Message);
                                }
                            }
                            isNameSpoofed = false;
                            originalPlayerName = "";
                        }
                    }

                    // 选中状态下，群体一键面板绘制在下方空余位置 (Details + actions takes about ~300px, startY at 385)
                    DrawGroupActionsPanel(385f);
                }
            }
        }

        private void DrawGroupActionsPanel(float startY)
        {
            GUI.Label(new Rect(485, startY, 290, 20), "<b>【 团队群体恶搞 (Group Actions) 】</b>"); startY += 22f;

            // Header labels
            GUI.Label(new Rect(485, startY, 100, 20), "一键操作:");
            GUI.Label(new Rect(590, startY, 90, 20), "<b><color=cyan>【警卫组】</color></b>");
            GUI.Label(new Rect(685, startY, 90, 20), "<b><color=orange>【走私犯】</color></b>");
            startY += 20f;

            // Row 1: 一键监禁
            GUI.Label(new Rect(485, startY, 100, 22), "一键监禁");
            if (GUI.Button(new Rect(585, startY, 90, 22), "监禁")) ExecuteGroupAction(true, "jail");
            if (GUI.Button(new Rect(680, startY, 90, 22), "监禁")) ExecuteGroupAction(false, "jail");
            startY += 24f;

            // Row 2: 一键扑倒
            GUI.Label(new Rect(485, startY, 100, 22), "一键扑倒");
            if (GUI.Button(new Rect(585, startY, 90, 22), "扑倒")) ExecuteGroupAction(true, "tackle");
            if (GUI.Button(new Rect(680, startY, 90, 22), "扑倒")) ExecuteGroupAction(false, "tackle");
            startY += 24f;

            // Row 3: 一键放倒
            GUI.Label(new Rect(485, startY, 100, 22), "一键放倒");
            if (GUI.Button(new Rect(585, startY, 90, 22), "放倒")) ExecuteGroupAction(true, "ragdoll");
            if (GUI.Button(new Rect(680, startY, 90, 22), "放倒")) ExecuteGroupAction(false, "ragdoll");
            startY += 24f;

            // 新增: 一键锁定倒地
            GUI.Label(new Rect(485, startY, 100, 22), "一键锁定倒地");
            if (GUI.Button(new Rect(585, startY, 90, 22), "锁定(警)")) LockGroupRagdoll(true);
            if (GUI.Button(new Rect(680, startY, 90, 22), "锁定(匪)")) LockGroupRagdoll(false);
            startY += 24f;

            // Row 4: 一键自爆
            GUI.Label(new Rect(485, startY, 100, 22), "一键自爆");
            if (GUI.Button(new Rect(585, startY, 90, 22), "自爆")) ExecuteGroupAction(true, "explode");
            if (GUI.Button(new Rect(680, startY, 90, 22), "自爆")) ExecuteGroupAction(false, "explode");
            startY += 24f;

            // Row 5: 一键踢出
            GUI.Label(new Rect(485, startY, 100, 22), "一键踢出");
            if (GUI.Button(new Rect(585, startY, 90, 22), "物理踢出")) ExecuteGroupAction(true, "kick");
            if (GUI.Button(new Rect(680, startY, 90, 22), "物理踢出")) ExecuteGroupAction(false, "kick");
            startY += 24f;

            // Row 6: 一键排泄
            bool isHost = localPlayerName != null && localPlayerName.isServer;
            GUI.Label(new Rect(485, startY, 100, 22), isHost ? "一键排泄" : "一键排泄(仅房主)");
            if (GUI.Button(new Rect(585, startY, 90, 22), "排泄"))
            {
                if (isHost) ExecuteGroupAction(true, "dump");
                else Plugin.LogSource.LogWarning("一键强制他人排泄仅限房主(Host)有效！");
            }
            if (GUI.Button(new Rect(680, startY, 90, 22), "排泄"))
            {
                if (isHost) ExecuteGroupAction(false, "dump");
                else Plugin.LogSource.LogWarning("一键强制他人排泄仅限房主(Host)有效！");
            }
            startY += 28f;

            if (GUI.Button(new Rect(485, startY, 285, 24), "【 一键解除全场倒地锁定 】"))
            {
                infiniteRagdollList.Clear();
                Plugin.LogSource.LogInfo("已清除全场所有倒地锁定！");
            }
            startY += 28f;
        }

    }

}
