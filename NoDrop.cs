using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using UnityEngine;

// 0.1.1 Added Configurable Wipe Options - Code Cleaned Up
namespace Oxide.Plugins
{
    [Info("NoDrop", "CTS Kael", "0.1.1")]
    [Description(
        "Saves and restores player inventories after death/disconnection, with configurable options."
    )]
    class NoDrop : RustPlugin
    {
        #region Fields
        private DynamicConfigFile inventoryDataFile;
        private Dictionary<ulong, PlayerInventoryData> playerInventories =
            new Dictionary<ulong, PlayerInventoryData>();
        private bool wipeDataOnWipe = false;
        private const int SmallBackpackItemId = 2068884361; // ID for Small Backpack
        private const int LargeBackpackItemId = -907422733; // ID for Large Backpack

        private const string DataDirectory = "NoDrop";
        private PluginConfig config;
        #endregion

        #region Oxide Hooks

        private void OnServerInitialized()
        {
            // Load configuration
            LoadConfig();

            // Load player inventory data from the data file
            LoadData();

            // Configure backpack drop behavior based on the config
            EnableBackpackDropOnDeath(SmallBackpackItemId, !config.RestoreBackpacksOnDeath);
            EnableBackpackDropOnDeath(LargeBackpackItemId, !config.RestoreBackpacksOnDeath);
        }

        private void OnServerSave() => SaveData();

        private void Unload()
        {
            EnableBackpackDropOnDeath(SmallBackpackItemId, true);
            EnableBackpackDropOnDeath(LargeBackpackItemId, true);

            SaveData();
        }

        private void OnNewSave(string filename)
        {
            wipeDataOnWipe = !config.RestoreInventoryOnWipe;

            if (wipeDataOnWipe)
                Puts("Wipe detected, inventory data will be cleared as per config.");
            else
                Puts("Wipe detected, but inventory will be preserved (Restore Inventory on Wipe: true).");
        }


        private void OnEntityDeath(BasePlayer player, HitInfo info)
        {
            if (player == null || !player.userID.IsSteamId())
                return;

            // Check if death was by suicide and whether we should restore inventory
            if (
                info != null
                && info.damageTypes.GetMajorityDamageType() == Rust.DamageType.Suicide
                && !config.RestoreOnSuicide
            )
            {
                Puts(
                    $"Player {player.displayName} committed suicide. Not restoring inventory as per config."
                );
                return;
            }

            // Prevent backpack from dropping if the config is set to restore backpacks
            if (config.RestoreBackpacksOnDeath)
            {
                DisableBackpackDrop(player);
            }

            // Log that we are attempting to save the player's inventory on death
            Puts($"Player {player.displayName} ({player.UserIDString}) died, saving inventory.");
            SavePlayerInventory(player);

            // Strip and delete the corpse once, avoiding duplicate triggers
            NextTick(() =>
            {
                var corpse = FindPlayerCorpse(player.userID);
                if (corpse != null)
                {
                    StripCorpseItems(corpse); // Strip items from the corpse
                    corpse.Kill(); // Explicitly delete the corpse once
                    Puts($"Corpse for {corpse.playerName} has been deleted.");
                }
            });
        }

        // This method will stop the backpack from dropping
        private void DisableBackpackDrop(BasePlayer player)
        {
            // Create a list to store items to modify
            List<Item> itemsToKeepEquipped = new List<Item>();

            // Iterate through player's belt and wear inventory to find the backpack
            foreach (
                var item in player.inventory.containerBelt.itemList.Concat(
                    player.inventory.containerWear.itemList
                )
            )
            {
                if (
                    item.info.itemid == SmallBackpackItemId
                    || item.info.itemid == LargeBackpackItemId
                )
                {
                    Puts($"Disabling drop for backpack with ID {item.info.itemid}.");
                    itemsToKeepEquipped.Add(item); // Keep the backpack equipped, no need to move it
                }
            }

            // Ensure backpacks remain equipped without moving them to the main inventory
            foreach (var item in itemsToKeepEquipped)
            {
                Puts(
                    $"Backpack with ID {item.info.itemid} remains equipped in the player's belt or wear slot."
                );
            }
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            if (player == null || !player.userID.IsSteamId())
                return;

            // Remove starting Rock and Torch from both belt and wear
            StripContainer(player.inventory.containerBelt);
            StripContainer(player.inventory.containerWear);

            // Restore the player's saved inventory, which includes the backpack
            RestorePlayerInventory(player);
        }

