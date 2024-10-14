using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("NoDrop", "CTS Kael", "0.0.4")]
    [Description("Saves and restores player inventories after death/disconnection, with configurable options.")]
    class NoDrop : RustPlugin
    {
        #region Fields
        private DynamicConfigFile inventoryDataFile;
        private Dictionary<ulong, PlayerInventoryData> playerInventories = new Dictionary<ulong, PlayerInventoryData>();
        private bool wipeDataOnWipe = false;

        private const string DataDirectory = "NoDrop";
        private PluginConfig config;
        #endregion

        #region Oxide Hooks

        private void OnServerInitialized()
        {
            // Ensure the config is loaded properly
            LoadConfigValues();
            LoadData();
        }

        private void OnServerSave() => SaveData();

        private void Unload() => SaveData();

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

            // Log that we are attempting to save the player's inventory on death
            Puts($"Player {player.displayName} ({player.UserIDString}) died, saving inventory.");
            SavePlayerInventory(player);

            // Strip corpse to avoid duplication
            NextTick(() =>
            {
                var corpse = FindPlayerCorpse(player.userID);
                if (corpse != null)
                {
                    StripCorpseItems(corpse);
                }
            });
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            if (player == null || !player.userID.IsSteamId()) return;

            // Remove starting Rock and Torch from the player's belt
            StripContainer(player.inventory.containerBelt);

            // Restore the player's saved inventory
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
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<PluginConfig>();
            }
            catch
            {
                PrintError("Error reading configuration file! Generating new config.");
                LoadDefaultConfig();  // Generate a default config to avoid null issues
            }
        }

        protected override void LoadDefaultConfig() => config = new PluginConfig();

        protected override void SaveConfig() => Config.WriteObject(config, true);

        private void LoadConfigValues()
        {
            // Make sure the config is not null before trying to access it
            if (config == null)
            {
                PrintError("Config not loaded, creating a new one.");
                LoadDefaultConfig();
                SaveConfig();
            }

            Puts($"Loaded config: DropHeldItemOnDeath = {config.DropHeldItemOnDeath}, RestoreOnSuicide = {config.RestoreOnSuicide}");
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
                RestoreItemList(player.inventory.containerMain, inventoryData.main);
                RestoreItemList(player.inventory.containerBelt, inventoryData.belt);
                RestoreItemList(player.inventory.containerWear, inventoryData.wear);

                player.ChatMessage("Your inventory has been restored.");
                playerInventories.Remove(player.userID);
                SaveData();
            }
        }

        #endregion

        #region Helper Functions

        private List<ItemData> GetItemList(ItemContainer container) => container?.itemList.Select(item => new ItemData(item)).ToList() ?? new List<ItemData>();

        private void RestoreItemList(ItemContainer container, List<ItemData> items)
        {
            foreach (var itemData in items)
            {
                var item = ItemManager.CreateByItemID(itemData.itemId, itemData.amount, itemData.skin);
                item.condition = itemData.condition;
                item.MoveToContainer(container, itemData.position);
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

        #endregion

        #region Classes

        private class PlayerInventoryData
        {
            public List<ItemData> main;
            public List<ItemData> belt;
            public List<ItemData> wear;
        }

        private class ItemData
        {
            public int itemId;
            public int amount;
            public ulong skin;
            public float condition;
            public int position;

            public ItemData(Item item)
            {
                itemId = item.info.itemid;
                amount = item.amount;
                skin = item.skin;
                condition = item.condition;
                position = item.position;
            }
        }

        #endregion
    }
}
