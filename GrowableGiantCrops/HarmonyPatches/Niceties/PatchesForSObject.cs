﻿using AtraShared.Utils.Extensions;

using HarmonyLib;

using Microsoft.Xna.Framework;

namespace GrowableGiantCrops.HarmonyPatches.Niceties;

/// <summary>
/// Holds patches on SObject for misc stuff.
/// </summary>
[HarmonyPatch(typeof(SObject))]
internal static class PatchesForSObject
{
    /// <summary>
    /// Marks an SObject placed with this mod.
    /// </summary>
    internal const string ModDataMiscObject = "atravita.GrowableGiantCrops.PlacedObject";

    private const string ModDataKey = "atravita.GrowableGiantCrops.PlacedSlimeBall";

    [HarmonyPostfix]
    [HarmonyPatch(nameof(SObject.placementAction))]
    [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313:Parameter names should begin with lower-case letter", Justification = "Harmony convention.")]
    private static void PostfixPlacement(SObject __instance, int x, int y)
    {
        if (__instance?.bigCraftable?.Value == true)
        {
            if (__instance.Name == "Slime Ball")
            {
                __instance.modData?.SetBool(ModDataKey, true);
                __instance.TileLocation = new Vector2(x / Game1.tileSize, y / Game1.tileSize);
            }
            else if (__instance.Name == "Mushroom Box")
            {
                __instance.modData?.SetBool(ModDataMiscObject, true);
                __instance.TileLocation = new Vector2(x / Game1.tileSize, y / Game1.tileSize);
            }
        }
        else if (__instance?.bigCraftable?.Value == false
            && (__instance.Name == "Stone" || __instance.Name.Contains("Weeds") || __instance.Name.Contains("Twig")))
        {
            __instance.modData?.SetBool(ModDataMiscObject, true);
            __instance.TileLocation = new Vector2(x / Game1.tileSize, y / Game1.tileSize);
        }
    }

    [HarmonyPrefix]
    [HarmonyPriority(Priority.VeryHigh)]
    [HarmonyPatch(nameof(SObject.checkForAction))]
    [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313:Parameter names should begin with lower-case letter", Justification = "Harmony convention.")]
    private static bool PrefixSlimeBall(SObject __instance, ref bool __result)
    {
        if (!ModEntry.Config.CanSquishPlacedSlimeBalls
            && __instance?.bigCraftable?.Value == true && __instance.Name == "Slime Ball"
            && __instance.modData?.GetBool(ModDataKey) == true)
        {
            __result = false;
            return false;
        }
        return true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(SObject.isPlaceable))]
    [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313:Parameter names should begin with lower-case letter", Justification = "Harmony convention.")]
    private static void PostfixIsPlaceable(SObject __instance, ref bool __result)
    {
        if (__result)
        {
            return;
        }

        try
        {
            if (!__instance.bigCraftable.Value && __instance.GetType() == typeof(SObject))
            {
                if (__instance.ParentSheetIndex == 590 // artifact spot
                    || __instance.Name == "Stone" || __instance.Name.Contains("Weeds") || __instance.Name.Contains("Twig"))
                {
                    __result = true;
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            ModEntry.ModMonitor.Log($"Failed while trying to make certain things placeable, see log for details.", LogLevel.Error);
            ModEntry.ModMonitor.Log(ex.ToString());
        }
    }
}