        // Remove all items from a container (e.g., belt or wear)
        private void StripContainer(ItemContainer container)
        {
            for (int i = container.itemList.Count - 1; i >= 0; i--)
            {
                var item = container.itemList[i];
                item.RemoveFromContainer();
                item.Remove();
            }
        }

        // Handle active item drop based on config
        private object CanDropActiveItem(BasePlayer player)
        {
            if (player == null || !player.userID.IsSteamId())
                return null;

            if (!config.DropHeldItemOnDeath)
            {
                return false;
            }

            return null;
        }

        #endregion

        #region Config Management

        private class PluginConfig
        {
            [JsonProperty("Drop Held Item on Death")]
            public bool DropHeldItemOnDeath { get; set; } = false;

            [JsonProperty("Restore Inventory on Suicide")]
            public bool RestoreOnSuicide { get; set; } = true;

            [JsonProperty("Restore Backpacks on Death")]
            public bool RestoreBackpacksOnDeath { get; set; } = true;

            [JsonProperty("Restore Inventory on Wipe")]
            public bool RestoreInventoryOnWipe { get; set; } = false;

        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<PluginConfig>();
                if (config == null)
                {
                    PrintError("Config is null, loading default configuration.");
                    LoadDefaultConfig();
                }
            }
            catch
            {
                PrintError("Error reading configuration file! Generating new config.");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(config, true);

        protected override void LoadDefaultConfig()
        {
            config = new PluginConfig
            {
                DropHeldItemOnDeath = false,
                RestoreOnSuicide = true,
                RestoreBackpacksOnDeath = true,
                RestoreInventoryOnWipe = true
            };
        }

        #endregion

        #region Data Management

        private void LoadData()
        {
            Puts("Loading inventory data...");

            inventoryDataFile = Interface.Oxide.DataFileSystem.GetFile(DataDirectory);
            playerInventories =
                inventoryDataFile.ReadObject<Dictionary<ulong, PlayerInventoryData>>()
                ?? new Dictionary<ulong, PlayerInventoryData>();

            if (wipeDataOnWipe)
            {
                Puts("Clearing inventory data due to wipe + config setting.");
                playerInventories.Clear();
                inventoryDataFile.Clear();
                inventoryDataFile.Save();
            }

            Puts($"Loaded {playerInventories.Count} player inventories.");
        }


        private void SaveData()
        {
            inventoryDataFile.WriteObject(playerInventories);
        }

        private void SavePlayerInventory(BasePlayer player)
        {
            var inventoryData = new PlayerInventoryData
            {
                main = GetItemList(player.inventory.containerMain),
                belt = GetItemList(player.inventory.containerBelt),
                wear = GetItemList(player.inventory.containerWear),
            };

            playerInventories[player.userID] = inventoryData;
            SaveData();
            Puts($"Successfully saved inventory for {player.displayName}.");
        }

        private void RestorePlayerInventory(BasePlayer player)
        {
            if (playerInventories.TryGetValue(player.userID, out var inventoryData))
            {
                // Restore the player's main inventory, belt, and wear (which includes the backpack)
                Puts($"Restoring inventory for player {player.displayName}.");
                RestoreItemList(player.inventory.containerMain, inventoryData.main);
                RestoreItemList(player.inventory.containerBelt, inventoryData.belt);
                RestoreItemList(player.inventory.containerWear, inventoryData.wear);

                // Clear inventory data after restoring to avoid repeated restores
                playerInventories.Remove(player.userID);
                SaveData();

                player.ChatMessage("Your inventory has been restored.");
            }
        }

        #endregion

        #region Helper Functions
        private void RestoreItemList(ItemContainer container, List<ItemData> items)
        {
            if (container == null || items == null) return;

            // 🧼 Clear container first to avoid duplication
            foreach (var existing in container.itemList.ToList())
            {
                existing.RemoveFromContainer();
                existing.Remove();
            }

            foreach (var itemData in items)
            {
                var item = ItemManager.CreateByItemID(itemData.itemId, itemData.amount, itemData.skin);
                if (item == null) continue;

                item.condition = itemData.condition;
                item.maxCondition = itemData.maxCondition;
                item.position = itemData.position;
                item.text = itemData.itemText;
                item.name = itemData.displayName;
                item.flags |= itemData.flags;

                // Restore instance data (EpicLoot blueprint/stats)
                if (itemData.instanceData?.IsValid() ?? false)
                    itemData.instanceData.Restore(item);

                // Move item to target container
                item.MoveToContainer(container, itemData.position);

                // 🔧 Restore mods (attachments like weapon mods)
                if (itemData.mods != null && item.contents != null)
                {
                    foreach (var modData in itemData.mods)
                    {
                        var modItem = ItemManager.CreateByItemID(modData.itemId, modData.amount);
                        if (modItem == null) continue;

                        modItem.condition = modData.condition;
                        modItem.MoveToContainer(item.contents);
                        modItem.MarkDirty();
                    }
                }

                // 🧰 Clear item.contents before restoring nested contents
                if (item.contents != null)
                {
                    foreach (var existing in item.contents.itemList.ToList())
                    {
                        existing.RemoveFromContainer();
                        existing.Remove();
                    }
                }

                // 🔁 Restore nested contents (backpacks, boxes, etc.)
                if (itemData.contents != null && item.contents != null)
                {
                    foreach (var contentData in itemData.contents)
                    {
                        var contentItem = ItemManager.CreateByItemID(contentData.itemId, contentData.amount, contentData.skin);
                        if (contentItem == null) continue;

                        contentItem.condition = contentData.condition;
                        contentItem.maxCondition = contentData.maxCondition;
                        contentItem.position = contentData.position;
                        contentItem.text = contentData.itemText;
                        contentItem.name = contentData.displayName;
                        contentItem.flags |= contentData.flags;

                        if (contentData.instanceData?.IsValid() ?? false)
                            contentData.instanceData.Restore(contentItem);

                        contentItem.MoveToContainer(item.contents);

                        // 🧼 Clean contents before deeper nested restore
                        if (contentItem.contents != null)
                        {
                            foreach (var existing in contentItem.contents.itemList.ToList())
                            {
                                existing.RemoveFromContainer();
                                existing.Remove();
                            }
                        }

                        // 🔁 Recursively restore deeper nested contents
                        if (contentData.contents != null && contentItem.contents != null)
                        {
                            foreach (var subContent in contentData.contents)
                            {
                                var subItem = ItemManager.CreateByItemID(subContent.itemId, subContent.amount, subContent.skin);
                                if (subItem == null) continue;

                                subItem.condition = subContent.condition;
                                subItem.maxCondition = subContent.maxCondition;
                                subItem.position = subContent.position;
                                subItem.text = subContent.itemText;
                                subItem.name = subContent.displayName;
                                subItem.flags |= subContent.flags;

                                if (subContent.instanceData?.IsValid() ?? false)
                                    subContent.instanceData.Restore(subItem);

                                subItem.MoveToContainer(contentItem.contents);
                                subItem.MarkDirty();
                            }
                        }

                        contentItem.MarkDirty();
                    }
                }

                // 🔫 Restore ammo if it's a weapon
                if (item.GetHeldEntity() is BaseProjectile projectile)
                {
                    projectile.primaryMagazine.contents = itemData.ammo;
                    projectile.primaryMagazine.ammoType = ItemManager.FindItemDefinition(itemData.ammoType);
                }

                // 📡 Restore RF frequency if it's a pager
                if (item.GetHeldEntity() is PagerEntity pagerEntity)
                {
                    pagerEntity.ChangeFrequency(itemData.frequency);
                }

                // 🎯 Final EpicLoot visual/data re-apply
                if (!string.IsNullOrEmpty(item.text) || item.instanceData != null)
                    item.MarkDirty();

                item.MarkDirty();
            }
        }



        private void ReapplyEpicLootProperties(Item item)
        {
            // Step 1: Verify
            if (!string.IsNullOrEmpty(item.text))
            {
                item.MarkDirty();
            }

            // Step 2: Restore
            if (item.instanceData != null)
            {
                item.MarkDirty();
            }
        }

        private PlayerCorpse FindPlayerCorpse(ulong userID)
        {
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                var corpse = entity as PlayerCorpse;
                if (corpse != null && corpse.playerSteamID == userID)
                {
                    return corpse;
                }
            }
            return null;
        }

