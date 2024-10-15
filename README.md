NoDrop by CTS Kael

Version: 0.0.8

Description:

The NoDrop plugin ensures that player inventories are saved when they die or disconnect, and their items are restored upon respawn without dropping any held items during wounding or death.This plugin is HEAVILY INSPIRED by K1lly0u's Restore Upon Death and aims to more robustly cover gaps missed by it.

Features

Inventory Saving: Saves the player’s full inventory (Main, Belt, and Wear) when they die or disconnect while alive.

No Item Drop: Prevents players from dropping their active item when they are wounded or die.

Persistent Inventory: Restores the player's inventory exactly as it was upon their next respawn. Regardless of when they respawn next, allowing for safe disconnects and rage quits.

Handles All Death Types: Works under all death conditions, including suicide, terrain death, flyhack kills, and more.

Wipe-Safe: Automatically wipes player inventory data when a new server wipe occurs.

Data Storage: Each player's inventory is saved in individual files located in oxide/data/NoDrop, named after their SteamID.

Configuration

No Drop allows for the following configureable settings:

Drop Held Item on Death: false by default

Restore Inventory On Suicide: true by defualt

Restore Backpacks on Death: true by default (this also prevents backpacks from dropping during the wounded phase)

How It Works

On Player Death: When a player dies, their entire inventory (Main, Belt, and Wear) is saved to a user specific file. This ensures the player's items are kept intact and restored on their next respawn, and cant not be cross contaimenated with other players data.

On Disconnect While Alive: If a player disconnects while still alive, their current inventory is saved, preventing loss of items. This is just a fail safe.

Inventory Restoration: Upon respawn, if a player has saved inventory data, it is fully restored to them, including equipped items and those in their hotbar.

Wipe Handling: When a server wipe occurs, all saved player data is wiped as well, ensuring no old inventory data persists across wipes.

Intended Use

If you have ever wanted to have PVP without the grief of losing all your hard earned items, this is for your server! I made this for my own server after several other plugins claiming to do this, failed to perform reliably. I was able to replace SEVERAL PVE plugins with just this little baby. PVEs can feel safe knowing they wont lose anything, and PVPers can still hunt.

Future Improvements

ByPass Flag:

For Events, or Defined zones you may want to allow either Held Items or full inventory drop!

Log File:

To Log players saves. This would be a bit mildly costly to run, but would grant admins a way to verify players error claims. Would be enabled through config.

License

This project is licensed under the Apache license 2.0

Updates

0.0.4 - Added Config, Fixed Corpses Not Deleting, Fixed Rock and Torch spawning

0.0.5 - Added support for Backpacks and Weapon Attachments

0.0.8 - Support for Epic Loot, Bags of Holding, likely any other plugin like that as well. Fixed Backpack Dupe.
