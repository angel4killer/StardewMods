﻿using HarmonyLib;

using StardewValley.Objects;

using StopRugRemoval.Configuration;

namespace StopRugRemoval.HarmonyPatches.Niceties;

/// <summary>
/// Holds patches to defang napalm rings in safe areas.
/// </summary>
[HarmonyPatch(typeof(Ring))]
internal static class NapalmInSafeAreas
{
    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(nameof(Ring.onMonsterSlay))]
    [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313:Parameter names should begin with lower-case letter", Justification = "Harmony Convention.")]
    private static bool Prefix(Ring __instance, GameLocation location)
    {
        if (ModEntry.Config.NapalmInSafeAreas)
        {
            return true;
        }

        try
        {
            if (__instance.ParentSheetIndex == 811 && location.IsLocationConsideredSafe())
            {
                return false;
            }
        }
        catch (Exception ex)
        {
            ModEntry.ModMonitor.Log($"Failed when trying to prevent napalm ring in safe areas:\n\n{ex}", LogLevel.Error);
        }
        return true;
    }

    private static bool IsLocationConsideredSafe(this GameLocation location)
        => ModEntry.Config.SafeLocationMap.TryGetValue(location.NameOrUniqueName, out IsSafeLocationEnum val)
            & val == IsSafeLocationEnum.Safe;
}
