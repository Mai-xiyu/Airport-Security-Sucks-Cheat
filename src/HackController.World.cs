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

        // ===== 新增的场景交互方法 =====
        private void TriggerInstantWin()
        {
            if (localPlayerName == null || !localPlayerName.isServer)
            {
                Plugin.LogSource.LogWarning("安全拦截：TriggerInstantWin 仅限房主(Host)使用");
                return;
            }
            try
            {
                if (localMeta == null) UpdateLocalRefs();
                if (localMeta == null)
                {
                    Plugin.LogSource.LogWarning("未找到本地 MetaPlayer，无法触发通关");
                    return;
                }
                var winTeles = UnityEngine.Object.FindObjectsOfType<WinTeleporter>();
                if (winTeles == null || winTeles.Length == 0)
                {
                    Plugin.LogSource.LogWarning("场景中未找到任何 WinTeleporter");
                    return;
                }
                foreach (var wt in winTeles)
                {
                    if (wt != null)
                    {
                        wt.CmdHeyServerImInTheTrigger(localMeta);
                        Plugin.LogSource.LogInfo("已向 WinTeleporter 发送进入通关区域指令");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Plugin.LogSource.LogError("TriggerInstantWin 异常: " + ex.Message);
            }
        }

        private void TriggerDevMapSwitch()
        {
            if (localPlayerName == null || !localPlayerName.isServer)
            {
                Plugin.LogSource.LogWarning("安全拦截：TriggerDevMapSwitch 仅限房主(Host)使用");
                return;
            }
            try
            {
                var sws = UnityEngine.Object.FindObjectsOfType<NetworkedDevMapSwitch>();
                if (sws == null || sws.Length == 0)
                {
                    Plugin.LogSource.LogWarning("场景中未找到任何 NetworkedDevMapSwitch 实例");
                    return;
                }
                foreach (var sw in sws)
                {
                    if (sw != null)
                    {
                        sw.CmdSwitchMaps();
                        Plugin.LogSource.LogInfo("已向 NetworkedDevMapSwitch 发送 CmdSwitchMaps 指令");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Plugin.LogSource.LogError("TriggerDevMapSwitch 异常: " + ex.Message);
            }
        }

        private void TriggerDevHijack()
        {
            if (localPlayerName == null || !localPlayerName.isServer)
            {
                Plugin.LogSource.LogWarning("安全拦截：TriggerDevHijack 仅限房主(Host)使用");
                return;
            }
            try
            {
                var sws = UnityEngine.Object.FindObjectsOfType<NetworkedDevMapSwitch>();
                if (sws == null || sws.Length == 0)
                {
                    Plugin.LogSource.LogWarning("场景中未找到任何 NetworkedDevMapSwitch 实例");
                    return;
                }
                foreach (var sw in sws)
                {
                    if (sw != null)
                    {
                        sw.CmdHijacking();
                        Plugin.LogSource.LogInfo("已向 NetworkedDevMapSwitch 发送 CmdHijacking 指令");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Plugin.LogSource.LogError("TriggerDevHijack 异常: " + ex.Message);
            }
        }

        private void TriggerDogAbuse()
        {
            if (localPlayerName == null || !localPlayerName.isServer)
            {
                Plugin.LogSource.LogWarning("安全拦截：TriggerDogAbuse 仅限房主(Host)使用");
                return;
            }
            try
            {
                var dogs = UnityEngine.Object.FindObjectsOfType<PoliceDog>();
                if (dogs == null || dogs.Length == 0)
                {
                    Plugin.LogSource.LogWarning("场景中未找到任何警犬");
                    return;
                }
                foreach (var dog in dogs)
                {
                    if (dog != null)
                    {
                        dog.CmdAnimalAbuse();
                        Plugin.LogSource.LogInfo("已向警犬发送 CmdAnimalAbuse 指令");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Plugin.LogSource.LogError("TriggerDogAbuse 异常: " + ex.Message);
            }
        }

        private void SetAllScannersState(int stateVal)
        {
            if (localPlayerName == null || !localPlayerName.isServer)
            {
                Plugin.LogSource.LogWarning("安全拦截：SetAllScannersState 仅限房主(Host)使用");
                return;
            }
            try
            {
                var gloveScanners = UnityEngine.Object.FindObjectsOfType<GloveScannerInteractable>();
                foreach (var gs in gloveScanners)
                {
                    if (gs != null)
                    {
                        gs.CmdSetLightState((GloveScannerInteractable.LightState)stateVal);
                    }
                }

                var wandScanners = UnityEngine.Object.FindObjectsOfType<WandScannerInteractable>();
                foreach (var ws in wandScanners)
                {
                    if (ws != null)
                    {
                        ws.CmdSetLightState((WandScannerInteractable.LightState)stateVal);
                    }
                }
                Plugin.LogSource.LogInfo($"已远程批量设置所有手套/扫描棒指示灯状态为: {stateVal}");
            }
            catch (System.Exception ex)
            {
                Plugin.LogSource.LogError("SetAllScannersState 异常: " + ex.Message);
            }
        }

        private Vector3 GetLocationCoords(string locName)
        {
            try
            {
                if (locName == "监狱")
                {
                    var jail = UnityEngine.Object.FindObjectOfType<JailInteractableFix>();
                    if (jail != null) return jail.transform.position + Vector3.up * 1f;
                }
                else if (locName == "登机口/飞机")
                {
                    var winTel = UnityEngine.Object.FindObjectsOfType<WinTeleporter>();
                    if (winTel != null && winTel.Length > 0) return winTel[0].transform.position + Vector3.up * 1f;

                    var plane = UnityEngine.Object.FindObjectOfType<PlaneController>();
                    if (plane != null) return plane.transform.position + Vector3.up * 1f;
                }
                else if (locName == "前台/大厅")
                {
                    var kiosk = UnityEngine.Object.FindObjectOfType<KioskInteractable>();
                    if (kiosk != null) return kiosk.transform.position + Vector3.up * 1f;

                    var hostBooth = UnityEngine.Object.FindObjectOfType<HostBoothInteractable>();
                    if (hostBooth != null) return hostBooth.transform.position + Vector3.up * 1f;
                }
                else if (locName == "休息室")
                {
                    var brControls = UnityEngine.Object.FindObjectOfType<BreakRoomControlsInteractable>();
                    if (brControls != null) return brControls.transform.position + Vector3.up * 1f;

                    var brDoor = UnityEngine.Object.FindObjectOfType<BreakRoomDoor>();
                    if (brDoor != null) return brDoor.transform.position + Vector3.up * 1f;
                }
            }
            catch (System.Exception ex)
            {
                Plugin.LogSource.LogError("GetLocationCoords 异常: " + ex.Message);
            }
            return Vector3.zero;
        }

    }

}
