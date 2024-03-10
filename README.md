### CS2 Reserved Slots plugin using [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp)

## <img src="https://cdn.discordapp.com/avatars/1195690503094673518/bc09a09dffd31aa168aa60242859ea48.png?size=128" width="30px" height="30px"> [Discord Support Server](https://discord.gg/sourcefactory)


Configuration in
```configs/plugins/ReservedSlots/ReservedSlots.json```

|   | What it does |
| ------------- | ------------- |
| `Flag for reserved slots`  | Reservation flag |
| `Admin flag for reserved slots`  | Reservation flag for Admins. When a player with an Admin reserved flag joins, no one is kicked (Leave blank for disable this option)|
| `Kick immunity type`  | Who will be immune to the kick? |
||`0` - Players with a Reserved flag or an Admin reserved flag |
||`1` - Only players with a Reserved flag|
||`2` - Only players with an Admin reserved flag|
| `Reserved slots` | How many slots will be reserved if the reserved slots method is 1 or 2 |
| `Reserved slots method` | `0` - There will always be one slot open. For example, if your maxplayers is set to 10, the server can have a maximum of 9 players. If a 10th player joins with a Reservation flag, it will kick a player based on the Kick type. If the 10th player doesn't have a reservation flag, they will be kicked |
||`1` - Maintains the number of available slots according to the reservation slots setting, allowing only players with a Reservation flag to join. For example, if you have maxplayers set to 10 and Reserved slots set to 3, when there are 7/10 players on the server, additional players can only join if they have a Reservation flag. If they don't, they will be kicked. If the server is already full and a player with a Reservation flag attempts to join, it will kick a player based on the Kick type |
||`2` - It works the same way as in method 2, except players with a Reservation flag are not counted towards the total player count. For example, if there are 7/10 players on the server, and Reserved slots are set to 3. Out of those 7 players, two players have a Reservation flag. The plugin will then consider that there are 5 players on the server, allowing two more players without a Reservation flag to connect. If the server is already full and a player with a Reservation flag attempts to join, it will kick a player based on the  Kick type |
| `Leave one slot open` | Works only if reserved slots method is set to 1 or 2. If set to `true`, there will always be one slot open. (`true` or `false`) |
| `Kick Check Method`  | When a player will be selected for kick when a player with a Reserved flag joins?? |
||`0` - When a player with a Reserved flag joins |
||`1` - When a player with a Reserved flag choose a team|
| `Kick type` | How is a players selected to be kicked? |
||`0` - Players will be kicked randomly |
||`1` -  Players will be kicked by highest ping|
||`2` -  Players will be kicked by highest score|
||`3` -  Players will be kicked by lowest score|
| `Kick players in spectate` | Kick players who are in spectate first? (`true` or `false`) |

### Installation
1. Download the lastest release https://github.com/NockyCZ/CS2-ReservedSlots/releases/latest
2. Unzip into your servers `csgo/addons/counterstrikesharp/plugins/` dir
