NoDrop by CTS Kael

Version: 0.1.5
BE SURE TO DOWNLOAD LATEST RELEASE

https://github.com/KaelKodes/NoDrop/releases/tag/v0.1.5


Description:

The NoDrop plugin ensures that player inventories are saved when they die or disconnect, and their items are restored upon respawn without dropping any held items during wounding or death.

This plugin is HEAVILY INSPIRED by K1lly0u's Restore Upon Death and aims to more robustly cover gaps missed by it.



Features

Inventory Saving: Saves the player’s full inventory (Main, Belt, and Wear) when they die or disconnect while alive.

No Item Drop: Prevents players from dropping their active item when they are wounded or die.

Persistent Inventory: Restores the player's inventory exactly as it was upon their next respawn. Regardless of when they respawn next, allowing for safe disconnects and rage quits.

Handles All Death Types: Works under all death conditions, including suicide, terrain death, flyhack kills, and more, with configuration

Wipe-Safe: Automatically wipes player inventory data when a new server wipe occurs, if triggered to do so.

Data Storage: Each player's inventory is saved in individual files located in oxide/data/NoDrop, named after their SteamID.

Epic Loot Supported

Raidable Bases Supported

Bags of Holding Supported



Configuration

No Drop allows for the following configureable settings:

Drop Held Item on Death: false by default

Restore Inventory On Suicide: true by defualt

Restore Backpacks on Death: true by default (this also prevents backpacks from dropping during the wounded phase)




How It Works

On Player Death: When a player dies, their entire inventory (Main, Belt, Worn, and Bags) is saved to a user specific file. This ensures the player's items are kept intact and restored on their next respawn, and cant not be cross contaimenated with other players data.

On Disconnect While Alive: If a player disconnects while still alive, their current inventory is saved, preventing loss of items. This is just a fail safe.

Inventory Restoration: Upon respawn, if a player has saved inventory data, it is fully restored to them, including equipped items and those in their hotbar.

Wipe Handling: When a server wipe occurs, all saved player data is wiped as well, ensuring no old inventory data persists across wipes.



Intended Use

If you have ever wanted to have PVP without the grief of losing all your hard earned items, this is for your server!I made this for my own server after several other plugins claiming to do this, failed to perform reliably.I was able to replace SEVERAL PVE plugins with just this little baby.PVERs can feel safe knowing they wont lose anything, and PVPers can still hunt!



License

This project is licensed under the Apache license 2.0




Updates

0.0.4 - Added Config, Fixed Corpses Not Deleting, Fixed Rock and Torch spawning

0.0.5 - Added support for Backpacks and Weapon Attachments

0.0.8 - Support for Epic Loot,Fixed Backpack Dupe.

0.0.9 - Updated for Epic Loot changes, Added Bag of Holding support

0.1.0 - Fixed Backpack and BoH bug

0.1.1 - Added Wipe Options - In Testing

0.1.2 - Removed Suicide backpack duping

0.1.3 - Added Raidable Bases support

0.1.4 - Added Armor Insert support

0.1.5 - Patched Backpacks to save containers content inside them as well

