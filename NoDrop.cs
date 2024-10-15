using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using UnityEngine;
// 0.0.8 Code Cleaned Up 
namespace Oxide.Plugins
{
    [Info("NoDrop", "CTS Kael", "0.0.8")]
    [Description("Saves and restores player inventories after death/disconnection, with configurable options.")]
    class NoDrop : RustPlugin
    {
        #region Fields
        private DynamicConfigFile inventoryDataFile;
        private Dictionary<ulong, PlayerInventoryData> playerInventories = new Dictionary<ulong, PlayerInventoryData>();
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
            wipeDataOnWipe = true;
            Puts("Wipe detected, clearing all inventory data.");
        }
        private void OnEntityDeath(BasePlayer player, HitInfo info)
        {
            if (player == null || !player.userID.IsSteamId()) return;

            // Check if death was by suicide and whether we should restore inventory
            if (info != null && info.damageTypes.GetMajorityDamageType() == Rust.DamageType.Suicide && !config.RestoreOnSuicide)
            {
                Puts($"Player {player.displayName} committed suicide. Not restoring inventory as per config.");
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
                    StripCorpseItems(corpse);  // Strip items from the corpse
                    corpse.Kill();  // Explicitly delete the corpse once
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
            foreach (var item in player.inventory.containerBelt.itemList.Concat(player.inventory.containerWear.itemList))
            {
                if (item.info.itemid == SmallBackpackItemId || item.info.itemid == LargeBackpackItemId)
                {
                    Puts($"Disabling drop for backpack with ID {item.info.itemid}.");
                    itemsToKeepEquipped.Add(item);  // Keep the backpack equipped, no need to move it
                }
            }

            // Ensure backpacks remain equipped without moving them to the main inventory
            foreach (var item in itemsToKeepEquipped)
            {
                Puts($"Backpack with ID {item.info.itemid} remains equipped in the player's belt or wear slot.");
            }
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            if (player == null || !player.userID.IsSteamId()) return;

            // Remove starting Rock and Torch from both belt and wear
            StripContainer(player.inventory.containerBelt);
            StripContainer(player.inventory.containerWear);

            // Restore the player's saved inventory, which includes the backpack
            RestorePlayerInventory(player);
        }

        // Function to remove all items from a container (e.g., belt or wear)
        private void StripContainer(ItemContainer container)
        {
            for (int i = container.itemList.Count - 1; i >= 0; i--)
            {
                var item = container.itemList[i];
                item.RemoveFromContainer();  // Remove from the container
                item.Remove();               // Delete the item
            }
        }

        // Handle active item drop based on config
        private object CanDropActiveItem(BasePlayer player)
        {
            if (player == null || !player.userID.IsSteamId()) return null;

            if (!config.DropHeldItemOnDeath)
            {
                return false;  // Prevent the active item from being dropped
            }

            return null;  // Allow the default behavior (drop item)
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
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<PluginConfig>();
                if (config == null)  // Check if config is null
                {
                    PrintError("Config is null, loading default configuration.");
                    LoadDefaultConfig();  // Load defaults if the config is null
                }
            }
            catch
            {
                PrintError("Error reading configuration file! Generating new config.");
                LoadDefaultConfig();  // Generate a default config if there's an error
            }
        }

        protected override void SaveConfig() => Config.WriteObject(config, true);
        protected override void LoadDefaultConfig()
        {
            config = new PluginConfig
            {
                DropHeldItemOnDeath = false,
                RestoreOnSuicide = true,
                RestoreBackpacksOnDeath = true  // Set the default value for restoring backpacks
            };
        }
        #endregion

        #region Data Management

        private void LoadData()
        {
            Puts("Loading inventory data...");

            inventoryDataFile = Interface.Oxide.DataFileSystem.GetFile(DataDirectory);
            playerInventories = inventoryDataFile.ReadObject<Dictionary<ulong, PlayerInventoryData>>() ?? new Dictionary<ulong, PlayerInventoryData>();

            Puts($"Loaded {playerInventories.Count} player inventories.");

            if (wipeDataOnWipe)
            {
                Puts("Wipe detected, clearing inventory data.");
                playerInventories.Clear();
            }
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
                wear = GetItemList(player.inventory.containerWear)
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
            foreach (var itemData in items)
            {
                // Create the item
                var item = ItemManager.CreateByItemID(itemData.itemId, itemData.amount, itemData.skin);
                item.condition = itemData.condition;
                item.maxCondition = itemData.maxCondition;  // Restore max condition
                item.MoveToContainer(container, itemData.position);

                // Restore text and display name
                item.text = itemData.itemText;
                item.name = itemData.displayName;

                // Restore instance data if present
                if (itemData.instanceData?.IsValid() ?? false)
                {
                    itemData.instanceData.Restore(item);
                }

                // Restore item flags
                item.flags |= itemData.flags;

                // Restore mods (attachments)
                if (itemData.mods != null && item.contents != null)
                {
                    foreach (var modData in itemData.mods)
                    {
                        var modItem = ItemManager.CreateByItemID(modData.itemId, modData.amount);
                        modItem.condition = modData.condition;
                        modItem.MoveToContainer(item.contents);
                    }
                }

                // Check if item has nested contents (e.g., backpacks)
                if (itemData.contents != null && item.contents != null)
                {
                    // Restore nested contents only if they are not already in the container
                    if (!item.contents.itemList.Any())  // Ensure the container is empty before restoring
                    {
                        foreach (var contentData in itemData.contents)
                        {
                            var contentItem = ItemManager.CreateByItemID(contentData.itemId, contentData.amount, contentData.skin);
                            contentItem.condition = contentData.condition;
                            contentItem.MoveToContainer(item.contents);
                        }
                    }
                }

                // Restore Ammo
                if (item.GetHeldEntity() is BaseProjectile projectile)
                {
                    projectile.primaryMagazine.contents = itemData.ammo;
                    projectile.primaryMagazine.ammoType = ItemManager.FindItemDefinition(itemData.ammoType);
                }

                // Restore RF frequency for RF-enabled items (e.g., pagers)
                if (item.GetHeldEntity() is PagerEntity pagerEntity)
                {
                    pagerEntity.ChangeFrequency(itemData.frequency);
                }

                // Mark the item as dirty to apply changes
                item.MarkDirty();
            }
        }
        private void ReapplyEpicLootProperties(Item item)
        {
            // Step 1: Ensure that Epic Loot properties are in item.text
            if (!string.IsNullOrEmpty(item.text))
            {
                // Since all critical buffs, rarity, and enhancements are likely stored in item.text,
                // simply restoring it should be enough. You don’t need to manually reapply individual buffs here.
                item.MarkDirty();  // Mark the item as dirty to apply any changes
            }

            // Step 2: If Epic Loot uses `instanceData` for additional buffs or properties, ensure it's restored
            if (item.instanceData != null)
            {
                item.MarkDirty();  // Mark the item as dirty to ensure any changes are applied
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
            return container?.itemList.Select(item => new ItemData(item)).ToList() ?? new List<ItemData>();
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
            public int frequency;  // RF Frequency
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
                contents = item.contents?.itemList.Select(contentItem => new ItemData(contentItem)).ToList();

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
