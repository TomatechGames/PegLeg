# PegLeg
PegLeg is a lightweight and ***unofficial*** companion app for use with Fortnite: Save The World. It essentially aims to recreate the functionality of the Homebase menu without needing the entire game to be running.
![A screenshot of the Missions tab](https://github.com/user-attachments/assets/b63bd5ef-38c5-450c-a0c2-d7fef81eb818)
![A screenshot of the Llamas tab](https://github.com/user-attachments/assets/9dc5b406-994a-47cb-bc1e-84a73b79792e)
![A screenshot of the Llamas tab while inspecting a choosable Llama item](https://github.com/user-attachments/assets/67785eab-ef2c-4f6c-890a-489dbb073d2f)

## Features
### Current Features
PegLeg currently suppoorts the following features:
- Browsing and filtering Missions and their rewards
- Viewing the contents of XRay Llamas in the Llama Shop (including reset countdowns)
- Purchasing Llamas from the Llama shop
- Viewing and Opening Llamas in your inventory
- Viewing the details of Heroes, Defenders, Survivors, and Schematics, including their stats (Heroes and Schematics), current perks and perk choices (Schematics), personality and set bonus (survivors), and more
- Viewing a list of every Hero and Schematic in the game
- Modifying Survivor Squads and creating/applying Survivor Squad presets
- Viewing and Purchasing items in the Weekly/Event shop (including reset countdowns)
- Viewing and filtering items in the Cosmetic shop (including per-item leaving countdowns, new item notifications, and more)
- Viewing, pinning, and rerolling Quests
- Customizable Themes that can auto-update to reflect the current Ventures Season

### Planned Features
Planned features include:
- Creating and modifying Hero loadouts
- Viewing and starting Expeditions
- A Dashboard tab to summarise relevant information on one screen
- Viewing a list of owned items (Heroes, Schematics, Survivors, etc) which can be upgraded, evolved, supercharged, and recycled
- Viewing and researching items in the Collection book
- A scrollable and searchable timeline of future events, questlines, shop items, and more
- An Android version of the app with a dedicated UI

## Generating Assets
In the event that a future update adds or changes an item in the game, PegLeg includes [a custom fork of BanjoBotAssets](https://github.com/TomatechGames/BanjoBotAssets), an application that can extract information and images of items, missions, and more[^1].
If you are using PegLeg for the first time, the application will prompt you to provide a path to the FortniteGame folder for BanjoBotAssets to extract the neccecary data during login. Otherwise, fresh extractions can be performed from the settings menu.


[^1]: If Fortnite changes how it formats it's data during a game update, BanjoBotAssets may fail to extract the data correctly. PegLeg may have reduced functionality until it is updated to support the latest data format