        private void StripCorpseItems(PlayerCorpse corpse)
        {
            foreach (var container in corpse.containers)
            {
                for (int i = container.itemList.Count - 1; i >= 0; i--)
                {
                    var item = container.itemList[i];
                    item.RemoveFromContainer();
                    item.Remove();
                }
            }
            Puts($"Corpse items for {corpse.playerName} have been deleted.");
        }

        private void EnableBackpackDropOnDeath(int itemId, bool enabled)
        {
            var itemDef = ItemManager.FindItemDefinition(itemId);
            if (itemDef == null)
            {
                PrintError($"Could not find item definition for item ID {itemId}");
                return;
            }

            var itemModBackpack = itemDef.GetComponent<ItemModBackpack>();
            if (itemModBackpack == null)
            {
                PrintError($"Item with ID {itemId} does not have a backpack component.");
                return;
            }

            itemModBackpack.DropWhenDowned = enabled;
        }

        #endregion

        #region Classes

        private class PlayerInventoryData
        {
            public List<ItemData> main;
            public List<ItemData> belt;
            public List<ItemData> wear;
            public List<ItemData> backpackContents;
        }

        private List<ItemData> GetItemList(ItemContainer container)
        {
            return container?.itemList.Select(item => new ItemData(item)).ToList()
                ?? new List<ItemData>();
        }

