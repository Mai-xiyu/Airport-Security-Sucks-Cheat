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
        private bool enableAntiKick = true;
        private bool enableAntiKickCrash = false;
        private bool enableAntiKickLayout = true;
        private bool enableInstantRecovery = true;
        private bool enableNoTackleCooldown = false;
        private System.Collections.Generic.List<PlayerName> infiniteRagdollList = new System.Collections.Generic.List<PlayerName>();

        // ===== 栽赃伪装延迟恢复 =====
        private bool isNameSpoofed = false;
        private string originalPlayerName = "";
        private float nameRestoreTime = 0f;
        private PlayerName selectedTrollPlayer = null;

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
        private bool espNpc = true; // 透视 NPC

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
        private bool SafeIsLocal(PlayerName p)
        {
            try { return p.isLocalPlayer; } catch { return false; }
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
