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
        private void ExecuteWithSpoofedName(System.Action action, PlayerName target)
        {
            if (localPlayerName == null) UpdateLocalRefs();
            if (localPlayerName == null)
            {
                action();
                return;
            }

            string originalName;
            if (isNameSpoofed)
            {
                originalName = originalPlayerName;
            }
            else
            {
                originalName = localPlayerName.playerName;
                originalPlayerName = originalName;
            }

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

            Plugin.LogSource.LogInfo($"[栽赃] 伪装为: {spoofedName}，真实名字为: {originalName}");

            try
            {
                ulong steamIdToUse = localPlayerName.steamId;
                if (enableAntiKick)
                {
                    ulong hostId = GetHostSteamId();
                    if (hostId != 0) steamIdToUse = hostId;
                }
                try { localPlayerName.CmdSetPlayerName(spoofedName, steamIdToUse); } catch { }
                isNameSpoofed = true;
                nameRestoreTime = Time.time + 3.0f; // 3.0 秒延迟恢复

                action();
            }
            catch (System.Exception ex)
            {
                Plugin.LogSource.LogError("ExecuteWithSpoofedName 内部执行异常: " + ex.Message);
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

        private void KickPlayer(PlayerName p)
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
                        // 利用物理引擎 NaN 速度的致命漏洞，直接令目标客户端本地抛出物理计算异常而强制卡死并掉线
                        UnityEngine.Vector3 crashVelocity = new UnityEngine.Vector3(float.NaN, float.NaN, float.NaN);
                        localTackle.CmdTacklePlayer(targetPrm, crashVelocity, false);
                        Plugin.LogSource.LogInfo("已对 " + p.playerName + " 发送 NaN 物理冲击，成功越权强制踢出！");
                    }, p);
                }
                else
                {
                    Plugin.LogSource.LogWarning("踢人失败：未找到本地 Tackle 或目标 RagdollManager");
                }
            }
            catch (System.Exception ex)
            {
                Plugin.LogSource.LogError("KickPlayer 异常: " + ex.Message);
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

        private void LockGroupRagdoll(bool isAgent)
        {
            try
            {
                int count = 0;
                foreach (var p in cachedPlayers)
                {
                    if (p != null && p.IsAgent == isAgent && !SafeIsLocal(p))
                    {
                        if (!infiniteRagdollList.Contains(p))
                        {
                            infiniteRagdollList.Add(p);
                            count++;
                        }
                    }
                }
                Plugin.LogSource.LogInfo($"已一键锁定 {count} 个 " + (isAgent ? "警卫" : "走私犯") + " 无限倒地！");
            }
            catch { }
        }

        private void ExecuteGroupAction(bool targetAgent, string actionType)
        {
            try
            {
                UpdateTargets();
                int count = 0;
                foreach (var p in cachedPlayers)
                {
                    if (!SafeCheckAlive(p)) continue;
                    if (p.isLocalPlayer) continue;
                    if (p.IsAgent != targetAgent) continue;

                    if (actionType == "jail") JailPlayer(p);
                    else if (actionType == "tackle") TacklePlayer(p);
                    else if (actionType == "ragdoll") RagdollPlayer(p);
                    else if (actionType == "explode") ExplodePlayer(p);
                    else if (actionType == "kick") KickPlayer(p);
                    else if (actionType == "dump") DumpPlayerButt(p);

                    count++;
                }

                string groupName = targetAgent ? "警卫组" : "走私犯组";
                Plugin.LogSource.LogInfo($"[群控] 已向 {groupName} 中的 {count} 名玩家发送 {actionType} 指令。");
            }
            catch (System.Exception ex)
            {
                Plugin.LogSource.LogError($"ExecuteGroupAction ({actionType}) 发生异常: " + ex.Message);
            }
        }

    }

}
