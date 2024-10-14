using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("NoDrop", "CTS Kael", "0.0.1")]
    [Description("Saves and restores player inventories after death/disconnection, without dropping items.")]
    class NoDrop : RustPlugin
    {
        #region Fields
        private DynamicConfigFile inventoryDataFile;
        private Dictionary<ulong, PlayerInventoryData> playerInventories = new Dictionary<ulong, PlayerInventoryData>();
        private bool wipeDataOnWipe = false;

        private const string DataDirectory = "NoDrop";
        #endregion

        #region Oxide Hooks

        private void OnServerInitialized()
        {
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

            // Log that we are attempting to save the player's inventory on death
            Puts($"Player {player.displayName} ({player.UserIDString}) died, saving inventory.");

            // Save inventory even though the player is dead
            SavePlayerInventory(player);
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (player == null || !player.userID.IsSteamId()) return;

            // Only save the player's inventory if they are alive
            if (!player.IsDead())
            {
                Puts($"Player {player.displayName} ({player.UserIDString}) is disconnecting while alive. Saving inventory...");
                SavePlayerInventory(player);  // Save inventory if the player is alive
            }
            else
            {
                Puts($"Player {player.displayName} ({player.UserIDString}) is dead, skipping inventory save on disconnect.");
            }
        }


        private void OnPlayerRespawned(BasePlayer player)
        {
            if (player == null || !player.userID.IsSteamId()) return;
            RestorePlayerInventory(player);
        }

        // New Hook to prevent active item from dropping when wounded or killed
        private object CanDropActiveItem(BasePlayer player)
        {
            if (player == null || !player.userID.IsSteamId()) return null;

            // Prevent the active item from being dropped
            return false;  // Returning false prevents the item from dropping
        }

        #endregion

        #region Data Management

        private void LoadData()
        {
            Puts("Loading inventory data...");

            inventoryDataFile = Interface.Oxide.DataFileSystem.GetFile(DataDirectory);
            playerInventories = inventoryDataFile.ReadObject<Dictionary<ulong, PlayerInventoryData>>() ?? new Dictionary<ulong, PlayerInventoryData>();

            // Log the number of inventories loaded for debugging
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

        private void WipeData()
        {
            playerInventories.Clear();
            SaveData();
        }

        private void SavePlayerInventory(BasePlayer player)
        {
            if (player == null)
            {
                Puts("Player is null, skipping inventory save.");
                return;
            }

            // Removed the IsDead check to allow saving the inventory after death
            var inventoryData = new PlayerInventoryData
            {
                main = GetItemList(player.inventory.containerMain),
                belt = GetItemList(player.inventory.containerBelt),
                wear = GetItemList(player.inventory.containerWear)
            };

            // Log the inventory contents for debugging
            Puts($"Saving inventory for {player.displayName} ({player.UserIDString}): " +
                 $"Main: {inventoryData.main.Count} items, Belt: {inventoryData.belt.Count} items, Wear: {inventoryData.wear.Count} items.");

            playerInventories[player.userID] = inventoryData;
            SaveData();
            Puts($"Successfully saved inventory for {player.displayName} ({player.UserIDString})");
        }



        private void RestorePlayerInventory(BasePlayer player)
        {
            if (playerInventories.TryGetValue(player.userID, out var inventoryData))
            {
                RestoreItemList(player.inventory.containerMain, inventoryData.main);
                RestoreItemList(player.inventory.containerBelt, inventoryData.belt);
                RestoreItemList(player.inventory.containerWear, inventoryData.wear);

                player.ChatMessage("Your inventory has been restored.");
                playerInventories.Remove(player.userID); // Optional: Remove data after restoration
                SaveData();
            }
            else
            {
                Puts($"No inventory data found for {player.displayName} ({player.UserIDString})");
            }
        }

        #endregion

        #region Helper Functions

        private List<ItemData> GetItemList(ItemContainer container)
        {
            return container?.itemList.Select(item => new ItemData(item)).ToList() ?? new List<ItemData>();
        }

        private void RestoreItemList(ItemContainer container, List<ItemData> items)
        {
            foreach (var itemData in items)
            {
                var item = ItemManager.CreateByItemID(itemData.itemId, itemData.amount, itemData.skin);
                if (item != null)
                {
                    item.condition = itemData.condition;
                    item.MoveToContainer(container, itemData.position);
                }
            }
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

            public ItemData() { }

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
