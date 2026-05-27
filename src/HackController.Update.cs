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
        private ulong GetHostSteamId()
        {
            try
            {
                var players = UnityEngine.Object.FindObjectsOfType<PlayerName>();
                if (players != null)
                {
                    for (int i = 0; i < players.Length; i++)
                    {
                        var p = players[i];
                        if (p != null && p.isHostPlayer && !SafeIsLocal(p))
                        {
                            return p.steamId;
                        }
                    }
                }
            }
            catch { }
            return 0;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Insert)) showMenu = !showMenu;
            if (Input.GetKeyDown(KeyCode.F1)) ToggleFly();

            // 名字延迟恢复，确保 SyncVar 状态同步足够到达其他所有客户端后再改回
            if (isNameSpoofed && Time.time >= nameRestoreTime)
            {
                if (!(enableAntiKick && (enableAntiKickCrash || enableAntiKickLayout)))
                {
                    try
                    {
                        if (localPlayerName != null && !string.IsNullOrEmpty(originalPlayerName))
                        {
                            ulong steamIdToUse = localPlayerName.steamId;
                            if (enableAntiKick)
                            {
                                ulong hostId = GetHostSteamId();
                                if (hostId != 0) steamIdToUse = hostId;
                            }
                            localPlayerName.CmdSetPlayerName(originalPlayerName, steamIdToUse);
                            Plugin.LogSource.LogInfo($"[栽赃] 自动恢复真实姓名: {originalPlayerName}");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Plugin.LogSource.LogWarning($"[栽赃] 自动恢复姓名异常: {ex.Message}");
                    }
                }
                isNameSpoofed = false;
                originalPlayerName = "";
            }

            UpdateLocalRefs();

            // 三阶段防踢 (SteamID + Carson 伪装 & 强力防踢 / 布局溢出防踢)
            if (enableAntiKick && localPlayerName != null && !isNameSpoofed)
            {
                try
                {
                    // 1. Carson 开发者权限同步
                    if (!localPlayerName.isCarson)
                    {
                        localPlayerName.CmdSetIsCarson(true);
                        Plugin.LogSource.LogInfo("[防踢] 成功为本地玩家设置 Carson 开发者标记！");
                    }

                    // 2. 强力防踢 (利用 TMPro 溢出瘫痪房主端 TabUI)
                    if (enableAntiKickCrash)
                    {
                        string crashName = "<sprite=999999>";
                        if (localPlayerName.playerName != crashName)
                        {
                            ulong steamIdToUse = localPlayerName.steamId;
                            ulong hostId = GetHostSteamId();
                            if (hostId != 0) steamIdToUse = hostId;
                            localPlayerName.CmdSetPlayerName(crashName, steamIdToUse);
                            Plugin.LogSource.LogInfo("[防踢] 开启强力防踢：发送恶意富文本破坏房主 Tab 菜单！");
                        }
                    }
                    else if (enableAntiKickLayout)
                    {
                        // 3. 布局排版溢出防踢 (追加空格把踢人按钮推到屏幕外)
                        ulong hostId = GetHostSteamId();
                        ulong steamIdToUse = (hostId != 0) ? hostId : localPlayerName.steamId;
                        
                        string currentCleanName = localPlayerName.playerName ?? "";
                        if (currentCleanName.Contains("<space="))
                        {
                            int idx = currentCleanName.IndexOf("<space=");
                            currentCleanName = currentCleanName.Substring(0, idx);
                        }
                        if (string.IsNullOrEmpty(currentCleanName))
                        {
                            currentCleanName = "游客";
                        }
                        
                        string layoutName = currentCleanName + "<space=3000>";
                        if (localPlayerName.playerName != layoutName || localPlayerName.steamId != steamIdToUse)
                        {
                            localPlayerName.CmdSetPlayerName(layoutName, steamIdToUse);
                            Plugin.LogSource.LogInfo($"[防踢] 开启布局防踢：设置玩家名字为 {currentCleanName} + <space=3000>");
                        }
                    }
                    else
                    {
                        // 4. 常规房主 SteamID 伪装
                        ulong hostId = GetHostSteamId();
                        if (hostId != 0 && localPlayerName.steamId != hostId && localPlayerName.steamId != 0)
                        {
                            localPlayerName.CmdSetPlayerName(localPlayerName.playerName, hostId);
                            Plugin.LogSource.LogInfo($"[防踢] 成功为本地玩家伪装房主 SteamID: {hostId}");
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Plugin.LogSource.LogError("[防踢] 自动防踢逻辑异常: " + ex.Message);
                }
            }

            // 瞬间解控/秒爬
            if (enableInstantRecovery && localPlayerName != null)
            {
                try
                {
                    var prm = localPlayerName.GetComponent<PlayerRagdollManager>();
                    if (prm == null) prm = localPlayerName.GetComponentInChildren<PlayerRagdollManager>();
                    if (prm == null) prm = localPlayerName.GetComponentInParent<PlayerRagdollManager>();

                    if (prm != null && prm.isRagdollActive)
                    {
                        prm.CmdRecoverImmediatelyIfShould();
                        prm.CmdRecover();
                    }
                }
                catch { }
            }

            // 无冷却扑人
            if (enableNoTackleCooldown && localPlayerName != null)
            {
                try
                {
                    var tackle = localPlayerName.GetComponent<PlayerTackle>();
                    if (tackle == null) tackle = localPlayerName.GetComponentInChildren<PlayerTackle>();
                    if (tackle == null) tackle = localPlayerName.GetComponentInParent<PlayerTackle>();

                    if (tackle != null)
                    {
                        tackle.tackleCooldown = 0f;
                        tackle.localTackleCooldownStartTime = 0f;
                        tackle.localGrabCooldownStartTime = 0f;
                        tackle.localRagdollCooldownStartTime = 0f;
                    }
                }
                catch { }
            }

            // 无限倒地锁 (起身后再次自动放倒)
            if (infiniteRagdollList.Count > 0)
            {
                for (int i = infiniteRagdollList.Count - 1; i >= 0; i--)
                {
                    var p = infiniteRagdollList[i];
                    if (p == null || p.gameObject == null)
                    {
                        infiniteRagdollList.RemoveAt(i);
                        continue;
                    }

                    try
                    {
                        var targetPrm = p.GetComponent<PlayerRagdollManager>();
                        if (targetPrm == null) targetPrm = p.GetComponentInChildren<PlayerRagdollManager>();
                        if (targetPrm == null) targetPrm = p.GetComponentInParent<PlayerRagdollManager>();

                        if (targetPrm != null && !targetPrm.isRagdollActive)
                        {
                            if (localPlayerName != null && localPlayerName.isServer)
                            {
                                targetPrm.ServerBeginRagdoll(Vector3.up * 2f, 5f, Vector3.zero);
                            }
                            else
                            {
                                targetPrm.CmdBeginRagdoll(Vector3.up * 2f, 5f, Vector3.zero);
                            }
                        }
                    }
                    catch { }
                }
            }

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
            // 循环随机乱码改名逻辑
            if (enableNameSpam && localPlayerName != null && Time.time > nextNameSpamTime)
            {
                nextNameSpamTime = Time.time + 0.15f;
                // 若开启强力防踢，跳过循环随机改名
                if (enableAntiKick && enableAntiKickCrash) return;
                try
                {
                    string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$";
                    System.Random rand = new System.Random();
                    string randName = "";
                    for (int i = 0; i < 8; i++) randName += chars[rand.Next(chars.Length)];
                    ulong steamIdToUse = localPlayerName.steamId;
                    if (enableAntiKick)
                    {
                        ulong hostId = GetHostSteamId();
                        if (hostId != 0) steamIdToUse = hostId;
                        
                        if (enableAntiKickLayout)
                        {
                            randName += "<space=3000>";
                        }
                    }
                    localPlayerName.CmdSetPlayerName(randName, steamIdToUse);
                }
                catch { }
            }
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

    }

}
