﻿using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using AtraBase.Toolkit;
using AtraCore.Framework.ReflectionManager;
using AtraShared.Utils.Extensions;
using AtraShared.Utils.HarmonyHelper;
using CameraPan.Framework;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI.Utilities;

namespace CameraPan.HarmonyPatches;

/// <summary>
/// Adjusts the viewport based on the offset vector.
/// </summary>
[HarmonyPatch(typeof(Game1))]
[SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313:Parameter names should begin with lower-case letter", Justification = "Named for Harmony.")]
internal static class ViewportAdjustmentPatches
{
    private static readonly PerScreen<CameraBehavior> cameraBehavior = new(() => CameraBehavior.Both);

    #region transpiler helpers

    [MethodImpl(TKConstants.Hot)]
    private static bool IsInEvent()
        => Game1.CurrentEvent is Event evt && (evt.farmer is not null && !evt.isFestival);

    [MethodImpl(TKConstants.Hot)]
    private static bool ShouldLock() => !IsInEvent() && cameraBehavior.Value.HasFlagFast(CameraBehavior.Locked);

    [MethodImpl(TKConstants.Hot)]
    private static bool ShouldOffset() => !IsInEvent() && cameraBehavior.Value.HasFlagFast(CameraBehavior.Offset);

    [MethodImpl(TKConstants.Hot)]
    private static float GetXTarget(float prevVal) => ShouldOffset() ? ModEntry.Target.X : prevVal;

    [MethodImpl(TKConstants.Hot)]
    private static float GetYTarget(float prevVal) => ShouldOffset() ? ModEntry.Target.Y : prevVal;

    #endregion

    [MethodImpl(TKConstants.Hot)]
    [HarmonyPatch("getViewportCenter")]
    private static void Postfix(ref Point __result)
    {
        if (Game1.viewportTarget.X == -2.14748365E+09f && !IsInEvent() && ShouldOffset()
            && (Math.Abs(Game1.viewportCenter.X - ModEntry.Target.X) >= 4 || Math.Abs(Game1.viewportCenter.Y - ModEntry.Target.Y) >= 4))
        {
            __result = Game1.viewportCenter = ModEntry.Target;
        }
    }

    [HarmonyPatch(nameof(Game1.UpdateViewPort))]
    [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1116:Split parameters should start on line after declaration", Justification = "Reviewed.")]
    private static IEnumerable<CodeInstruction>? Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original)
    {
        try
        {
            ILHelper helper = new(original, instructions, ModEntry.ModMonitor, gen);
            helper.FindNext(new CodeInstructionWrapper[]
            { // if (Game1.viewportFreeze || overrideFreeze)
                (OpCodes.Ldsfld, typeof(Game1).GetCachedField(nameof(Game1.viewportFreeze), ReflectionCache.FlagTypes.StaticFlags)),
                OpCodes.Ldc_I4_0,
                OpCodes.Ceq,
                OpCodes.Ldarg_0,
                OpCodes.Or,
                OpCodes.Brfalse,
            })
            .Push()
            .Advance(5)
            .StoreBranchDest()
            .AdvanceToStoredLabel()
            .DefineAndAttachLabel(out Label jumpPast)
            .Pop()
            .GetLabels(out IList<Label>? firstLabelsToMove)
            .Insert(new CodeInstruction[]
            { // insert if (!ShouldLock) around this block.
                new(OpCodes.Call, typeof(ViewportAdjustmentPatches).GetCachedMethod(nameof(ShouldLock), ReflectionCache.FlagTypes.StaticFlags)),
                new(OpCodes.Brtrue, jumpPast),
            }, withLabels: firstLabelsToMove)
            .FindNext(new CodeInstructionWrapper[]
            { // if (Game1.currentLocation.forceViewportPlayerFollow)
                (OpCodes.Call, typeof(Game1).GetCachedProperty(nameof(Game1.currentLocation), ReflectionCache.FlagTypes.StaticFlags).GetGetMethod()),
                (OpCodes.Ldfld, typeof(GameLocation).GetCachedField(nameof(GameLocation.forceViewportPlayerFollow), ReflectionCache.FlagTypes.InstanceFlags)),
                OpCodes.Brfalse_S,
            })
            .Push()
            .Advance(3)
            .DefineAndAttachLabel(out Label jumpToLock)
            .Pop()
            .GetLabels(out IList<Label>? secondLabelsToMove)
            .Insert(new CodeInstruction[]
            { // insert if (ShouldLock() || GAme1.currentLocation.forceViewportPlayerFollow)
                new(OpCodes.Call, typeof(ViewportAdjustmentPatches).GetCachedMethod(nameof(ShouldLock), ReflectionCache.FlagTypes.StaticFlags)),
                new(OpCodes.Brtrue, jumpToLock),
            }, withLabels: secondLabelsToMove)
            .FindNext(new CodeInstructionWrapper[]
            { // fix up the X location
                (OpCodes.Callvirt, typeof(Character).GetCachedProperty(nameof(Character.Position), ReflectionCache.FlagTypes.InstanceFlags).GetGetMethod()),
                (OpCodes.Ldfld, typeof(Vector2).GetCachedField(nameof(Vector2.X), ReflectionCache.FlagTypes.InstanceFlags)),
            })
            .Advance(2)
            .Insert(new CodeInstruction[]
            {
                new (OpCodes.Call, typeof(ViewportAdjustmentPatches).GetCachedMethod(nameof(GetXTarget), ReflectionCache.FlagTypes.StaticFlags)),
            })
            .FindNext(new CodeInstructionWrapper[]
            { // fix up the Y location
                (OpCodes.Callvirt, typeof(Character).GetCachedProperty(nameof(Character.Position), ReflectionCache.FlagTypes.InstanceFlags).GetGetMethod()),
                (OpCodes.Ldfld, typeof(Vector2).GetCachedField(nameof(Vector2.Y), ReflectionCache.FlagTypes.InstanceFlags)),
            })
            .Advance(2)
            .Insert(new CodeInstruction[]
            {
                new (OpCodes.Call, typeof(ViewportAdjustmentPatches).GetCachedMethod(nameof(GetYTarget), ReflectionCache.FlagTypes.StaticFlags)),
            });

            // helper.Print();
            return helper.Render();
        }
        catch (Exception ex)
        {
            ModEntry.ModMonitor.Log($"Ran into error transpiling {original.Name}\n\n{ex}", LogLevel.Error);
            original.Snitch(ModEntry.ModMonitor);
        }
        return null;
    }
}
