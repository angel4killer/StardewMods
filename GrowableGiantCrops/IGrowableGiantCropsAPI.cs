﻿using Microsoft.Xna.Framework;

using NetEscapades.EnumGenerators;

using StardewValley.TerrainFeatures;

#nullable enable

namespace GrowableGiantCrops;

/// <summary>
/// The api for this mod.
/// </summary>
public interface IGrowableGiantCropsAPI
{
    #region Tool methods

    /// <summary>
    /// Gets whether or not the current tool is a shovel.
    /// </summary>
    /// <param name="tool">Tool to check.</param>
    /// <returns>Whether or not the tool is a shovel.</returns>
    public bool IsShovel(Tool tool);

    /// <summary>
    /// Gets a shovel instance.
    /// </summary>
    /// <returns>A shovel.</returns>
    public Tool GetShovel();

    #endregion

    #region config
    public int MaxTreeStage { get; }
    public int MaxFruitTreeStage { get; }
    #endregion

    #region Generalized Placement

    /// <summary>
    /// Checks whether or an object managed by this mod.
    /// </summary>
    /// <param name="obj">Object to place.</param>
    /// <param name="loc">Game location.</param>
    /// <param name="tile">Tile to place at.</param>
    /// <param name="relaxed">Whether or not to use relaxed placement rules.</param>
    /// <returns>True if place-able.</returns>
    public bool CanPlace(StardewValley.Object obj, GameLocation loc, Vector2 tile, bool relaxed);

    /// <summary>
    /// Tries to place the object at this location.
    /// </summary>
    /// <param name="obj">Object to place.</param>
    /// <param name="loc">Game location.</param>
    /// <param name="tile">Tile to place at.</param>
    /// <param name="relaxed">Whether or not to use relaxed placement rules.</param>
    /// <returns>True if place-able.</returns>
    public bool TryPlace(StardewValley.Object obj, GameLocation loc, Vector2 tile, bool relaxed);

    /// <summary>
    /// Draws the pick up graphics for a specific item.
    /// </summary>
    /// <param name="obj">The object to draw for.</param>
    /// <param name="loc">The placement tile of the object. (As a general rule, pass in the tilelocation of the item.)</param>
    /// <param name="tile">The tile to draw it at.</param>
    public void DrawPickUpGraphics(StardewValley.Object obj, GameLocation loc, Vector2 tile);

    /// <summary>
    /// Helps animate a resource clump or large crop.
    /// </summary>
    /// <param name="loc">GameLocation.</param>
    /// <param name="tile">Tile to animate at.</param>
    /// <param name="texturePath">Path to the texture. Note that this should be valid for every player in multiplayer.</param>
    /// <param name="sourceRect">Sourcerect to use.</param>
    /// <param name="tileSize">The size of the item, in tiles.</param>
    public void DrawAnimations(GameLocation loc, Vector2 tile, string? texturePath, Rectangle sourceRect, Point tileSize);

    /// <summary>
    /// Checks to see something can be picked up from this location.
    /// </summary>
    /// <param name="loc">Game location.</param>
    /// <param name="tile">Tile to pick up from.</param>
    /// <param name="placedOnly">Whether or not we should be limited to just clump we placed.</param>
    /// <returns>True if something can be picked up here, false otherwise.</returns>
    public bool CanPickUp(GameLocation loc, Vector2 tile, bool placedOnly = false);

    /// <summary>
    /// Tries to pick up something from this location.
    /// </summary>
    /// <param name="loc">Game location.</param>
    /// <param name="tile">Tile to pick up from.</param>
    /// <param name="placedOnly">Whether or not we should be limited to just clump we placed.</param>
    /// <returns>The object if it was successfully picked up, null otherwise..</returns>
    public StardewValley.Object? TryPickUp(GameLocation loc, Vector2 tile, bool placedOnly = false);
    #endregion

    #region Resource Clump methods

