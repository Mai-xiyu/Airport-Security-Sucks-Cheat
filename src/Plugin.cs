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
        public const string PLUGIN_VERSION = "5.2.0";
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


}
