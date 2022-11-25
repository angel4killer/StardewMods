﻿using AtraCore.Utilities;

using AtraShared.ConstantsAndEnums;
using AtraShared.Integrations;
using AtraShared.MigrationManager;
using AtraShared.Utils.Extensions;

using BetterIntegratedModItems.Framework;
using BetterIntegratedModItems.Framework.DataModels;

using HarmonyLib;

using StardewModdingAPI.Events;

using AtraUtils = AtraShared.Utils.Utils;

namespace BetterIntegratedModItems;

/// <inheritdoc />
internal sealed class ModEntry : Mod
{
    private const string LOCATIONWATCHER = "location.data";
    private const string DATAPACKAGE = "DATAPACKAGE";
    private const string LOCATIONNAME = "LOCATIONNAME";

    private MigrationManager? migrator;

    /// <summary>
    /// Gets the logger for this mod.
    /// </summary>
    internal static IMonitor ModMonitor { get; private set; } = null!;

    /// <summary>
    /// Gets the config instance for this mod.
    /// </summary>
    internal static ModConfig Config { get; private set; } = null!;

    /// <summary>
    /// Gets the location watcher for this mod.
    /// This tracks every location any player has seen.
    /// </summary>
    internal static LocationWatcher? LocationWatcher { get; private set; }

    /// <inheritdoc />
    public override void Entry(IModHelper helper)
    {
        // bind helpers.
        ModMonitor = this.Monitor;
        I18n.Init(helper.Translation);

        // AssetManager.Initialize(helper.GameContent);
        Config = AtraUtils.GetConfigOrDefault<ModConfig>(helper, this.Monitor);

        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
        helper.Events.GameLoop.Saved += this.OnSaved;

        helper.Events.Player.Warped += this.OnWarped;

        helper.Events.Multiplayer.PeerConnected += this.OnPeerConnected;
        helper.Events.Multiplayer.ModMessageReceived += this.OnModMessageRecieved;
    }

    /// <inheritdoc cref="IGameLoopEvents.GameLaunched"/>
    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        this.ApplyPatches(new Harmony(this.ModManifest.UniqueID));

        GMCMHelper helper = new(this.Monitor, this.Helper.Translation, this.Helper.ModRegistry, this.ModManifest);
        if (helper.TryGetAPI())
        {
            helper.Register(
                reset: static () => Config = new(),
                save: () => this.Helper.AsyncWriteConfig(this.Monitor, Config))
            .AddParagraph(I18n.ModDescription)
            .GenerateDefaultGMCM(static () => Config);
        }
    }

    /// <summary>
    /// Applies the patches for this mod.
    /// </summary>
    /// <param name="harmony">This mod's harmony instance.</param>
    private void ApplyPatches(Harmony harmony)
    {
        try
        {
            harmony.PatchAll();
        }
        catch (Exception ex)
        {
            ModMonitor.Log(string.Format(ErrorMessageConsts.HARMONYCRASH, ex), LogLevel.Error);
        }
        harmony.Snitch(this.Monitor, harmony.Id, transpilersOnly: true);
    }

    /// <inheritdoc cref="IGameLoopEvents.SaveLoaded"/>
    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        if (Context.IsSplitScreen && Context.ScreenId != 0)
        {
            return;
        }
        MultiplayerHelpers.AssertMultiplayerVersions(this.Helper.Multiplayer, this.ModManifest, this.Monitor, this.Helper.Translation);
        this.migrator = new(this.ModManifest, this.Helper, this.Monitor);
        if (!this.migrator.CheckVersionInfo())
        {
            this.Helper.Events.GameLoop.Saved += this.WriteMigrationData;
        }
        else
        {
            this.migrator = null;
        }

        // Load data for the LocationWatcher.
        LocationWatcher = this.Helper.Data.ReadSaveData<LocationWatcher>(LOCATIONWATCHER) ?? new();
        this.Helper.Multiplayer.SendMessage(
            message: LocationWatcher,
            messageType: DATAPACKAGE,
            modIDs: new[] { this.ModManifest.UniqueID },
            playerIDs: this.Helper.Multiplayer.GetConnectedPlayers().Where(p => !p.IsSplitScreen).Select(p => p.PlayerID).ToArray());
    }

    /// <inheritdoc cref="IPlayerEvents.Warped"/>
    private void OnWarped(object? sender, WarpedEventArgs e)
    {
        if (e.IsLocalPlayer && LocationWatcher!.SeenLocations.Add(e.NewLocation.Name))
        {
            this.Helper.Multiplayer.SendMessage(e.NewLocation.Name, LOCATIONNAME, new[] { this.ModManifest.UniqueID });
        }
    }

    /// <inheritdoc cref="IGameLoopEvents.Saved"/>
    private void OnSaved(object? sender, SavedEventArgs e)
    {
        if (Context.IsMultiplayer)
        {
            this.Helper.Data.WriteSaveData(LOCATIONWATCHER, LocationWatcher);
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

    #region multiplayer
    private void OnPeerConnected(object? sender, PeerConnectedEventArgs e)
    {
        if (Context.IsMainPlayer && LocationWatcher is not null)
        {
            this.Helper.Multiplayer.SendMessage(
                message: LocationWatcher,
                messageType: DATAPACKAGE,
                modIDs: new[] { this.ModManifest.UniqueID },
                playerIDs: new[] { e.Peer.PlayerID });
        }
    }

    private void OnModMessageRecieved(object? sender, ModMessageReceivedEventArgs e)
    {
        if (e.FromModID != this.ModManifest.UniqueID || Context.ScreenId != 0)
        {
            return;
        }

        switch (e.Type)
        {
            case DATAPACKAGE:
            {
                LocationWatcher = e.ReadAs<LocationWatcher>();
                break;
            }
            case LOCATIONNAME:
            {
                string name = e.ReadAs<string>();
                if (Game1.getLocationFromName(name) is not null)
                {
                    LocationWatcher?.SeenLocations?.Add(name);
                }
                else
                {
                    this.Monitor.Log($"{name} is not a valid location.", LogLevel.Warn);
                }
                break;
            }
        }
    }
    #endregion
}