    /// <summary>
    /// Gets the InventoryResourceClump associated with the specific ResourceClumpIndex, if valid.
    /// </summary>
    /// <param name="idx">Index to use.</param>
    /// <param name="initialStack">The initial stack size. Defaults to one.</param>
    /// <returns>InventoryResourceClump, null if invalid.</returns>
    public StardewValley.Object? GetResourceClump(ResourceClumpIndexes idx, int initialStack = 1);

    // Pintail currently does not proxy Nullable<TEnum>

    /// <summary>
    /// Gets the <see cref="ResourceClumpIndexes"/> associated with an item, or <see cref="ResourceClumpIndexes.Invalid"/>
    /// if it's not one.
    /// </summary>
    /// <param name="obj">The object to check.</param>
    /// <returns>A <see cref="ResourceClumpIndexes"/> (or <see cref="ResourceClumpIndexes.Invalid"/> if not applicable.).</returns>
    public ResourceClumpIndexes GetIndexOfClumpIfApplicable(StardewValley.Object obj);

    /// <summary>
    /// Gets the InventoryResourceClump or InventoryGiantCrop that matches this resource clump.
    /// </summary>
    /// <param name="resource">Resource clump.</param>
    /// <returns>Inventory item, or null for not applicable.</returns>
    public SObject? GetMatchingClump(ResourceClump resource);

    /// <summary>
    /// Checks whether or not a clump can be placed at this location.
    /// </summary>
    /// <param name="obj">Clump to place.</param>
    /// <param name="loc">Game location.</param>
    /// <param name="tile">Tile to place at.</param>
    /// <param name="relaxed">Whether or not to use relaxed placement rules.</param>
    /// <returns>True if place-able.</returns>
    public bool CanPlaceClump(StardewValley.Object obj, GameLocation loc, Vector2 tile, bool relaxed);

    /// <summary>
    /// Tries to place a clump at a specific location.
    /// </summary>
    /// <param name="obj">Clump to place.</param>
    /// <param name="loc">Game location.</param>
    /// <param name="tile">Tile to place at.</param>
    /// <param name="relaxed">Whether or not to use relaxed placement rules.</param>
    /// <returns>True if successfully placed.</returns>
    public bool TryPlaceClump(StardewValley.Object obj, GameLocation loc, Vector2 tile, bool relaxed);

    /// <summary>
    /// Checks to see if a clump can be picked up from this location.
    /// </summary>
    /// <param name="loc">Game location.</param>
    /// <param name="tile">Tile to pick up from.</param>
    /// <param name="placedOnly">Whether or not we should be limited to just clump we placed.</param>
    /// <returns>A ResourceClumpIndexes that corresponds size of the clump, or <see cref="ResourceClumpIndexes.Invalid"/> otherwise.</returns>
    public ResourceClumpIndexes CanPickUpClump(GameLocation loc, Vector2 tile, bool placedOnly = false);

    /// <summary>
    /// Checks to see if this specific clump can be picked up.
    /// </summary>
    /// <param name="clump">The clump in question.</param>
    /// <param name="placedOnly">If I should favor only placed clumps.</param>
    /// <returns>A ResourceClumpIndexes that corresponds size of the clump, or <see cref="ResourceClumpIndexes.Invalid"/> otherwise.</returns>
    public ResourceClumpIndexes CanPickUpClump(ResourceClump clump, bool placedOnly = false);

    /// <summary>
    /// Picks up a clump or giant crop if possible.
    /// </summary>
    /// <param name="loc">Game location.</param>
    /// <param name="tile">Tile to pick up from.</param>
    /// <param name="placedOnly">Whether or not we should be limited to just clumps or giant crops we placed.</param>
    /// <returns>InventoryGiantClump or InventoryGiantCrop if successfully picked up, null otherwise.</returns>
    /// This method also does NOT handle adding the item to the user's inventory.</remarks>
    public StardewValley.Object? TryPickUpClumpOrGiantCrop(GameLocation loc, Vector2 tile, bool placedOnly = false);

    #endregion

    #region GiantCrop

    /// <summary>
    /// Gets the identifiers for an inventory giant crop, if relevant.
    /// </summary>
    /// <param name="obj">Suspected inventory giant crop.</param>
    /// <returns>the product index and the GiantCropsTweaks string id, if relevant. Null otherwise.</returns>
    public (int idx, string? stringId)? GetIdentifiers(StardewValley.Object obj);

