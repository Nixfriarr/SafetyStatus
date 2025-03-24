// Ignore Spelling: SafetyStatus Jotunn
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;
using System.Reflection;
using UnityEngine;
using System.Linq;


namespace SafetyStatus
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid, Jotunn.Main.Version)]
    internal sealed class SafetyStatus : BaseUnityPlugin
    {
        internal const string Author = "Searica";
        public const string PluginName = "SafetyStatus";
        public const string PluginGUID = $"{Author}.Valheim.{PluginName}";
        public const string PluginVersion = "1.2.2";

        internal static CustomStatusEffect SafeEffect;
        internal const string SafeEffectName = "SafeStatusEffect";
        internal static int SafeEffectHash;

        public void Awake()
        {
            Log.Init(Logger);

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), harmonyInstanceId: PluginGUID);

            Game.isModded = true;

            PrefabManager.OnVanillaPrefabsAvailable += AddCustomStatusEffect;
        }

        /// <summary>
        ///     Create and add the Safe status effect
        /// </summary>
        private void AddCustomStatusEffect()
        {
            try
            {
                StatusEffect statusEffect = ScriptableObject.CreateInstance<StatusEffect>();
                statusEffect.name = SafeEffectName;
                statusEffect.m_name = "Safe";
                statusEffect.m_icon = PrefabManager.Cache.GetPrefab<CraftingStation>("piece_workbench").m_icon;
                statusEffect.m_startMessageType = MessageHud.MessageType.TopLeft;
                statusEffect.m_startMessage = "You feel safer";
                statusEffect.m_stopMessageType = MessageHud.MessageType.TopLeft;
                statusEffect.m_stopMessage = "You feel less safe";
                SafeEffect = new CustomStatusEffect(statusEffect, false);
                SafeEffectHash = SafeEffect.StatusEffect.NameHash();
                ItemManager.Instance.AddStatusEffect(SafeEffect);
            }
            finally
            {
                PrefabManager.OnVanillaPrefabsAvailable -= AddCustomStatusEffect;
            }
        }

        [HarmonyPatch(typeof(EffectArea))]
        internal static class EffectAreaPatch
        {
            /// <summary>
            ///     Catch things that are not pieces but have a PlayerBase effect
            /// </summary>
            /// <param name="__instance"></param>
            [HarmonyPostfix]
            [HarmonyPatch(nameof(EffectArea.Awake))]
            private static void AwakePostfix(EffectArea __instance)
            {
                if (__instance.m_type == EffectArea.Type.PlayerBase)
                {
                    __instance.m_statusEffect = SafeEffectName;
                    __instance.m_statusEffectHash = SafeEffectHash;
                }
                // Draw a sphere around PlayerBase EffectArea
                DrawSphere(__instance.gameObject, Vector3.zero, Mathf.Max(0.5f, __instance.GetRadius()), Color.green, 0.1f, "Safe Area");
            }

            /// <summary>
            /// Draws a sphere at the specified position with the given parameters.
            /// </summary>
            /// <param name="parent"></param>
            /// <param name="position"></param>
            /// <param name="radius"></param>
            /// <param name="color"></param>
            /// <param name="width"></param>
            /// <param name="text"></param>
            private static void DrawSphere(GameObject parent, Vector3 position, float radius, Color color, float width, string text = "")
            {
                // Draw circles along three axes (X, Y, Z) to form a sphere
                DrawCircle(parent, position, radius, color, width, text, new Vector3(1, 0, 0)); // Circle on X-axis
                DrawCircle(parent, position, radius, color, width, text, new Vector3(0, 1, 0)); // Circle on Y-axis
                DrawCircle(parent, position, radius, color, width, text, new Vector3(0, 0, 1)); // Circle on Z-axis

                if (!string.IsNullOrEmpty(text))
                {
                    // Add hoverable text to the parent object
                    var hoverText = parent.AddComponent<HoverText>();
                    hoverText.m_text = text;
                }
            }

            /// <summary>
            /// Draws a single circle around the specified axis.
            /// </summary>
            /// <param name="parent"></param>
            /// <param name="position"></param>
            /// <param name="radius"></param>
            /// <param name="color"></param>
            /// <param name="width"></param>
            /// <param name="text"></param>
            /// <param name="axis"></param>
            private static void DrawCircle(GameObject parent, Vector3 position, float radius, Color color, float width, string text, Vector3 axis)
            {
                var obj = new GameObject("CircleRenderer");
                obj.transform.position = parent.transform.position;
                obj.transform.rotation = parent.transform.rotation;
                obj.transform.parent = parent.transform;

                var lineRenderer = obj.AddComponent<LineRenderer>();
                lineRenderer.useWorldSpace = false;
                lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
                lineRenderer.startColor = color;
                lineRenderer.endColor = color;
                lineRenderer.widthMultiplier = width;

                // Calculate the number of segments for the circle
                int segments = 100;
                lineRenderer.positionCount = segments + 1;

                // Generate points for the circle
                for (int i = 0; i <= segments; i++)
                {
                    float angle = 2f * Mathf.PI * i / segments;
                    Vector3 point = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
                    point = Quaternion.LookRotation(axis) * point; // Rotate circle to align with the axis
                    lineRenderer.SetPosition(i, point + position);
                }
            }

            /// <summary>
            ///     SafetyStatus is also applied to non player creatures so when they die it tries to update 
            ///     the status effect for them since they didn't leave, but they are not there any more.
            ///     So remove any invalid items from list of items to update the status effect of the EffectArea for.
            /// </summary>
            /// <param name="__instance"></param>
            /// <param name="deltaTime"></param>
            [HarmonyPrefix]
            [HarmonyPatch(nameof(EffectArea.CustomFixedUpdate))]
            private static void CustomFixedUpdatePrefex(EffectArea __instance, float deltaTime)
            {
                if (!__instance)
                {
                    return;
                }
                __instance.m_collidedWithCharacter = __instance.m_collidedWithCharacter.Where(x => IsValidCollidedWithCharacter(x)).ToList();
            }

            private static bool IsValidCollidedWithCharacter(Character item)
            {
                if (!item || item.GetSEMan() == null || !item.GetSEMan().m_nview || !item.GetSEMan().m_nview.IsValid())
                {
                    return false;
                }
                return true;
            }

        }

        [HarmonyPatch(typeof(Piece))]
        internal static class PiecePatch
        {
            /// <summary>
            ///     Catch pieces that mods like MVBP add a PlayerBase effect to
            /// </summary>
            /// <param name="__instance"></param>
            [HarmonyPostfix]
            [HarmonyPriority(Priority.Low)]
            [HarmonyPatch(nameof(Piece.Awake))]
            private static void AwakePostfix(Piece __instance)
            {
                if (!__instance)
                {
                    return;
                }

                AddSafeEffect(__instance.gameObject);
            }

            /// <summary>
            ///     Catch pieces that mods like MVBP add a PlayerBase effect to
            /// </summary>
            /// <param name="__instance"></param>
            [HarmonyPostfix]
            [HarmonyPriority(Priority.Low)]
            [HarmonyPatch(nameof(Piece.SetCreator))]
            private static void SetCreatorPostfix(Piece __instance)
            {
                if (!__instance)
                {
                    return;
                }

                AddSafeEffect(__instance.gameObject);
            }

            /// <summary>
            ///     Scans components in children and add SafeEffect if PlayerBase effect area is found.
            /// </summary>
            /// <param name="gameObject"></param>
            private static void AddSafeEffect(GameObject gameObject)
            {
                if (!gameObject) { return; }

                foreach (var effectArea in gameObject.GetComponentsInChildren<EffectArea>())
                {
                    if (effectArea.m_type == EffectArea.Type.PlayerBase)
                    {
                        effectArea.m_statusEffect = SafeEffectName;
                        effectArea.m_statusEffectHash = SafeEffectHash;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Player))]
        internal static class PlayerPatch
        {
            /// <summary>
            ///     Patch to check if the SafeStatusEffect should be removed.
            /// </summary>
            /// <param name="__instance"></param>
            [HarmonyPostfix]
            [HarmonyPatch(nameof(Player.UpdateEnvStatusEffects))]
            private static void UpdateEnvStatusEffectsPostFix(Player __instance)
            {
                var inPlayerBase = EffectArea.IsPointInsideArea(__instance.transform.position, EffectArea.Type.PlayerBase, 1f);
                var hasSafeEffect = __instance.m_seman.HaveStatusEffect(SafeEffectHash);

                if (hasSafeEffect && !inPlayerBase)
                {
                    __instance.m_seman.RemoveStatusEffect(SafeEffectHash);
                }
            }
        }
    }

    /// <summary>
    /// Helper class for properly logging from static contexts.
    /// </summary>
    internal static class Log
    {
        internal static ManualLogSource _logSource;

        internal static void Init(ManualLogSource logSource)
        {
            _logSource = logSource;
        }

        internal static void LogDebug(object data) => _logSource.LogDebug(data);

        internal static void LogError(object data) => _logSource.LogError(data);

        internal static void LogFatal(object data) => _logSource.LogFatal(data);

        internal static void LogInfo(object data) => _logSource.LogInfo(data);

        internal static void LogMessage(object data) => _logSource.LogMessage(data);

        internal static void LogWarning(object data) => _logSource.LogWarning(data);
    }
}
