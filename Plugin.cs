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
    public static class PluginInfo
    {
        public const string PLUGIN_GUID = "com.airport.hack.super";
        public const string PLUGIN_NAME = "AirportSecuritySuperHack";
        public const string PLUGIN_VERSION = "5.1.0";
    }

    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BasePlugin
    {
        public static ManualLogSource LogSource;

        public override void Load()
        {
            LogSource = Log;
            Log.LogInfo("[超神辅助 v5.1] 插件加载中...");

            ClassInjector.RegisterTypeInIl2Cpp<HackController>();
            var go = new GameObject("SuperAirportHackManager");
            go.AddComponent<HackController>();
            GameObject.DontDestroyOnLoad(go);

            try
            {
                var harmony = new HarmonyLib.Harmony(PluginInfo.PLUGIN_GUID);
                
                // 反射挂钩房主的踢人断开事件
                var assembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
                var targetType = assembly?.GetType("SteamMatchmakingTest");
                if (targetType != null)
                {
                    var targetMethod = targetType.GetMethod("OnLobbyKicked", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                    if (targetMethod != null)
                    {
                        var prefix = typeof(Plugin).GetMethod(nameof(OnLobbyKickedPrefix), BindingFlags.Static | BindingFlags.Public);
                        harmony.Patch(targetMethod, prefix: new HarmonyMethod(prefix));
                        Log.LogInfo("[防踢] 成功挂钩 Lobby 踢出回调方法！");
                    }
                    else
                    {
                        Log.LogError("[防踢] 未找到 SteamMatchmakingTest.OnLobbyKicked 方法！");
                    }
                }
                else
                {
                    Log.LogError("[防踢] 未找到 SteamMatchmakingTest 类型！");
                }

                harmony.PatchAll(Assembly.GetExecutingAssembly());
                Log.LogInfo("[超神辅助 v5.1] Harmony 补丁注入成功！");
            }
            catch (System.Exception ex)
            {
                Log.LogError("Harmony 补丁注入失败: " + ex.Message);
            }

            Log.LogInfo("[超神辅助 v5.1] 插件加载完成。Insert = 菜单, F1 = 飞行穿墙");
        }

        public static bool OnLobbyKickedPrefix()
        {
            LogSource.LogWarning("[防踢] 拦截到房主踢出消息 Steam Lobby Kicked！已成功阻止退房。");
            return false; // 拦截原逻辑，拒绝退出游戏房间
        }
    }

    // ===== Harmony 补丁：移动控制 =====
    [HarmonyPatch(typeof(MetaPlayerController), nameof(MetaPlayerController.Move))]
    public static class MetaPlayerController_Move_Patch
    {
        public static bool Prefix(bool jumpedThisFrame)
        {
            if (HackController.Instance != null && HackController.Instance.enableFly)
            {
                return false; // 飞行开启时，跳过原本的运动结算
            }
            return true;
        }
    }

    // ===== Harmony 补丁：速度修改 =====
    [HarmonyPatch(typeof(MetaPlayerConfig), nameof(MetaPlayerConfig.walkSpeed), MethodType.Getter)]
    public static class MetaPlayerConfig_WalkSpeed_Patch
    {
        public static void Postfix(ref float __result)
        {
            if (HackController.Instance != null && HackController.Instance.enableSpeedHack)
            {
                __result *= HackController.Instance.speedMultiplier;
            }
        }
    }

    [HarmonyPatch(typeof(MetaPlayerConfig), nameof(MetaPlayerConfig.sprintSpeed), MethodType.Getter)]
    public static class MetaPlayerConfig_SprintSpeed_Patch
    {
        public static void Postfix(ref float __result)
        {
            if (HackController.Instance != null && HackController.Instance.enableSpeedHack)
            {
                __result *= HackController.Instance.speedMultiplier;
            }
        }
    }

    // ===== Harmony 补丁：强杀进程退出 =====
    [HarmonyPatch(typeof(UnityEngine.Application), nameof(UnityEngine.Application.Quit), new System.Type[] { })]
    public static class Application_Quit_Patch
    {
        public static bool Prefix()
        {
            Plugin.LogSource.LogInfo("[ExitPatch] Harmony 拦截到 Application.Quit()，正在强制杀死进程防止卡死...");
            try
            {
                System.Diagnostics.Process.GetCurrentProcess().Kill();
            }
            catch
            {
                System.Environment.Exit(0);
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(UnityEngine.Application), nameof(UnityEngine.Application.Quit), new System.Type[] { typeof(int) })]
    public static class Application_QuitWithCode_Patch
    {
        public static bool Prefix(int exitCode)
        {
            Plugin.LogSource.LogInfo("[ExitPatch] Harmony 拦截到 Application.Quit(exitCode)，正在强制杀死进程...");
            try
            {
                System.Diagnostics.Process.GetCurrentProcess().Kill();
            }
            catch
            {
                System.Environment.Exit(exitCode);
            }
            return false;
        }
    }

    public class HackController : MonoBehaviour
    {
        public HackController(System.IntPtr ptr) : base(ptr) { }

        public static HackController Instance;

        // ===== UI 状态 =====
        private bool showMenu = true;
        private Rect windowRect = new Rect(80, 80, 800, 600);
        private string spawnFilter = "";
        private string moneyAmount = "50000";
        private string winsAmount = "100";
        private string newPlayerName = "迈克尔·蕉克逊";
        private int currentTab = 0;
        private int playerPage = 0;
        private int spawnPage = 0;
        private int vehiclePage = 0;
        private int trollPlayerPage = 0;
        private int npcPage = 0;
        private bool isDragging = false;
        private Vector2 dragOffset = Vector2.zero;
        private bool enableNameSpam = false;
        private float nextNameSpamTime = 0f;

        // ===== 飞行 =====
        public bool enableFly = false;
        private float flySpeed = 15f;
        private bool savedCharCtrlEnabled = true;

        // ===== 移速 / 跳跃 =====
        public bool enableSpeedHack = false;
        public float speedMultiplier = 2.5f;
        public bool enableSuperJump = false;
        private float jumpMultiplier = 3.0f;

        // ===== ESP =====
        private bool espPlayer = true;
        private bool espContraband = true;
        private bool espShowDistance = true;
        private bool espBoxes = true;
        private bool espTracers = false;
        private float espMaxDistance = 200f;

        // ===== 自动功能 =====
        private bool autoExposeContraband = false;

        // ===== 缓存 =====
        private float nextCheckTime = 0f;
        private System.Collections.Generic.List<PlayerName> cachedPlayers = new System.Collections.Generic.List<PlayerName>();
        private System.Collections.Generic.List<Contraband> cachedContrabands = new System.Collections.Generic.List<Contraband>();
        private System.Collections.Generic.List<SegwayInteractable> cachedSegways = new System.Collections.Generic.List<SegwayInteractable>();
        private System.Collections.Generic.List<NpcRagdollManager> cachedNpcs = new System.Collections.Generic.List<NpcRagdollManager>();
        private Il2CppSystem.Collections.Generic.List<GameObject> spawnablePrefabs = null;

        // 本地玩家引用
        private MetaPlayer localMeta;
        private PlayerName localPlayerName;
        private uint autoBypassProbeNetId = 0;
        private float nextAutoBypassProbeTime = 0f;

        private void Awake()
        {
            Instance = this;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Insert)) showMenu = !showMenu;
            if (Input.GetKeyDown(KeyCode.F1)) ToggleFly();

            UpdateLocalRefs();
            RunAutoBypassProbe();

            // 菜单拖拽逻辑（直接在 Update 中基于 Input 获取，完全避开 DragWindow 异常）
            if (showMenu)
            {
                try
                {
                    Vector3 mousePos = Input.mousePosition;
                    Vector2 guiMousePos = new Vector2(mousePos.x, Screen.height - mousePos.y);

                    if (Input.GetMouseButtonDown(0))
                    {
                        // 检测是否点在标题栏上（顶部 25 像素高度）
                        Rect titleRect = new Rect(windowRect.x, windowRect.y, windowRect.width, 25f);
                        if (titleRect.Contains(guiMousePos))
                        {
                            isDragging = true;
                            dragOffset = guiMousePos - new Vector2(windowRect.x, windowRect.y);
                        }
                    }

                    if (Input.GetMouseButton(0) && isDragging)
                    {
                        windowRect.x = guiMousePos.x - dragOffset.x;
                        windowRect.y = guiMousePos.y - dragOffset.y;
                    }
                    else
                    {
                        isDragging = false;
                    }
                }
                catch { }
            }

            // 动态应用跳跃与重力系数
            try
            {
                if (localMeta != null && localMeta.gameObject != null)
                {
                    var ctrl = localMeta.controller != null ? localMeta.controller : localMeta.playerController;
                    if (ctrl != null && ctrl.config != null)
                    {
                        // 超级跳跃
                        if (enableSuperJump)
                        {
                            ctrl.config.JumpHeight = ctrl.config.nominalJumpHeight * jumpMultiplier;
                        }
                        else
                        {
                            ctrl.config.JumpHeight = ctrl.config.nominalJumpHeight;
                        }

                        // 飞行无引力
                        if (enableFly)
                        {
                            ctrl.config.Gravity = 0f;
                        }
                        else
                        {
                            ctrl.config.Gravity = ctrl.config.nominalGravity;
                        }
                    }
                }
            }
            catch
            {
                localMeta = null;
                localPlayerName = null;
            }

            if (enableFly) HandleFly();

            if (Time.time > nextCheckTime)
            {
                UpdateTargets();
                nextCheckTime = Time.time + 1.0f;
            }

            if (autoExposeContraband)
            {
                foreach (var c in cachedContrabands)
                {
                    try
                    {
                        if (c != null && c.gameObject != null && c.contrabandRedOutline != null)
                        {
                            c.contrabandRedOutline.enabled = true;
                        }
                    }
                    catch { }
                }
            }

            // 循环随机乱码改名逻辑
            if (enableNameSpam && localPlayerName != null && Time.time > nextNameSpamTime)
            {
                nextNameSpamTime = Time.time + 0.15f;
                try
                {
                    string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$";
                    System.Random rand = new System.Random();
                    string randName = "";
                    for (int i = 0; i < 8; i++) randName += chars[rand.Next(chars.Length)];
                    localPlayerName.CmdSetPlayerName(randName, localPlayerName.steamId);
                }
                catch { }
            }
        }

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
        private bool SafeCheckAlive(UnityEngine.Object obj)
        {
            if (obj == null) return false;
            try
            {
                string name = obj.name;
                return true;
            }
            catch
            {
                return false;
            }
        }

        // ===== 获取本地玩家 =====
        private void UpdateLocalRefs()
        {
            try
            {
                if (localMeta == null || localMeta.gameObject == null)
                {
                    localMeta = MetaPlayer._LocalPlayerInstance_k__BackingField;
                }
                if (localPlayerName == null || !SafeIsLocal(localPlayerName) || localPlayerName.gameObject == null)
                {
                    var arr = UnityEngine.Object.FindObjectsOfType<PlayerName>();
                    foreach (var p in arr)
                    {
                        if (p == null || p.gameObject == null) continue;
                        if (SafeIsLocal(p)) { localPlayerName = p; break; }
                    }
                    if (localPlayerName == null && localMeta != null && localMeta.gameObject != null)
                    {
                        localPlayerName = localMeta.GetComponent<PlayerName>();
                        if (localPlayerName == null) localPlayerName = localMeta.GetComponentInParent<PlayerName>();
                        if (localPlayerName == null) localPlayerName = localMeta.GetComponentInChildren<PlayerName>();
                    }
                }
            }
            catch
            {
                localMeta = null;
                localPlayerName = null;
            }
        }

        private bool SafeIsLocal(PlayerName p)
        {
            try { return p.isLocalPlayer; } catch { return false; }
        }

        private void RunAutoBypassProbe()
        {
            try
            {
                if (localPlayerName == null || localPlayerName.gameObject == null || Time.time < nextAutoBypassProbeTime) return;
                nextAutoBypassProbeTime = Time.time + 3f;

                uint netId = localPlayerName.netId;
                bool isServer = localPlayerName.isServer;
                if (netId == 0 || autoBypassProbeNetId == netId) return;
                if (!SafeIsLocal(localPlayerName)) return;
                
                autoBypassProbeNetId = netId;
                Plugin.LogSource.LogInfo("[AutoProbe] 玩家就绪。netId=" + netId + ", isHost=" + isServer);
            }
            catch { }
        }

        private void ToggleFly()
        {
            enableFly = !enableFly;
            if (localMeta == null) UpdateLocalRefs();
            if (localMeta == null) { Plugin.LogSource.LogWarning("找不到本地玩家，飞行无效"); enableFly = false; return; }
            try
            {
                var ctrl = localMeta.controller != null ? localMeta.controller : localMeta.playerController;
                if (ctrl == null) { Plugin.LogSource.LogWarning("找不到 MetaPlayerController"); enableFly = false; return; }
                
                if (enableFly)
                {
                    if (ctrl.characterController != null)
                    {
                        savedCharCtrlEnabled = ctrl.characterController.enabled;
                        ctrl.characterController.enabled = false;
                    }
                    ctrl.velocityY = 0f;
                    Plugin.LogSource.LogInfo("[飞行] 已开启 (穿墙飞行)");
                }
                else
                {
                    if (ctrl.characterController != null)
                    {
                        ctrl.characterController.enabled = savedCharCtrlEnabled;
                    }
                    ctrl.velocityY = 0f;
                    Plugin.LogSource.LogInfo("[飞行] 已关闭");
                }
            }
            catch (System.Exception e) { Plugin.LogSource.LogError("ToggleFly 异常: " + e.Message); enableFly = false; }
        }

        private void HandleFly()
        {
            try
            {
                if (localMeta == null || localMeta.gameObject == null) return;
                Camera cam = Camera.main;
                if (cam == null) return;
                Transform camT = cam.transform;
                Transform playerT = localMeta.transform;
                float speed = flySpeed;
                if (Input.GetKey(KeyCode.LeftShift)) speed *= 3f;

                Vector3 move = Vector3.zero;
                if (Input.GetKey(KeyCode.W)) move += camT.forward;
                if (Input.GetKey(KeyCode.S)) move -= camT.forward;
                if (Input.GetKey(KeyCode.A)) move -= camT.right;
                if (Input.GetKey(KeyCode.D)) move += camT.right;
                if (Input.GetKey(KeyCode.Space)) move += Vector3.up;
                if (Input.GetKey(KeyCode.LeftControl)) move -= Vector3.up;
                
                if (move.sqrMagnitude > 0.001f)
                {
                    playerT.position += move.normalized * speed * Time.deltaTime;
                }
            }
            catch { }
        }

        private void Teleport(Vector3 dest)
        {
            if (localMeta == null) { Plugin.LogSource.LogWarning("找不到本地玩家，传送失败"); return; }
            try
            {
                var ctrl = localMeta.controller != null ? localMeta.controller : localMeta.playerController;
                if (ctrl != null)
                {
                    ctrl.Teleport(dest, true);
                    Plugin.LogSource.LogInfo("高平滑传送至 " + dest);
                }
                else
                {
                    localMeta.transform.position = dest;
                    Plugin.LogSource.LogInfo("基础坐标传送至 " + dest);
                }
            }
            catch (System.Exception e) { Plugin.LogSource.LogError("Teleport 异常: " + e.Message); }
        }

        private bool IsValidEntity(GameObject go)
        {
            if (go == null) return false;
            try
            {
                if (!go.activeInHierarchy) return false;
                Vector3 p = go.transform.position;
                if (p.y < -500f || p.y > 1000f) return false;
                return true;
            }
            catch { return false; }
        }

        private void UpdateTargets()
        {
            try
            {
                cachedPlayers.Clear();
                var players = UnityEngine.Object.FindObjectsOfType<PlayerName>();
                foreach (var p in players)
                {
                    try
                    {
                        if (p == null || p.gameObject == null) continue;
                        if (!IsValidEntity(p.gameObject)) continue;
                        bool real = false;
                        if (p.steamId != 0UL) real = true;
                        else if (p.isHostPlayer || p.isCarsonLocal) real = true;
                        if (!real) continue;
                        cachedPlayers.Add(p);
                    }
                    catch { }
                }
            }
            catch (System.Exception e) { Plugin.LogSource.LogWarning("扫描玩家异常: " + e.Message); }

            try
            {
                cachedContrabands.Clear();
                var contrabands = UnityEngine.Object.FindObjectsOfType<Contraband>();
                foreach (var c in contrabands)
                {
                    try
                    {
                        if (c == null || c.gameObject == null) continue;
                        if (!IsValidEntity(c.gameObject)) continue;
                        bool isCB = false;
                        try { isCB = c.isContraband; } catch { }
                        if (!isCB) continue;
                        cachedContrabands.Add(c);
                    }
                    catch { }
                }
            }
            catch (System.Exception e) { Plugin.LogSource.LogWarning("扫描违禁品异常: " + e.Message); }

            try
            {
                cachedSegways.Clear();
                var segways = UnityEngine.Object.FindObjectsOfType<SegwayInteractable>();
                foreach (var s in segways)
                {
                    try
                    {
                        if (s == null || s.gameObject == null) continue;
                        if (!IsValidEntity(s.gameObject)) continue;
                        cachedSegways.Add(s);
                    }
                    catch { }
                }
            }
            catch (System.Exception e) { Plugin.LogSource.LogWarning("扫描平衡车异常: " + e.Message); }

            try
            {
                cachedNpcs.Clear();
                var npcs = UnityEngine.Object.FindObjectsOfType<NpcRagdollManager>();
                foreach (var n in npcs)
                {
                    try
                    {
                        if (n == null || n.gameObject == null) continue;
                        if (!IsValidEntity(n.gameObject)) continue;
                        cachedNpcs.Add(n);
                    }
                    catch { }
                }
            }
            catch (System.Exception e) { Plugin.LogSource.LogWarning("扫描NPC异常: " + e.Message); }
        }

        private void DrawMenu(int windowID)
        {
            try
            {
                // 用背景框隔离开侧边栏和主控制区域 (改为 800x600 布局)
                GUI.Box(new Rect(0, 0, 150, 600), ""); // 侧边栏背景
                GUI.Box(new Rect(150, 0, 650, 600), ""); // 右侧背景

                // 侧边栏 Logo 与状态
                GUI.Label(new Rect(15, 20, 120, 20), "<b>⚡ VAPE</b>");
                GUI.Label(new Rect(15, 38, 120, 15), "SUPER CHEAT v5.1");

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
                GUI.Label(new Rect(170, y, 610, 20), "<b>== 透视与玩家设置 ==</b>"); y += 30f;

                if (GUI.Button(new Rect(170, y, 290, 28), (espPlayer ? "[ ON ]" : "[ OFF ]") + " 透视玩家")) espPlayer = !espPlayer;
                if (GUI.Button(new Rect(480, y, 290, 28), (espBoxes ? "[ ON ]" : "[ OFF ]") + " ESP 方框 (Boxes)")) espBoxes = !espBoxes;
                y += 32f;
                if (GUI.Button(new Rect(170, y, 290, 28), (espTracers ? "[ ON ]" : "[ OFF ]") + " ESP 射线 (Tracers)")) espTracers = !espTracers;
                if (GUI.Button(new Rect(480, y, 290, 28), (espShowDistance ? "[ ON ]" : "[ OFF ]") + " 显示距离")) espShowDistance = !espShowDistance;
                y += 32f;

                GUI.Label(new Rect(170, y, 200, 28), "透视最大范围: " + espMaxDistance.ToString("0") + "m");
                if (GUI.Button(new Rect(380, y, 40, 28), "-")) espMaxDistance = Mathf.Max(20f, espMaxDistance - 20f);
                if (GUI.Button(new Rect(430, y, 40, 28), "+")) espMaxDistance = Mathf.Min(500f, espMaxDistance + 20f);
                y += 36f;

                // 玩家列表分页（支持 6 名玩家，排布极度宽松）
                int maxPages = (cachedPlayers.Count + 5) / 6;
                if (maxPages < 1) maxPages = 1;
                if (playerPage >= maxPages) playerPage = maxPages - 1;
                if (playerPage < 0) playerPage = 0;

                GUI.Label(new Rect(170, y, 200, 25), "<b>房间玩家 (页 " + (playerPage + 1) + "/" + maxPages + ")</b>");
                if (GUI.Button(new Rect(380, y, 40, 22), "◀")) playerPage = Mathf.Max(0, playerPage - 1);
                if (GUI.Button(new Rect(430, y, 40, 22), "▶")) playerPage = Mathf.Min(maxPages - 1, playerPage + 1);
                y += 28f;

                int startIndex = playerPage * 6;
                for (int i = 0; i < 6; i++)
                {
                    int idx = startIndex + i;
                    if (idx >= cachedPlayers.Count) break;
                    var p = cachedPlayers[idx];
                    if (!SafeCheckAlive(p)) continue;
                    
                    string name = "(未知)"; int money = 0;
                    try { name = p.playerName ?? p.gameObject.name; } catch { }
                    try { money = p.syncedMoney; } catch { }

                    GUI.Label(new Rect(170, y, 200, 26), name + " | $" + money);
                    
                    if (GUI.Button(new Rect(380, y, 60, 26), "瞬移"))
                    {
                        if (p.transform != null) Teleport(p.transform.position + Vector3.up * 1f);
                    }
                    if (GUI.Button(new Rect(450, y, 60, 26), "拉人"))
                    {
                        PullPlayer(p);
                    }
                    if (GUI.Button(new Rect(520, y, 60, 26), "伪装"))
                    {
                        if (localPlayerName != null)
                        {
                            try
                            {
                                localPlayerName.CmdSetPlayerName(name, localPlayerName.steamId);
                                newPlayerName = name;
                                Plugin.LogSource.LogInfo("已伪装名称为: " + name);
                            }
                            catch (System.Exception ex)
                            {
                                Plugin.LogSource.LogError("伪装名称失败: " + ex.Message);
                            }
                        }
                    }
                    if (GUI.Button(new Rect(590, y, 60, 26), "监禁"))
                    {
                        JailPlayer(p);
                    }
                    if (GUI.Button(new Rect(660, y, 60, 26), "扑倒"))
                    {
                        TacklePlayer(p);
                    }
                    y += 30f;
                }
            }
            else if (currentTab == 2)
            {
                GUI.Label(new Rect(170, y, 610, 20), "<b>== 违禁品与生成设置 ==</b>"); y += 30f;

                if (GUI.Button(new Rect(170, y, 610, 30), (espContraband ? "[ ON ]" : "[ OFF ]") + " 违禁品透视 (ESP)")) espContraband = !espContraband; y += 35f;
                if (GUI.Button(new Rect(170, y, 610, 30), (autoExposeContraband ? "[ ON ]" : "[ OFF ]") + " 自动标记违禁品 Outline")) autoExposeContraband = !autoExposeContraband; y += 35f;
                if (GUI.Button(new Rect(170, y, 610, 30), "手动标记所有红框"))
                {
                    int n = 0;
                    foreach (var c in cachedContrabands)
                    {
                        if (!SafeCheckAlive(c)) continue;
                        try { if (c.contrabandRedOutline != null) { c.contrabandRedOutline.enabled = true; n++; } } catch { }
                    }
                    Plugin.LogSource.LogInfo("标记Outline数量: " + n);
                }
                y += 35f;

                if (GUI.Button(new Rect(170, y, 140, 28), "释放警犬")) ReleaseAllDogs();
                if (GUI.Button(new Rect(320, y, 140, 28), "贩卖机出货")) VendAllMachines();
                if (GUI.Button(new Rect(470, y, 140, 28), "互动愿望单")) InteractWishlist();
                if (GUI.Button(new Rect(620, y, 160, 28), "生娃 (生成NPC)")) SpawnBabyNpc();
                y += 32f;

                if (GUI.Button(new Rect(170, y, 140, 28), "引爆所有C4")) DetonateAllC4();
                if (GUI.Button(new Rect(320, y, 140, 28), "呼叫所有电梯")) SummonAllElevators();
                if (GUI.Button(new Rect(470, y, 140, 28), "开启休息室门")) TriggerBreakRoomDoors(true);
                if (GUI.Button(new Rect(620, y, 160, 28), "一键拉响警报")) TriggerLockdown();
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
                    if (localPlayerName != null) { try { localPlayerName.CmdSetPlayerName(newPlayerName, localPlayerName.steamId); } catch { } }
                }
                y += 30f;

                if (GUI.Button(new Rect(170, y, 610, 30), (enableNameSpam ? "[ ON ]" : "[ OFF ]") + " 循环随机乱码改名 (防踢/防选择)"))
                {
                    enableNameSpam = !enableNameSpam;
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
                GUI.Label(new Rect(170, y, 610, 20), "<b>== 房间恶搞与全局指令 (Troll) ==</b>"); y += 30f;

                var gm = UnityEngine.Object.FindObjectOfType<GameManager>();
                if (gm != null)
                {
                    if (GUI.Button(new Rect(170, y, 190, 28), "强制开启游戏"))
                    {
                        try { gm.CmdRequestStartGame(null); Plugin.LogSource.LogInfo("已发送强制开启游戏命令"); } catch { }
                    }
                    if (GUI.Button(new Rect(380, y, 190, 28), "强制切换模式"))
                    {
                        try { gm.CmdCycleGameMode(null); Plugin.LogSource.LogInfo("已发送强制切换模式命令"); } catch { }
                    }
                    if (GUI.Button(new Rect(590, y, 190, 28), "强制返回大厅"))
                    {
                        try { gm.CmdRequestResetToLobby(null); Plugin.LogSource.LogInfo("已发送强制返回大厅命令"); } catch { }
                    }
                }
                else
                {
                    GUI.Label(new Rect(170, y, 610, 22), "未找到 GameManager...");
                }
                y += 35f;

                int maxTrollPages = (cachedPlayers.Count + 4) / 5;
                if (maxTrollPages < 1) maxTrollPages = 1;
                if (trollPlayerPage >= maxTrollPages) trollPlayerPage = maxTrollPages - 1;
                if (trollPlayerPage < 0) trollPlayerPage = 0;

                GUI.Label(new Rect(170, y, 200, 25), "<b>恶搞玩家 (页 " + (trollPlayerPage + 1) + "/" + maxTrollPages + ")</b>");
                if (GUI.Button(new Rect(380, y, 40, 22), "◀")) trollPlayerPage = Mathf.Max(0, trollPlayerPage - 1);
                if (GUI.Button(new Rect(430, y, 40, 22), "▶")) trollPlayerPage = Mathf.Min(maxTrollPages - 1, trollPlayerPage + 1);
                y += 28f;

                int startTrollIdx = trollPlayerPage * 5;
                for (int i = 0; i < 5; i++)
                {
                    int idx = startTrollIdx + i;
                    if (idx >= cachedPlayers.Count) break;
                    var p = cachedPlayers[idx];
                    if (!SafeCheckAlive(p)) continue;

                    string name = "(未知)";
                    try { name = p.playerName ?? p.gameObject.name; } catch { }

                    GUI.Label(new Rect(170, y, 150, 22), "<b>" + name + "</b>");
                    
                    if (GUI.Button(new Rect(330, y, 60, 22), "监禁")) JailPlayer(p);
                    if (GUI.Button(new Rect(395, y, 60, 22), "拉人")) PullPlayer(p);
                    if (GUI.Button(new Rect(460, y, 60, 22), "扑倒")) TacklePlayer(p);
                    if (GUI.Button(new Rect(525, y, 60, 22), "自爆")) ExplodePlayer(p);
                    if (GUI.Button(new Rect(590, y, 60, 22), "放倒")) RagdollPlayer(p);
                    if (GUI.Button(new Rect(655, y, 60, 22), "强退")) GiveUpPlayer(p);
                    if (GUI.Button(new Rect(720, y, 60, 22), "排泄")) DumpPlayerButt(p);

                    y += 26f;
                }

                y += 10f; // 间距

                int maxNpcPages = (cachedNpcs.Count + 4) / 5;
                if (maxNpcPages < 1) maxNpcPages = 1;
                if (npcPage >= maxNpcPages) npcPage = maxNpcPages - 1;
                if (npcPage < 0) npcPage = 0;

                GUI.Label(new Rect(170, y, 200, 25), "<b>场景 NPC (页 " + (npcPage + 1) + "/" + maxNpcPages + ")</b>");
                if (GUI.Button(new Rect(380, y, 40, 22), "◀")) npcPage = Mathf.Max(0, npcPage - 1);
                if (GUI.Button(new Rect(430, y, 40, 22), "▶")) npcPage = Mathf.Min(maxNpcPages - 1, npcPage + 1);
                y += 28f;

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

                    GUI.Label(new Rect(170, y, 150, 22), nname + " [" + dist.ToString("0") + "m]");
                    
                    if (GUI.Button(new Rect(330, y, 80, 22), "扑倒NPC")) TackleNpc(n);
                    if (GUI.Button(new Rect(420, y, 80, 22), "传送到NPC")) Teleport(n.transform.position + Vector3.up * 1f);

                    y += 26f;
                }
            }
        }

        private void DrawESP()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 camPos = cam.transform.position;

            if (espContraband)
            {
                foreach (var c in cachedContrabands)
                {
                    try
                    {
                        if (!SafeCheckAlive(c)) continue;
                        Vector3 wp = c.transform.position;
                        float d = Vector3.Distance(camPos, wp);
                        if (d > espMaxDistance) continue;

                        Vector3 sp = cam.WorldToScreenPoint(wp + Vector3.up * 0.2f);
                        if (sp.z <= 0) continue;

                        float sX = sp.x;
                        float sY = Screen.height - sp.y;

                        if (espBoxes)
                        {
                            DrawBox(sX - 8f, sY - 8f, 16f, 16f, Color.red, 1.5f);
                        }

                        if (espTracers)
                        {
                            DrawLine(new Vector2(Screen.width / 2, Screen.height), new Vector2(sX, sY), Color.red, 1.0f);
                        }

                        string txt = "违禁品";
                        if (espShowDistance) txt += " (" + d.ToString("0") + "m)";
                        DrawTextESP(sX, sY - 12f, txt, Color.red);
                    }
                    catch { }
                }
            }

            if (espPlayer)
            {
                foreach (var p in cachedPlayers)
                {
                    try
                    {
                        if (!SafeCheckAlive(p)) continue;
                        Vector3 footWorld = p.transform.position;
                        Vector3 headWorld = footWorld + Vector3.up * 1.8f;

                        float d = Vector3.Distance(camPos, footWorld);
                        if (d > espMaxDistance) continue;

                        Vector3 footScreen = cam.WorldToScreenPoint(footWorld);
                        Vector3 headScreen = cam.WorldToScreenPoint(headWorld);

                        if (footScreen.z <= 0 || headScreen.z <= 0) continue;

                        float footY = Screen.height - footScreen.y;
                        float headY = Screen.height - headScreen.y;

                        float h = footY - headY;
                        float w = h / 2.2f;
                        float x = footScreen.x - w / 2;
                        float y = headY;

                        if (espBoxes)
                        {
                            DrawBox(x, y, w, h, Color.green, 2.0f);
                        }

                        if (espTracers)
                        {
                            DrawLine(new Vector2(Screen.width / 2, Screen.height), new Vector2(footScreen.x, footY), Color.green, 1.5f);
                        }

                        string name = p.gameObject.name;
                        try { if (!string.IsNullOrEmpty(p.playerName)) name = p.playerName; } catch { }
                        string txt = name;
                        if (espShowDistance) txt += " (" + d.ToString("0") + "m)";
                        DrawTextESP(footScreen.x, headY - 14f, txt, Color.green);
                    }
                    catch { }
                }
            }
        }

        private void DrawTextESP(float x, float y, string text, Color color)
        {
            // 通过 GUI.color 控制颜色，完美避开 GUIStyle 的 unstrip 异常！
            Color saved = GUI.color;

            // 绘制黑色阴影轮廓
            GUI.color = Color.black;
            GUI.Label(new Rect(x - 99f, y - 9f, 200f, 20f), text);
            GUI.Label(new Rect(x - 101f, y - 11f, 200f, 20f), text);

            // 绘制彩色文本
            GUI.color = color;
            GUI.Label(new Rect(x - 100f, y - 10f, 200f, 20f), text);

            GUI.color = saved;
        }

        private void DrawLine(Vector2 start, Vector2 end, Color color, float width)
        {
            Color savedColor = GUI.backgroundColor;
            GUI.backgroundColor = color;
            Vector2 d = end - start;
            float a = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            GUIUtility.RotateAroundPivot(a, start);
            
            // 使用最简单的原生 GUI.Box 模拟划线，避开 GUI.DrawTexture 裁切异常
            GUI.Box(new Rect(start.x, start.y, d.magnitude, width), "");
            
            GUIUtility.RotateAroundPivot(-a, start);
            GUI.backgroundColor = savedColor;
        }

        private void DrawBox(float x, float y, float width, float height, Color color, float thickness)
        {
            DrawLine(new Vector2(x, y), new Vector2(x + width, y), color, thickness);
            DrawLine(new Vector2(x, y + height), new Vector2(x + width, y + height), color, thickness);
            DrawLine(new Vector2(x, y), new Vector2(x, y + height), color, thickness);
            DrawLine(new Vector2(x + width, y), new Vector2(x + width, y + height), color, thickness);
        }

        // ===== 绕过与反射调用 =====
        private void ToggleRoleBypass()
        {
            bool current = false;
            TryGetBoolMemberOnAny("NetworkisAgent", out current);
            bool desired = !current;
            bool ok = TryInvokeOnAnyTarget("ServerSetIsAgent", desired);
            if (!ok) ok = TrySetBoolMemberOnAny("NetworkisAgent", desired);
            if (!ok) ok = TryInvokeLocalUserCode("UserCode_CmdDevFlipIsAgent");
            Plugin.LogSource.LogInfo("[Role] setAgent=" + desired + " ok=" + ok);
        }

        private void BeginBoardingBypass()
        {
            bool ok = TryInvokeOnAnyTarget("BeginBoarding");
            ok |= TrySetBoolMemberOnAny("NetworkisBoardingWindow", true);
            TrySetFloatMemberOnAny("NetworksyncedBoardingCountdownRemaining", 0f);
            TrySetFloatMemberOnAny("NetworksyncedBoardingWindowRemaining", 999f);
            if (!ok) ok = TryInvokeLocalUserCode("UserCode_CmdDevBeginBoarding");
            Plugin.LogSource.LogInfo("[Boarding] Begin ok=" + ok);
        }

        private void EndBoardingBypass()
        {
            bool ok = TryInvokeOnAnyTarget("EndBoarding");
            ok |= TrySetBoolMemberOnAny("NetworkisBoardingWindow", false);
            TrySetFloatMemberOnAny("NetworksyncedBoardingCountdownRemaining", 0f);
            TrySetFloatMemberOnAny("NetworksyncedBoardingWindowRemaining", 0f);
            if (!ok) ok = TryInvokeLocalUserCode("UserCode_CmdDevEndBoarding");
            Plugin.LogSource.LogInfo("[Boarding] End ok=" + ok);
        }

        private void EndGameCopsBypass()
        {
            bool ok = TryInvokeOnAnyTarget("ServerTimeWarpEndGameCopsWin");
            if (!ok) ok = TryInvokeOnAnyTarget("RequestEndGameCopsWin");
            if (!ok) ok = TryInvokeLocalUserCode("UserCode_CmdDevEndGameCopsWin");
            Plugin.LogSource.LogInfo("[EndGame] CopsWin ok=" + ok);
        }

        private void EndGameSmugglersBypass()
        {
            bool ok = TryInvokeOnAnyTarget("ServerTimeWarpEndGameSmugglersWin");
            if (!ok) ok = TryInvokeOnAnyTarget("RequestEndGameSmugglersWin");
            if (!ok) ok = TryInvokeLocalUserCode("UserCode_CmdDevEndGameSmugglersWin");
            Plugin.LogSource.LogInfo("[EndGame] SmugglersWin ok=" + ok);
        }

        private void EndGameNoWinnersBypass()
        {
            bool ok = TryInvokeOnAnyTarget("ServerEndGameNoWinners");
            if (!ok) ok = TryInvokeOnAnyTarget("RequestEndGameNoWinners");
            if (!ok) ok = TryInvokeLocalUserCode("UserCode_CmdDevEndGameNoWinners");
            Plugin.LogSource.LogInfo("[EndGame] NoWinners ok=" + ok);
        }

        private void AddMoneyToSelf(int delta)
        {
            if (localPlayerName == null) UpdateLocalRefs();
            if (localPlayerName == null || !SafeCheckAlive(localPlayerName)) return;
            try
            {
                int cur = localPlayerName.syncedMoney;
                int target = cur + delta;
                if (!TrySetMoneyAbsolute(localPlayerName, target))
                    TryInvokeLocalUserCode("UserCode_CmdSetMoney__Int32", target);
            }
            catch (System.Exception e) { Plugin.LogSource.LogError("AddMoney 失败: " + e.Message); }
        }

        private void CreditAllPlayers(int amount)
        {
            if (localPlayerName == null) return;
            try
            {
                UpdateTargets();
                int count = 0;
                foreach (var p in cachedPlayers)
                {
                    if (!SafeCheckAlive(p)) continue;
                    int cur = p.syncedMoney;
                    if (TrySetMoneyAbsolute(p, cur + amount)) count++;
                }
                if (count == 0 && TryInvokeLocalUserCode("UserCode_CmdDevCreditAllPlayers__Int32", amount)) count = -1;
            }
            catch { }
        }

        private void SpawnByName(string prefabName)
        {
            if (localPlayerName == null) return;
            try
            {
                if (SpawnPrefabDirect(prefabName)) return;
                TryInvokeLocalUserCode("UserCode_CmdDevSpawnInteractable__String", prefabName);
            }
            catch { }
        }

        private bool SpawnPrefabDirect(string prefabName)
        {
            if (spawnablePrefabs == null && Mirror.NetworkManager.singleton != null)
            {
                try { spawnablePrefabs = Mirror.NetworkManager.singleton.spawnPrefabs; } catch { }
            }
            if (spawnablePrefabs == null || spawnablePrefabs.Count == 0) return false;

            GameObject prefab = null;
            for (int i = 0; i < spawnablePrefabs.Count; i++)
            {
                GameObject candidate = spawnablePrefabs[i];
                if (candidate == null) continue;
                if (candidate.name == prefabName || string.Equals(candidate.name, prefabName, System.StringComparison.OrdinalIgnoreCase))
                {
                    prefab = candidate;
                    break;
                }
            }
            if (prefab == null) return false;

            Transform source = (localMeta != null && SafeCheckAlive(localMeta)) ? localMeta.transform : ((localPlayerName != null && SafeCheckAlive(localPlayerName)) ? localPlayerName.transform : null);
            if (source == null) return false;

            Vector3 pos = source.position + source.forward * 2f + Vector3.up * 0.5f;
            GameObject instance = null;
            try
            {
                instance = UnityEngine.Object.Instantiate(prefab, pos, Quaternion.identity);
                if (instance == null) return false;
                instance.name = prefab.name;
                Mirror.NetworkServer.Spawn(instance);
                return true;
            }
            catch
            {
                try { if (instance != null) UnityEngine.Object.Destroy(instance); } catch { }
                return false;
            }
        }

        private bool TrySetMoneyAbsolute(PlayerName player, int targetAmount)
        {
            if (player == null) return false;
            TrySetBoolMember(player, "NetworkcanAddMoney", true);
            bool ok = TrySetIntMember(player, "NetworksyncedMoney", targetAmount);
            ok |= TrySetIntMember(player, "NetworktotalMoney", targetAmount);
            if (!ok) ok = TrySetIntMember(player, "syncedMoney", targetAmount);
            return ok;
        }

        // ===== 底层反射助手 =====
        private System.Collections.Generic.List<object> GetCandidateTargets()
        {
            var targets = new System.Collections.Generic.List<object>();
            if (localPlayerName != null && SafeCheckAlive(localPlayerName))
            {
                targets.Add(localPlayerName);
                try { foreach (var c in localPlayerName.GetComponents<Component>()) if (c != null) targets.Add(c); } catch { }
            }
            if (localMeta != null && SafeCheckAlive(localMeta))
            {
                targets.Add(localMeta);
                try { foreach (var c in localMeta.GetComponents<Component>()) if (c != null) targets.Add(c); } catch { }
            }
            try { foreach (var b in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>()) if (b != null) targets.Add(b); } catch { }
            return targets;
        }

        private MethodInfo FindInstanceMethod(object target, string methodPrefix, int parameterCount)
        {
            if (target == null) return null;
            var cur = target.GetType();
            while (cur != null && cur.FullName != "System.Object")
            {
                foreach (var m in cur.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    if (m.IsStatic) continue;
                    if (!m.Name.StartsWith(methodPrefix)) continue;
                    if (m.GetParameters().Length != parameterCount) continue;
                    return m;
                }
                cur = cur.BaseType;
            }
            return null;
        }

        private PropertyInfo FindProperty(object target, string name)
        {
            if (target == null) return null;
            var cur = target.GetType();
            while (cur != null && cur.FullName != "System.Object")
            {
                var prop = cur.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                if (prop != null) return prop;
                cur = cur.BaseType;
            }
            return null;
        }

        private FieldInfo FindField(object target, string name)
        {
            if (target == null) return null;
            var cur = target.GetType();
            while (cur != null && cur.FullName != "System.Object")
            {
                var field = cur.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                if (field != null) return field;
                cur = cur.BaseType;
            }
            return null;
        }

        private bool TryInvokeOnTarget(object target, string methodPrefix, params object[] args)
        {
            var method = FindInstanceMethod(target, methodPrefix, args != null ? args.Length : 0);
            if (method == null) return false;
            try
            {
                method.Invoke(target, args);
                return true;
            }
            catch { return false; }
        }

        private bool TryInvokeOnAnyTarget(string methodPrefix, params object[] args)
        {
            foreach (var target in GetCandidateTargets())
                if (TryInvokeOnTarget(target, methodPrefix, args)) return true;
            return false;
        }

        private bool TryInvokeLocalUserCode(string methodPrefix, params object[] args)
        {
            if (localPlayerName == null) return false;
            return TryInvokeOnTarget(localPlayerName, methodPrefix, args);
        }

        private bool TrySetIntMember(object target, string name, int value)
        {
            try
            {
                var prop = FindProperty(target, name);
                if (prop != null && prop.CanWrite) { prop.SetValue(target, value); return true; }
                var field = FindField(target, name);
                if (field != null) { field.SetValue(target, value); return true; }
            }
            catch { }
            return false;
        }

        private bool TryGetIntMember(object target, string name, out int value)
        {
            value = 0;
            try
            {
                var prop = FindProperty(target, name);
                if (prop != null && prop.CanRead) { value = System.Convert.ToInt32(prop.GetValue(target)); return true; }
                var field = FindField(target, name);
                if (field != null) { value = System.Convert.ToInt32(field.GetValue(target)); return true; }
            }
            catch { }
            return false;
        }

        private bool TrySetBoolMember(object target, string name, bool value)
        {
            try
            {
                var prop = FindProperty(target, name);
                if (prop != null && prop.CanWrite) { prop.SetValue(target, value); return true; }
                var field = FindField(target, name);
                if (field != null) { field.SetValue(target, value); return true; }
            }
            catch { }
            return false;
        }

        private bool TryGetBoolMember(object target, string name, out bool value)
        {
            value = false;
            try
            {
                var prop = FindProperty(target, name);
                if (prop != null && prop.CanRead) { value = System.Convert.ToBoolean(prop.GetValue(target)); return true; }
                var field = FindField(target, name);
                if (field != null) { value = System.Convert.ToBoolean(field.GetValue(target)); return true; }
            }
            catch { }
            return false;
        }

        private bool TrySetFloatMember(object target, string name, float value)
        {
            try
            {
                var prop = FindProperty(target, name);
                if (prop != null && prop.CanWrite) { prop.SetValue(target, value); return true; }
                var field = FindField(target, name);
                if (field != null) { field.SetValue(target, value); return true; }
            }
            catch { }
            return false;
        }

        private bool TrySetIntMemberOnAny(string name, int value)
        {
            foreach (var target in GetCandidateTargets()) if (TrySetIntMember(target, name, value)) return true;
            return false;
        }

        private bool TrySetBoolMemberOnAny(string name, bool value)
        {
            foreach (var target in GetCandidateTargets()) if (TrySetBoolMember(target, name, value)) return true;
            return false;
        }

        private bool TrySetFloatMemberOnAny(string name, float value)
        {
            foreach (var target in GetCandidateTargets()) if (TrySetFloatMember(target, name, value)) return true;
            return false;
        }

        private bool TryGetBoolMemberOnAny(string name, out bool value)
        {
            foreach (var target in GetCandidateTargets()) if (TryGetBoolMember(target, name, out value)) return true;
            value = false;
            return false;
        }

        // ===== VEHICLE HELPERS =====
        private void SpawnSegwayPrefab()
        {
            if (spawnablePrefabs == null && Mirror.NetworkManager.singleton != null)
            {
                try { spawnablePrefabs = Mirror.NetworkManager.singleton.spawnPrefabs; } catch { }
            }
            if (spawnablePrefabs == null || spawnablePrefabs.Count == 0)
            {
                Plugin.LogSource.LogWarning("未就绪，请先刷新 WORLD 选项卡的 Prefab 列表");
                return;
            }
            string segName = "";
            for (int i = 0; i < spawnablePrefabs.Count; i++)
            {
                var pf = spawnablePrefabs[i];
                if (pf != null && pf.name != null && pf.name.ToLower().Contains("segway"))
                {
                    segName = pf.name;
                    break;
                }
            }
            if (string.IsNullOrEmpty(segName))
            {
                Plugin.LogSource.LogWarning("未找到 Segway 预制体，尝试生成默认 Segway");
                segName = "Segway";
            }
            SpawnByName(segName);
        }

        private PlayerInteractor GetLocalInteractor()
        {
            if (localPlayerName == null) UpdateLocalRefs();
            if (localPlayerName == null) return null;
            var pi = localPlayerName.GetComponent<PlayerInteractor>();
            if (pi == null) pi = localPlayerName.GetComponentInChildren<PlayerInteractor>();
            if (pi == null) pi = localPlayerName.GetComponentInParent<PlayerInteractor>();
            return pi;
        }

        private PlayerInteractor GetSegwayRider(SegwayInteractable s)
        {
            try
            {
                var prop = FindProperty(s, "rider");
                if (prop != null) return prop.GetValue(s) as PlayerInteractor;
                var field = FindField(s, "rider");
                if (field != null) return field.GetValue(s) as PlayerInteractor;
            }
            catch { }
            return null;
        }

        private void MountSegway(SegwayInteractable s)
        {
            if (s == null) return;
            var pi = GetLocalInteractor();
            if (pi == null) { Plugin.LogSource.LogWarning("未找到 PlayerInteractor，上车失败"); return; }
            try
            {
                s.CmdInteract(pi);
                Plugin.LogSource.LogInfo("已发送上车请求");
            }
            catch (System.Exception e) { Plugin.LogSource.LogError("上车异常: " + e.Message); }
        }

        private void EjectSegway(SegwayInteractable s)
        {
            if (s == null) return;
            var rider = GetSegwayRider(s);
            try
            {
                s.CmdDismount(rider, true, Vector3.up * 20f);
                Plugin.LogSource.LogInfo("已发送弹射请求！");
            }
            catch (System.Exception e) { Plugin.LogSource.LogError("弹射异常: " + e.Message); }
        }

        private void ExplodeSegway(SegwayInteractable s)
        {
            if (s == null) return;
            try
            {
                s.RpcExplosionForce(s.transform.position, 15f, 2000f, 3f);
                Plugin.LogSource.LogInfo("已调用 Segway 爆炸 RPC");
            }
            catch (System.Exception e) { Plugin.LogSource.LogError("爆炸异常: " + e.Message); }
        }

        private void CrashSegway(SegwayInteractable s)
        {
            if (s == null) return;
            try
            {
                s.RpcCrash();
                Plugin.LogSource.LogInfo("已调用 Segway 崩溃 RPC");
            }
            catch (System.Exception e) { Plugin.LogSource.LogError("崩溃异常: " + e.Message); }
        }

        private void BeepSegway(SegwayInteractable s)
        {
            if (s == null) return;
            try
            {
                s.CmdBeep();
                Plugin.LogSource.LogInfo("已发送鸣笛指令");
            }
            catch (System.Exception e) { Plugin.LogSource.LogError("鸣笛异常: " + e.Message); }
        }

        private void DestroySegway(SegwayInteractable s)
        {
            if (s == null) return;
            try
            {
                if (localPlayerName != null && localPlayerName.isServer)
                {
                    s.DestroySegway();
                    Plugin.LogSource.LogInfo("Host 已直接销毁平衡车");
                }
                else
                {
                    UnityEngine.Object.Destroy(s.gameObject);
                    Plugin.LogSource.LogInfo("本地已销毁平衡车");
                }
            }
            catch (System.Exception e) { Plugin.LogSource.LogError("销毁异常: " + e.Message); }
        }

        private void BringSegway(SegwayInteractable s)
        {
            if (s == null) return;
            if (localPlayerName != null && localPlayerName.isServer)
            {
                s.transform.position = localPlayerName.transform.position + localPlayerName.transform.forward * 2f;
                Plugin.LogSource.LogInfo("成功把平衡车吸到面前！");
            }
            else
            {
                Plugin.LogSource.LogWarning("吸车仅限主机有效！客机请使用'传送到车'功能。");
            }
        }

        // ===== TROLL HELPERS =====
        private void ExecuteWithSpoofedName(System.Action action, PlayerName target)
        {
            if (localPlayerName == null) UpdateLocalRefs();
            if (localPlayerName == null)
            {
                action();
                return;
            }

            string originalName = localPlayerName.playerName;
            string spoofedName = "系统管理员"; // 默认替罪羊

            if (cachedPlayers != null && cachedPlayers.Count > 0)
            {
                var otherNames = new System.Collections.Generic.List<string>();
                foreach (var pl in cachedPlayers)
                {
                    if (pl != null && pl.gameObject != null && pl != localPlayerName && pl != target)
                    {
                        string pname = pl.playerName;
                        if (!string.IsNullOrEmpty(pname) && pname != originalName)
                        {
                            otherNames.Add(pname);
                        }
                    }
                }
                if (otherNames.Count > 0)
                {
                    System.Random rand = new System.Random();
                    spoofedName = otherNames[rand.Next(otherNames.Count)];
                }
            }

            Plugin.LogSource.LogInfo($"[栽赃] 替罪羊伪装名: {spoofedName}");

            try
            {
                try { localPlayerName.CmdSetPlayerName(spoofedName, localPlayerName.steamId); } catch { }
                action();
            }
            finally
            {
                try { localPlayerName.CmdSetPlayerName(originalName, localPlayerName.steamId); } catch { }
            }
        }

        private void JailPlayer(PlayerName p)
        {
            if (p == null) return;
            try
            {
                var teleporter = p.GetComponent<PlayerTeleporter>();
                if (teleporter == null) teleporter = p.GetComponentInChildren<PlayerTeleporter>();
                if (teleporter == null) teleporter = p.GetComponentInParent<PlayerTeleporter>();
                if (teleporter == null) { Plugin.LogSource.LogWarning("无法获取目标 PlayerTeleporter"); return; }

                if (localPlayerName != null && localPlayerName.isServer)
                {
                    ExecuteWithSpoofedName(() => {
                        string currentName = localPlayerName.playerName;
                        teleporter.ServerCatchToPrisonThenReturnBomber("Jailed by " + currentName);
                        Plugin.LogSource.LogInfo("Host 已权威关押 (栽赃给 " + currentName + "): " + p.playerName);
                    }, p);
                }
                else
                {
                    var localArrest = localPlayerName != null ? localPlayerName.GetComponent<ArrestInteractable>() : null;
                    if (localArrest == null && localPlayerName != null) localArrest = localPlayerName.GetComponentInChildren<ArrestInteractable>();
                    if (localArrest == null && localPlayerName != null) localArrest = localPlayerName.GetComponentInParent<ArrestInteractable>();

                    if (localArrest != null && localPlayerName != null)
                    {
                        ExecuteWithSpoofedName(() => {
                            localArrest.CmdArrest(teleporter, false, null);
                            Plugin.LogSource.LogInfo("已伪装发送远程逮捕指令");
                        }, p);
                    }
                    else
                    {
                        Plugin.LogSource.LogWarning("无法远程逮捕，未获取到本地 Arrest 或本地 PlayerName");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Plugin.LogSource.LogError("JailPlayer 异常: " + ex.Message);
            }
        }

        private void PullPlayer(PlayerName p)
        {
            if (p == null) return;
            try
            {
                if (localPlayerName != null && localPlayerName.isServer)
                {
                    var teleporter = p.GetComponent<PlayerTeleporter>();
                    if (teleporter == null) teleporter = p.GetComponentInChildren<PlayerTeleporter>();
                    if (teleporter == null) teleporter = p.GetComponentInParent<PlayerTeleporter>();

                    if (teleporter != null && localMeta != null)
                    {
                        ExecuteWithSpoofedName(() => {
                            Vector3 dest = localMeta.transform.position + localMeta.transform.forward * 2f;
                            string currentName = localPlayerName.playerName;
                            teleporter.ServerTeleportAuthoritative(dest, "Pulled by " + currentName, "");
                            Plugin.LogSource.LogInfo("Host 权威拉人成功: " + p.playerName);
                        }, p);
                    }
                    else
                    {
                        Plugin.LogSource.LogWarning("拉人失败：未找到目标 teleporter 或本地 MetaPlayer");
                    }
                }
                else
                {
                    // Client: Physics Pull (物理吸人/扑倒击飞拉人)
                    var localTackle = localPlayerName != null ? localPlayerName.GetComponent<PlayerTackle>() : null;
                    if (localTackle == null && localPlayerName != null) localTackle = localPlayerName.GetComponentInChildren<PlayerTackle>();
                    if (localTackle == null && localPlayerName != null) localTackle = localPlayerName.GetComponentInParent<PlayerTackle>();

                    var targetPrm = p.GetComponent<PlayerRagdollManager>();
                    if (targetPrm == null) targetPrm = p.GetComponentInChildren<PlayerRagdollManager>();
                    if (targetPrm == null) targetPrm = p.GetComponentInParent<PlayerRagdollManager>();

                    if (localTackle != null && targetPrm != null && localMeta != null)
                    {
                        Vector3 startPos = p.transform.position;
                        Vector3 destPos = localMeta.transform.position;
                        Vector3 diff = destPos - startPos;
                        
                        // 依据距离计算抛射速度，将其物理抛射到自己身边
                        float dist = diff.magnitude;
                        Vector3 dir = diff.normalized;
                        Vector3 velocity = dir * (dist * 1.1f) + Vector3.up * (Mathf.Min(dist * 0.4f, 15f) + 3f);
                        
                        ExecuteWithSpoofedName(() => {
                            localTackle.CmdTacklePlayer(targetPrm, velocity, false);
                            Plugin.LogSource.LogInfo("客机物理拉人：已扑倒并击飞 " + p.playerName + " 投掷向你！距离=" + dist + ", 速度=" + velocity);
                        }, p);
                    }
                    else
                    {
                        Plugin.LogSource.LogWarning("物理拉人失败：未找到本地 Tackle、目标 RagdollManager 或本地 MetaPlayer");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Plugin.LogSource.LogError("PullPlayer 异常: " + ex.Message);
            }
        }

        private void TacklePlayer(PlayerName p)
        {
            if (p == null) return;
            try
            {
                var localTackle = localPlayerName != null ? localPlayerName.GetComponent<PlayerTackle>() : null;
                if (localTackle == null && localPlayerName != null) localTackle = localPlayerName.GetComponentInChildren<PlayerTackle>();
                if (localTackle == null && localPlayerName != null) localTackle = localPlayerName.GetComponentInParent<PlayerTackle>();

                var targetPrm = p.GetComponent<PlayerRagdollManager>();
                if (targetPrm == null) targetPrm = p.GetComponentInChildren<PlayerRagdollManager>();
                if (targetPrm == null) targetPrm = p.GetComponentInParent<PlayerRagdollManager>();

                if (localTackle != null && targetPrm != null)
                {
                    ExecuteWithSpoofedName(() => {
                        localTackle.CmdTacklePlayer(targetPrm, Vector3.up * 5f, false);
                        Plugin.LogSource.LogInfo("已对 " + p.playerName + " 施加远程扑倒！");
                    }, p);
                }
                else
                {
                    Plugin.LogSource.LogWarning("扑倒失败：未找到本地 Tackle 或目标 RagdollManager");
                }
            }
            catch (System.Exception ex)
            {
                Plugin.LogSource.LogError("TacklePlayer 异常: " + ex.Message);
            }
        }

        private void ExplodePlayer(PlayerName p)
        {
            if (p == null) return;
            try
            {
                var targetPrm = p.GetComponent<PlayerRagdollManager>();
                if (targetPrm == null) targetPrm = p.GetComponentInChildren<PlayerRagdollManager>();
                if (targetPrm == null) targetPrm = p.GetComponentInParent<PlayerRagdollManager>();

                if (targetPrm != null)
                {
                    ExecuteWithSpoofedName(() => {
                        targetPrm.CmdGiveUpExplode();
                        Plugin.LogSource.LogInfo("已发送强制自爆指令给: " + p.playerName);
                    }, p);
                }
                else
                {
                    Plugin.LogSource.LogWarning("自爆失败：未找到目标 RagdollManager");
                }
            }
            catch (System.Exception ex)
            {
                Plugin.LogSource.LogError("ExplodePlayer 异常: " + ex.Message);
            }
        }

        private void RagdollPlayer(PlayerName p)
        {
            if (p == null) return;
            try
            {
                var targetPrm = p.GetComponent<PlayerRagdollManager>();
                if (targetPrm == null) targetPrm = p.GetComponentInChildren<PlayerRagdollManager>();
                if (targetPrm == null) targetPrm = p.GetComponentInParent<PlayerRagdollManager>();

                if (targetPrm != null)
                {
                    if (localPlayerName != null && localPlayerName.isServer)
                    {
                        ExecuteWithSpoofedName(() => {
                            targetPrm.ServerBeginRagdoll(Vector3.up * 5f, 5f, Vector3.zero);
                            Plugin.LogSource.LogInfo("Host 已强制放倒: " + p.playerName);
                        }, p);
                    }
                    else
                    {
                        ExecuteWithSpoofedName(() => {
                            targetPrm.CmdBeginRagdoll(Vector3.up * 5f, 5f, Vector3.zero);
                            Plugin.LogSource.LogInfo("已发送放倒指令给: " + p.playerName);
                        }, p);
                    }
                }
                else
                {
                    Plugin.LogSource.LogWarning("放倒失败：未找到目标 RagdollManager");
                }
            }
            catch (System.Exception ex)
            {
                Plugin.LogSource.LogError("RagdollPlayer 异常: " + ex.Message);
            }
        }

        private void GiveUpPlayer(PlayerName p)
        {
            if (p == null) return;
            try
            {
                var targetPrm = p.GetComponent<PlayerRagdollManager>();
                if (targetPrm == null) targetPrm = p.GetComponentInChildren<PlayerRagdollManager>();
                if (targetPrm == null) targetPrm = p.GetComponentInParent<PlayerRagdollManager>();

                var targetPmm = p.GetComponent<PlayerModeManager>();
                if (targetPmm == null) targetPmm = p.GetComponentInChildren<PlayerModeManager>();
                if (targetPmm == null) targetPmm = p.GetComponentInParent<PlayerModeManager>();

                if (targetPrm != null && targetPmm != null)
                {
                    ExecuteWithSpoofedName(() => {
                        targetPrm.CmdGiveUp(targetPmm, "Killed by Mod");
                        Plugin.LogSource.LogInfo("已发送强制投降退场指令给: " + p.playerName);
                    }, p);
                }
                else
                {
                    Plugin.LogSource.LogWarning("强退失败：未找到目标 RagdollManager 或 ModeManager");
                }
            }
            catch (System.Exception ex)
            {
                Plugin.LogSource.LogError("GiveUpPlayer 异常: " + ex.Message);
            }
        }

        private void TackleNpc(NpcRagdollManager n)
        {
            if (n == null) return;
            try
            {
                var localTackle = localPlayerName != null ? localPlayerName.GetComponent<PlayerTackle>() : null;
                if (localTackle == null && localPlayerName != null) localTackle = localPlayerName.GetComponentInChildren<PlayerTackle>();
                if (localTackle == null && localPlayerName != null) localTackle = localPlayerName.GetComponentInParent<PlayerTackle>();

                if (localTackle != null)
                {
                    ExecuteWithSpoofedName(() => {
                        localTackle.CmdTackleNpc(n, Vector3.up * 5f);
                        Plugin.LogSource.LogInfo("已远程扑倒 NPC！");
                    }, null);
                }
                else
                {
                    Plugin.LogSource.LogWarning("扑倒失败：未找到本地 Tackle 组件");
                }
            }
            catch (System.Exception ex)
            {
                Plugin.LogSource.LogError("TackleNpc 异常: " + ex.Message);
            }
        }

        private void DumpPlayerButt(PlayerName p)
        {
            if (p == null) return;
            try
            {
                var mp = p.GetComponent<MetaPlayer>();
                if (mp == null) mp = p.GetComponentInChildren<MetaPlayer>();
                if (mp == null) mp = p.GetComponentInParent<MetaPlayer>();
                if (mp != null && mp.buttStorage != null)
                {
                    if (localPlayerName != null && localPlayerName.isServer)
                    {
                        mp.buttStorage.ServerDumpAll();
                        Plugin.LogSource.LogInfo("Host 已强制让 " + p.playerName + " 排泄！");
                    }
                    else if (p == localPlayerName)
                    {
                        mp.buttStorage.CmdDumpAll();
                        Plugin.LogSource.LogInfo("已排泄自己！");
                    }
                    else
                    {
                        Plugin.LogSource.LogWarning("强制他人排泄仅限主机有效！客机只能点击排泄自己。");
                    }
                }
                else
                {
                    Plugin.LogSource.LogWarning("未找到该玩家的 ButtStorage 组件");
                }
            }
            catch (System.Exception ex)
            {
                Plugin.LogSource.LogError("DumpPlayerButt 异常: " + ex.Message);
            }
        }

        private void ReleaseAllDogs()
        {
            var cages = UnityEngine.Object.FindObjectsOfType<DogCageInteractable>();
            var pi = GetLocalInteractor();
            if (pi == null)
            {
                Plugin.LogSource.LogWarning("未找到 PlayerInteractor，无法释放警犬");
                return;
            }
            int count = 0;
            foreach (var cage in cages)
            {
                if (cage == null) continue;
                try
                {
                    if (!cage.isOpen)
                    {
                        cage.CmdInteract(pi);
                        count++;
                    }
                }
                catch (System.Exception ex)
                {
                    Plugin.LogSource.LogError("互动警犬笼异常: " + ex.Message);
                }
            }
            Plugin.LogSource.LogInfo("已远程尝试互动/开启警犬笼，数量: " + count);
        }

        private void VendAllMachines()
        {
            var machines = UnityEngine.Object.FindObjectsOfType<VendingMachineInteractable>();
            var pi = GetLocalInteractor();
            if (pi == null)
            {
                Plugin.LogSource.LogWarning("未找到 PlayerInteractor，无法触发贩卖机");
                return;
            }
            int count = 0;
            foreach (var vm in machines)
            {
                if (vm == null) continue;
                try
                {
                    if (localPlayerName != null && localPlayerName.isServer)
                    {
                        vm.BeginVend();
                    }
                    else
                    {
                        vm.CmdInteract(pi);
                    }
                    count++;
                }
                catch (System.Exception ex)
                {
                    Plugin.LogSource.LogError("互动贩卖机异常: " + ex.Message);
                }
            }
            Plugin.LogSource.LogInfo("已远程尝试触发所有自动贩卖机，数量: " + count);
        }

        private void InteractWishlist()
        {
            var boards = UnityEngine.Object.FindObjectsOfType<WishlistInteractable>();
            var pi = GetLocalInteractor();
            if (pi == null)
            {
                Plugin.LogSource.LogWarning("未找到 PlayerInteractor，无法与愿望单板互动");
                return;
            }
            int count = 0;
            foreach (var b in boards)
            {
                if (b == null) continue;
                try
                {
                    b.CmdInteract(pi);
                    count++;
                }
                catch (System.Exception ex)
                {
                    Plugin.LogSource.LogError("互动愿望单板异常: " + ex.Message);
                }
            }
            Plugin.LogSource.LogInfo("已远程尝试互动所有愿望单板，数量: " + count);
        }

        private void PlayButtSoundForLobby()
        {
            if (localMeta == null) UpdateLocalRefs();
            if (localMeta == null || localMeta.buttStorage == null)
            {
                Plugin.LogSource.LogWarning("未找到本地 Player 的 ButtStorage，无法播放屁声");
                return;
            }
            try
            {
                localMeta.buttStorage.CmdPlayButtSound(false);
                Plugin.LogSource.LogInfo("已成功播放屁声！");
            }
            catch (System.Exception ex)
            {
                Plugin.LogSource.LogError("播放屁声异常: " + ex.Message);
            }
        }

        private void SpawnBabyNpc()
        {
            if (localPlayerName == null) UpdateLocalRefs();
            if (localPlayerName == null) return;

            // 1. 如果是主机，直接用 NpcManager 权威生成NPC
            if (localPlayerName.isServer && NpcManager.ServerInstance != null)
            {
                try
                {
                    Transform t = (localMeta != null) ? localMeta.transform : localPlayerName.transform;
                    NpcManager.ServerInstance.ServerSpawnNpc(t, true);
                    Plugin.LogSource.LogInfo("Host 已成功权威生娃（生成NPC）！");
                    return;
                }
                catch (System.Exception ex)
                {
                    Plugin.LogSource.LogError("NpcManager 生娃异常: " + ex.Message);
                }
            }

            // 2. 如果是客机，或者第一种方式失败，尝试在 spawnablePrefabs 里查找 NPC 预制体并生成
            if (spawnablePrefabs == null && Mirror.NetworkManager.singleton != null)
            {
                try { spawnablePrefabs = Mirror.NetworkManager.singleton.spawnPrefabs; } catch { }
            }

            if (spawnablePrefabs != null)
            {
                string npcPrefabName = "";
                // 搜索包含 npc, passenger, civilian, ai, character, human 的预制体
                string[] keywords = { "npc", "passenger", "civilian", "ai", "character", "human" };
                foreach (var kw in keywords)
                {
                    for (int i = 0; i < spawnablePrefabs.Count; i++)
                    {
                        var pf = spawnablePrefabs[i];
                        if (pf != null && pf.name != null && pf.name.ToLower().Contains(kw))
                        {
                            npcPrefabName = pf.name;
                            break;
                        }
                    }
                    if (!string.IsNullOrEmpty(npcPrefabName)) break;
                }

                if (!string.IsNullOrEmpty(npcPrefabName))
                {
                    SpawnByName(npcPrefabName);
                    Plugin.LogSource.LogInfo("已尝试生成预制体 NPC: " + npcPrefabName);
                }
                else
                {
                    Plugin.LogSource.LogWarning("未在 spawnPrefabs 中找到包含 NPC 关键字的预制体，尝试直接生成 'Passenger'");
                    SpawnByName("Passenger");
                }
            }
            else
            {
                Plugin.LogSource.LogWarning("spawnablePrefabs 为空，无法生成 NPC");
            }
        }

        private void DetonateAllC4()
        {
            var charges = UnityEngine.Object.FindObjectsOfType<C4Charge>();
            int count = 0;
            foreach (var c in charges)
            {
                if (c == null) continue;
                try
                {
                    c.RpcExplode();
                    count++;
                }
                catch (System.Exception ex)
                {
                    Plugin.LogSource.LogError("引爆C4异常: " + ex.Message);
                }
            }
            Plugin.LogSource.LogInfo("已远程引爆 C4 炸药，数量: " + count);
        }

        private void SummonAllElevators()
        {
            var buttons = UnityEngine.Object.FindObjectsOfType<ElevatorCallButtonInteractable>();
            var pi = GetLocalInteractor();
            if (pi == null)
            {
                Plugin.LogSource.LogWarning("未找到 PlayerInteractor，无法呼叫电梯");
                return;
            }
            int count = 0;
            foreach (var btn in buttons)
            {
                if (btn == null) continue;
                try
                {
                    btn.CmdInteract(pi);
                    count++;
                }
                catch (System.Exception ex)
                {
                    Plugin.LogSource.LogError("呼叫电梯异常: " + ex.Message);
                }
            }
            Plugin.LogSource.LogInfo("已远程尝试呼叫所有电梯，数量: " + count);
        }

        private void TriggerBreakRoomDoors(bool open)
        {
            var doors = UnityEngine.Object.FindObjectsOfType<BreakRoomDoor>();
            int count = 0;
            foreach (var d in doors)
            {
                if (d == null) continue;
                try
                {
                    if (open)
                    {
                        d.CmdTriggerDoorUnityEvent();
                    }
                    else
                    {
                        d.CmdResetDoorUnityEvent();
                    }
                    count++;
                }
                catch (System.Exception ex)
                {
                    Plugin.LogSource.LogError("触发休息室门异常: " + ex.Message);
                }
            }
            Plugin.LogSource.LogInfo("已远程尝试" + (open ? "开启" : "重置") + "所有休息室门，数量: " + count);
        }

        private void TriggerLockdown()
        {
            var buttons = UnityEngine.Object.FindObjectsOfType<LockdownButtonInteractable>();
            var pi = GetLocalInteractor();
            if (pi == null)
            {
                Plugin.LogSource.LogWarning("未找到 PlayerInteractor，无法触发封锁");
                return;
            }
            int count = 0;
            foreach (var btn in buttons)
            {
                if (btn == null) continue;
                try
                {
                    btn.CmdInteract(pi);
                    count++;
                }
                catch (System.Exception ex)
                {
                    Plugin.LogSource.LogError("触发封锁按钮异常: " + ex.Message);
                }
            }
            Plugin.LogSource.LogInfo("已远程触发封锁按钮，数量: " + count);
        }

        private void OnApplicationQuit()
        {
            Plugin.LogSource.LogInfo("[ExitPatch] 检测到游戏退出，正在强制杀掉当前进程以防止Steam卡“正在运行”状态...");
            try
            {
                System.Diagnostics.Process.GetCurrentProcess().Kill();
            }
            catch
            {
                System.Environment.Exit(0);
            }
        }
    }
}
