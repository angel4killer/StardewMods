﻿using AtraCore.Utilities;

using AtraShared.ConstantsAndEnums;
using AtraShared.Integrations;
using AtraShared.MigrationManager;
using AtraShared.Utils;
using AtraShared.Utils.Extensions;

using GrowableBushes.Framework;

using HarmonyLib;

using StardewModdingAPI.Events;

using StardewValley.TerrainFeatures;

using AtraUtils = AtraShared.Utils.Utils;

namespace GrowableBushes;

// TODO:
// * Override all the necessary draw methods.
// * Bushes for sale.
// * Smart Building compat.

/// <inheritdoc />
internal sealed class ModEntry : Mod
{
    private MigrationManager? migrator;

    /// <summary>
    /// Gets the logger for this mod.
    /// </summary>
    internal static IMonitor ModMonitor { get; private set; } = null!;

    /// <summary>
    /// Gets the config instance for this mod.
    /// </summary>
    internal static ModConfig Config { get; private set; } = null!;

    /// <inheritdoc />
    public override void Entry(IModHelper helper)
    {
        I18n.Init(helper.Translation);

        ModMonitor = this.Monitor;
        Config = AtraUtils.GetConfigOrDefault<ModConfig>(helper, this.Monitor);

        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
        ConsoleCommands.RegisterCommands(helper.ConsoleCommands);
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        IntegrationHelper helper = new(this.Monitor, this.Helper.Translation, this.Helper.ModRegistry, LogLevel.Error);

        if (helper.TryGetAPI("spacechase0.SpaceCore", "1.9.3", out SpaceCore.IApi? api))
        {
            api.RegisterSerializerType(typeof(InventoryBush));

            this.Helper.Events.Input.ButtonPressed += this.OnButtonPressed;
            this.Helper.Events.GameLoop.SaveLoaded += this.SaveLoaded;
            this.Helper.Events.Player.Warped += this.OnWarped;
            this.Helper.Events.Player.InventoryChanged += this.OnInventoryChanged;

            this.ApplyPatches(new Harmony(this.ModManifest.UniqueID));

            GMCMHelper gmcmHelper = new(this.Monitor, this.Helper.Translation, this.Helper.ModRegistry, this.ModManifest);

            if (gmcmHelper.TryGetAPI())
            {
                gmcmHelper.Register(
                    reset: static () => Config = new(),
                    save: () => this.Helper.AsyncWriteConfig(this.Monitor, Config))
                .AddParagraph(I18n.ModDescription)
                .GenerateDefaultGMCM(static () => Config);
            }
        }
        else
        {
            // this should never happen. I'm using a spacecore type. It should actually just die.
            this.Monitor.Log($"Could not load spacecore's API. This is a fatal error.", LogLevel.Error);
        }
    }

    private void OnInventoryChanged(object? sender, InventoryChangedEventArgs e)
    {
        if (!e.IsLocalPlayer || Game1.currentLocation is not GameLocation loc)
        {
            return;
        }

        foreach (var item in e.Added)
        {
            if (item is InventoryBush bush)
            {
                bush.UpdateForNewLocation(loc);
            }
        }

        foreach (var item in e.Removed)
        {
            if (item is InventoryBush bush)
            {
                bush.UpdateForNewLocation(loc);
            }
        }
    }

    private void OnWarped(object? sender, WarpedEventArgs e)
    {
        if (e.IsLocalPlayer && e.NewLocation is GameLocation loc)
        {
            foreach (var item in e.Player.Items)
            {
                if (item is InventoryBush bush)
                {
                    bush.UpdateForNewLocation(loc);
                }
            }
        }
    }

    /// <summary>
    /// Applies the patches for this mod.
    /// </summary>
    /// <param name="harmony">This mod's harmony instance.</param>
    /// <remarks>Delay until GameLaunched in order to patch other mods....</remarks>
    private void ApplyPatches(Harmony harmony)
    {
        try
        {
            harmony.PatchAll(typeof(ModEntry).Assembly);
        }
        catch (Exception ex)
        {
            ModMonitor.Log(string.Format(ErrorMessageConsts.HARMONYCRASH, ex), LogLevel.Error);
        }
        harmony.Snitch(this.Monitor, harmony.Id, transpilersOnly: true);
    }

#warning - remove this debug method!
    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (Context.IsPlayerFree && e.Button == SButton.L)
        {
            InventoryBush bush = new(BushSizes.Small, 1);
            Game1.player.addItemByMenuIfNecessaryElseHoldUp(bush);
        }
    }

    #region migration

    /// <inheritdoc cref="IGameLoopEvents.SaveLoaded"/>
    /// <remarks>Used to load in this mod's data models.</remarks>
    private void SaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        MultiplayerHelpers.AssertMultiplayerVersions(this.Helper.Multiplayer, this.ModManifest, this.Monitor, this.Helper.Translation);

        if (Context.IsSplitScreen && Context.ScreenId != 0)
        {
            return;
        }

        this.migrator = new(this.ModManifest, this.Helper, this.Monitor);
        if (!this.migrator.CheckVersionInfo())
        {
            this.Helper.Events.GameLoop.Saved += this.WriteMigrationData;
        }
        else
        {
            this.migrator = null;
        }

        if (Context.IsMainPlayer)
        {
            foreach (GameLocation loc in GameLocationUtils.YieldAllLocations())
            {
                foreach (LargeTerrainFeature? feature in loc.largeTerrainFeatures)
                {
                    if (feature is not Bush bush || !bush.modData.ContainsKey(InventoryBush.BushModData))
                    {
                        continue;
                    }

                    BushSizes size = bush.modData.GetEnum(InventoryBush.BushModData, BushSizes.Small);
                    if (size == BushSizes.SmallAlt)
                    {
                        bush.tileSheetOffset.Value = 1;
                        bush.setUpSourceRect();
                    }
                }
            }
        }
    }

    /// <inheritdoc cref="IGameLoopEvents.Saved"/>
    /// <remarks>
    /// Writes migration data then detaches the migrator.
    /// </remarks>
    private void WriteMigrationData(object? sender, SavedEventArgs e)
    {
        if (this.migrator is not null)
        {
            this.migrator.SaveVersionInfo();
            this.migrator = null;
        }

        this.Helper.Events.GameLoop.Saved -= this.WriteMigrationData;
    }
    #endregion
}