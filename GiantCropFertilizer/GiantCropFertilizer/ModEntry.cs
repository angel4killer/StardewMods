﻿using AtraShared.ConstantsAndEnums;
using AtraShared.Integrations;
using AtraShared.Integrations.Interfaces;
using AtraShared.MigrationManager;
using AtraShared.Utils;
using AtraShared.Utils.Extensions;
using GiantCropFertilizer.DataModels;
using GiantCropFertilizer.HarmonyPatches;
using HarmonyLib;
using StardewModdingAPI.Events;

namespace GiantCropFertilizer;

/// <inheritdoc />
internal class ModEntry : Mod
{
    private static IJsonAssetsAPI? jsonAssets;

    private MigrationManager? migrator;

    /// <summary>
    /// Gets the integer ID of the giant crop fertilizer. -1 if not found/not loaded yet.
    /// </summary>
    internal static int GiantCropFertilizerID => jsonAssets?.GetObjectId("Giant Crop Fertilizer") ?? -1;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    /// <summary>
    /// Gets the logger for this mod.
    /// </summary>
    internal static IMonitor ModMonitor { get; private set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    /// <inheritdoc />
    public override void Entry(IModHelper helper)
    {
        I18n.Init(helper.Translation);
        ModMonitor = this.Monitor;

        helper.Events.Content.AssetRequested += this.OnAssetRequested;

        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
        helper.Events.GameLoop.Saving += this.OnSaving;
    }

    private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
        => AssetEditor.HandleAssetRequested(e);

    private void OnSaving(object? sender, SavingEventArgs e)
        => this.Helper.Data.WriteGlobalData(
            Constants.SaveFolderName + "_SavedObjectID",
            new GiantCropFertilizerIDStorage(GiantCropFertilizerID));

    /// <summary>
    /// Applies the patches for this mod.
    /// </summary>
    /// <param name="harmony">This mod's harmony instance.</param>
    /// <remarks>Delay until GameLaunched in order to patch other mods....</remarks>
    private void ApplyPatches(Harmony harmony)
    {
        try
        {
            harmony.PatchAll();
            if (this.Helper.ModRegistry.IsLoaded("spacechase0.MultiFertilizer"))
            {
                this.Monitor.Log("Found MultiFertilizer, applying compat patches", LogLevel.Info);
                HoeDirtPatcher.ApplyPatches(harmony);
                MultiFertilizerDrawTranspiler.ApplyPatches(harmony);
            }
            else
            {
                HoeDirtDrawTranspiler.ApplyPatches(harmony);
            }
        }
        catch (Exception ex)
        {
            ModMonitor.Log(string.Format(ErrorMessageConsts.HARMONYCRASH, ex), LogLevel.Error);
        }
        harmony.Snitch(this.Monitor, harmony.Id, transpilersOnly: true);
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        this.ApplyPatches(new Harmony(this.ModManifest.UniqueID));

        IntegrationHelper helper = new(this.Monitor, this.Helper.Translation, this.Helper.ModRegistry, LogLevel.Warn);
        if (helper.TryGetAPI("spacechase0.JsonAssets", "1.10.3", out jsonAssets))
        {
            jsonAssets.LoadAssets(Path.Combine(this.Helper.DirectoryPath, "assets", "json-assets"), this.Helper.Translation);
            this.Monitor.Log("Loaded packs!");
        }
        else
        {
            this.Monitor.Log("Packs could not be loaded! This mod will probably not function.", LogLevel.Error);
        }
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        MultiplayerHelpers.AssertMultiplayerVersions(this.Helper.Multiplayer, this.ModManifest, this.Monitor, this.Helper.Translation);

        this.migrator = new(this.ModManifest, this.Helper, this.Monitor);
        this.migrator.ReadVersionInfo();
        this.Helper.Events.GameLoop.Saved += this.WriteMigrationData;
    }

    /// <summary>
    /// Writes migration data then detaches the migrator.
    /// </summary>
    /// <param name="sender">Smapi thing.</param>
    /// <param name="e">Arguments for just-before-saving.</param>
    private void WriteMigrationData(object? sender, SavedEventArgs e)
    {
        if (this.migrator is not null)
        {
            this.migrator.SaveVersionInfo();
            this.migrator = null;
        }
        this.Helper.Events.GameLoop.Saved -= this.WriteMigrationData;
    }
}