        public class ItemData
        {
            public int itemId;
            public int amount;
            public ulong skin;
            public float condition;
            public float maxCondition;
            public int position;
            public string itemText;
            public string displayName;
            public int ammo;
            public string ammoType;
            public int frequency; // RF Frequency
            public Item.Flag flags;
            public List<ItemModData> mods;
            public List<ItemData> contents;
            public InstanceData instanceData;
            public int blueprintAmount;
            public int blueprintTarget;

            public ItemData(Item item)
            {
                itemId = item.info.itemid;
                amount = item.amount;
                skin = item.skin;
                condition = item.condition;
                maxCondition = item.maxCondition;
                position = item.position;
                itemText = item.text;
                displayName = item.name;

                flags = item.flags;
                mods = item.contents?.itemList.Select(mod => new ItemModData(mod)).ToList();
                contents = item
                    .contents?.itemList.Select(contentItem => new ItemData(contentItem))
                    .ToList();

                // Capture ammo for weapons
                if (item.GetHeldEntity() is BaseProjectile projectile)
                {
                    ammo = projectile.primaryMagazine.contents;
                    ammoType = projectile.primaryMagazine.ammoType?.shortname;
                }

                // Capture RF frequency (for items like pagers)
                if (item.GetHeldEntity() is PagerEntity pagerEntity)
                {
                    frequency = pagerEntity.GetFrequency();
                }

                // Capture instance data
                if (item.instanceData != null)
                {
                    instanceData = new InstanceData(item);
                    blueprintAmount = item.instanceData.blueprintAmount;
                    blueprintTarget = item.instanceData.blueprintTarget;
                }
            }
        }

        public class ItemModData
        {
            public int itemId;
            public int amount;
            public float condition;

            public ItemModData(Item modItem)
            {
                itemId = modItem.info.itemid;
                amount = modItem.amount;
                condition = modItem.condition;
            }
        }

        public class InstanceData
        {
            public int dataInt;
            public int blueprintTarget;
            public int blueprintAmount;

            public InstanceData(Item item)
            {
                if (item.instanceData != null)
                {
                    dataInt = item.instanceData.dataInt;
                    blueprintTarget = item.instanceData.blueprintTarget;
                    blueprintAmount = item.instanceData.blueprintAmount;
                }
            }

            public void Restore(Item item)
            {
                if (item.instanceData == null)
                    item.instanceData = new ProtoBuf.Item.InstanceData();

                item.instanceData.ShouldPool = false;
                item.instanceData.blueprintAmount = blueprintAmount;
                item.instanceData.blueprintTarget = blueprintTarget;
                item.instanceData.dataInt = dataInt;

                item.MarkDirty();
            }

            public bool IsValid()
            {
                return dataInt != 0 || blueprintAmount != 0 || blueprintTarget != 0;
            }
        }

        #endregion
    }
}
