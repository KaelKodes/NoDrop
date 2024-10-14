#NoDrop by CTS Kael
Version: 0.0.1
Description: The NoDrop plugin ensures that player inventories are saved when they die or disconnect, and their items are restored upon respawn without dropping any held items during wounding or death.

Features
Inventory Saving: Saves the player’s full inventory (Main, Belt, and Wear) when they die or disconnect while alive.
No Item Drop: Prevents players from dropping their active item when they are wounded or die.
Persistent Inventory: Restores the player's inventory exactly as it was upon their next respawn.
Handles All Death Types: Works under all death conditions, including suicide, terrain death, flyhack kills, and more.
Wipe-Safe: Automatically wipes player inventory data when a new server wipe occurs.
Data Storage: Each player's inventory is saved in individual files located in oxide/data/NoDrop, named after their SteamID.

Configuration
No configuration is required for the basic functionality of the plugin. However, you can modify it to your needs by extending the plugin code for specific server requirements.

How It Works
On Player Death: When a player dies, their entire inventory (Main, Belt, and Wear) is saved to a file. This ensures the player's items are kept intact and restored on their next respawn.
On Disconnect While Alive: If a player disconnects while still alive, their current inventory is saved, preventing loss of items.
Inventory Restoration: Upon respawn, if a player has saved inventory data, it is fully restored to them, including equipped items and those in their hotbar.
Wipe Handling: When a server wipe occurs, all saved player data is wiped as well, ensuring no old inventory data persists across wipes.

Known Issues
Currently, there are no known issues. Please report any problems via the plugin’s repository or support channel.

License
This project is licensed under the Apache license 2.0