    /// <summary>
    /// Gets the InventoryGiantCrop associated with this produceIndex.
    /// </summary>
    /// <param name="produceIndex">the index of the produce.</param>
    /// <param name="initialStack">Initial stack.</param>
    /// <returns>The giant crop, or null for invalid.</returns>
    public StardewValley.Object? GetGiantCrop(int produceIndex, int initialStack);

    /// <summary>
    /// Gets the InventoryGiantCrop associated with this produceIndex.
    /// </summary>
    /// <param name="stringID">The string id for GiantCropsTweaks.</param>
    /// <param name="produceIndex">the index of the produce.</param>
    /// <param name="initialStack">Initial stack.</param>
    /// <returns>The giant crop, or null for invalid.</returns>
    public StardewValley.Object? GetGiantCrop(string stringID, int produceIndex, int initialStack);

    /// <summary>
    /// Checks whether or not a giant crop can be placed at this location.
    /// </summary>
    /// <param name="obj">Giant crop to place.</param>
    /// <param name="loc">Game location.</param>
    /// <param name="tile">Tile to place at.</param>
    /// <param name="relaxed">Whether or not to use relaxed placement rules.</param>
    /// <returns>True if place-able.</returns>
    public bool CanPlaceGiant(StardewValley.Object obj, GameLocation loc, Vector2 tile, bool relaxed);

    /// <summary>
    /// Tries to place a giant crop at a specific location.
    /// </summary>
    /// <param name="obj">giant crop to place.</param>
    /// <param name="loc">Game location.</param>
    /// <param name="tile">Tile to place at.</param>
    /// <param name="relaxed">Whether or not to use relaxed placement rules.</param>
    /// <returns>True if successfully placed.</returns>
    public bool TryPlaceGiant(StardewValley.Object obj, GameLocation loc, Vector2 tile, bool relaxed);

    /// <summary>
    /// Gets the InventoryGiantCrop associated with this giant crop.
    /// </summary>
    /// <param name="giant">The giant crop.</param>
    /// <returns>Inventory item, or null for invalid.</returns>
    public StardewValley.Object? GetMatchingCrop(GiantCrop giant);

    /// <summary>
    /// Whether or not this giant crop can be picked up.
    /// </summary>
    /// <param name="crop">Giant crop to check.</param>
    /// <param name="placedOnly">If we should restrict to just placed crops.</param>
    /// <returns>Identifier if it can be picked up, null otherwise.</returns>
    public (int idx, string? stringId)? CanPickUpCrop(GiantCrop crop, bool placedOnly);

    /// <summary>
    /// Whether or not a giant crop at this location can be picked up.
    /// </summary>
    /// <param name="loc">Game location to check.</param>
    /// <param name="tile">tile location to check.</param>
    /// <param name="placedOnly">Whether to check placed items only or not.</param>
    /// <returns>Identifier if there's a giant crop that can be picked up, null otherwise.</returns>
    public (int idx, string? stringId)? CanPickUpCrop(GameLocation loc, Vector2 tile, bool placedOnly);

    #endregion

    #region tree

    /// <summary>
    /// Gets the InventoryTree associated with the specific TeeIndex, if valid.
    /// </summary>
    /// <param name="idx">the index of the tree.</param>
    /// <param name="initialStack">The initial stack size.</param>
    /// <param name="growthStage">The growth stage of the tree. Vanilla trees range from 0 <see cref="Tree.seedStage"/> to 5 <see cref="Tree.treeStage"/>. Defaults to <see cref="Tree.bushStage"/></param>
    /// <param name="isStump">Whether or not the tree is a stump. This really only makes sense for <see cref="Tree.treeStage" />trees.</param>
    /// <returns>The InventoryTree, or null if invalid.</returns>
    public StardewValley.Object? GetTree(TreeIndexes idx, int initialStack = 1, int growthStage = Tree.bushStage, bool isStump = false);

