﻿using AtraBase.Collections;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;

namespace PelicanTownFoodBank;

/// <summary>
/// Manages assets for this mod.
/// </summary>
internal static class AssetManager
{
    private const string ASSETPREFIX = "Mods/atravita_FoodBank_";

    private static IAssetName denylist = null!;

    /// <summary>
    /// Initialize the asset manager.
    /// </summary>
    /// <param name="parser">Game Content Helper.</param>
    internal static void Initialize(IGameContentHelper parser)
        => denylist = parser.ParseAssetName(ASSETPREFIX + "denylist");

    /// <summary>
    /// Applies the load.
    /// </summary>
    /// <param name="e">Event args.</param>
    internal static void Load(AssetRequestedEventArgs e)
    {
        if (e.NameWithoutLocale.IsEquivalentTo(denylist))
        {
            e.LoadFrom(EmptyContainers.GetEmptyDictionary<string, string>, AssetLoadPriority.Low);
        }
    }
}