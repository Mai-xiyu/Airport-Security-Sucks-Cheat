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

            if (espNpc)
            {
                foreach (var n in cachedNpcs)
                {
                    try
                    {
                        if (!SafeCheckAlive(n)) continue;
                        Vector3 footWorld = n.transform.position;
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
                            DrawBox(x, y, w, h, Color.yellow, 1.5f);
                        }

                        if (espTracers)
                        {
                            DrawLine(new Vector2(Screen.width / 2, Screen.height), new Vector2(footScreen.x, footY), Color.yellow, 1.0f);
                        }

                        string name = "NPC Cop/Civ";
                        if (n.gameObject != null) name = n.gameObject.name;
                        string txt = name;
                        if (espShowDistance) txt += " (" + d.ToString("0") + "m)";
                        DrawTextESP(footScreen.x, headY - 14f, txt, Color.yellow);
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
    }

}