    /// <summary>
    /// Checks whether or not a tree can be placed at this location.
    /// </summary>
    /// <param name="obj">Tree to place.</param>
    /// <param name="loc">Game location.</param>
    /// <param name="tile">Tile to place at.</param>
    /// <param name="relaxed">Whether or not to use relaxed placement rules.</param>
    /// <returns>True if place-able.</returns>
    public bool CanPlaceTree(StardewValley.Object obj, GameLocation loc, Vector2 tile, bool relaxed);

    /// <summary>
    /// Tries to place a tree at a specific location.
    /// </summary>
    /// <param name="obj">Tree to place.</param>
    /// <param name="loc">Game location.</param>
    /// <param name="tile">Tile to place at.</param>
    /// <param name="relaxed">Whether or not to use relaxed placement rules.</param>
    /// <returns>True if successfully placed.</returns>
    public bool TryPlaceTree(StardewValley.Object obj, GameLocation loc, Vector2 tile, bool relaxed);
    #endregion

    #region fruit tree

    /// <summary>
    /// Checks whether or not a fruit tree can be placed at this location.
    /// </summary>
    /// <param name="obj">Fruit tree to place.</param>
    /// <param name="loc">Game location.</param>
    /// <param name="tile">Tile to place at.</param>
    /// <param name="relaxed">Whether or not to use relaxed placement rules.</param>
    /// <returns>True if place-able.</returns>
    public bool CanPlaceFruitTree(StardewValley.Object obj, GameLocation loc, Vector2 tile, bool relaxed);

    /// <summary>
    /// Tries to place a fruit tree at a specific location.
    /// </summary>
    /// <param name="obj">Fruit tree to place.</param>
    /// <param name="loc">Game location.</param>
    /// <param name="tile">Tile to place at.</param>
    /// <param name="relaxed">Whether or not to use relaxed placement rules.</param>
    /// <returns>True if successfully placed.</returns>
    public bool TryPlaceFruitTree(StardewValley.Object obj, GameLocation loc, Vector2 tile, bool relaxed);
    #endregion
}

/// <summary>
/// The enum used for resource clumps.
/// Do not copy the [EnumExtensions] attribute, that is used for internal source generation.
/// </summary>
[EnumExtensions]
public enum ResourceClumpIndexes
{
    Stump = 600,
    HollowLog = 602,
    Meteorite = 622,
    Boulder = 672,
    MineRockOne = 752,
    MineRockTwo = 754,
    MineRockThree = 756,
    MineRockFour = 758,

    // usually would have just used null,
    // but Pintail can't proxy Nullable<TEnum> right now.

    /// <summary>
    /// Represents an invalid ResourceClumpIndex
    /// </summary>
    Invalid = -999,
}

/// <summary>
/// The enum used for different grass types.
/// Do not copy the [EnumExtensions] attribute, that is used for internal source generation.
/// </summary>
[EnumExtensions]
public enum GrassIndexes
{
    Spring = Grass.springGrass,
    Cave = Grass.caveGrass,
    Forst = Grass.frostGrass,
    Lava = Grass.lavaGrass,
    CaveTwo = Grass.caveGrass2,
    Cobweb = Grass.cobweb,

    // usually would have just used null,
    // but Pintail can't proxy Nullable<TEnum> right now.

    /// <summary>
    /// Represents an invalid GrassIndex
    /// </summary>
    Invalid = -999,
}

/// <summary>
/// The enum used for different tree types.
/// Do not copy the [EnumExtensions] attribute, that is used for internal source generation.
/// </summary>
[EnumExtensions]
public enum TreeIndexes
{
    Maple = Tree.bushyTree,
    Oak = Tree.leafyTree,
    Pine = Tree.pineTree,
    Palm = Tree.palmTree,
    BigPalm = Tree.palmTree2,
    Mahogany = Tree.mahoganyTree,
    Mushroom = Tree.mushroomTree,

    // usually would have just used null,
    // but Pintail can't proxy Nullable<TEnum> right now.

    /// <summary>
    /// Represents an invalid TreeIndex
    /// </summary>
    Invalid = -999,
}