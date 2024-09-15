<p align="center">
<b>Reseved Slots</b> is a CS2 plugin that is used to reserve slots for VIP players or Admins.<br>
Designed for <a href="https://github.com/roflmuffin/CounterStrikeSharp">CounterStrikeSharp</a> framework<br>
<br>
<a href="https://buymeacoffee.com/sourcefactory">
<img src="https://img.buymeacoffee.com/button-api/?text=Support Me&emoji=🚀&slug=sourcefactory&button_colour=e6005c&font_colour=ffffff&font_family=Lato&outline_colour=000000&coffee_colour=FFDD00" />
</a>
</p>

### Discord Support Server
[<img src="https://discordapp.com/api/guilds/1149315368465211493/widget.png?style=banner2">](https://discord.gg/Tzmq98gwqF)

### Installation
1. Download the lastest release https://github.com/NockyCZ/CS2-ReservedSlots/releases/latest
2. Unzip into your servers `csgo/addons/counterstrikesharp/plugins/` dir

### Dependencies
- [CS2 Discord Utilities](https://github.com/NockyCZ/CS2-Discord-Utilities) (Only if you want to use Discord Roles in `Reserved Flags and Roles` or `Admin Flags and Roles`)

## 
Configuration in
```configs/plugins/ReservedSlots/ReservedSlots.json```

|   | What it does |
| ------------- | ------------- |
| `Reserved Flags and Roles`  | List of Flags or Discord Roles for reserved slot |
| `Reserved Admin Flags and Roles`  | List of Flags or Discord Roles for admin reserved slot. (When a player with an Admin reserved slot joins, no one is kicked (Leave empty for disable this option))|
| `Use Discord Utilities`  | Use the [Discord Utilities](https://github.com/NockyCZ/CS2-Discord-Utilities) plugin to detect player discord roles in **Reserved Flags and Roles** or  **Reserved Admin Flags and Roles** (`true` or `false`) |
| `Reserved Slots` | How many slots will be reserved if the reserved slots method is 1 or 2 |
| `Reserved Slots Method` | `0` - There will always be one slot open. For example, if your maxplayers is set to 10, the server can have a maximum of 9 players. If a 10th player joins with a Reservation flag/role, it will kick a player based on the Kick type. If the 10th player doesn't have a reservation flag/role, they will be kicked |
||`1` - Maintains the number of available slots according to the reservation slots setting, allowing only players with a Reservation flag/role to join. For example, if you have maxplayers set to 10 and Reserved slots set to 3, when there are 7/10 players on the server, additional players can only join if they have a Reservation flag/role. If they don't, they will be kicked. If the server is already full and a player with a Reservation flag/role attempts to join, it will kick a player based on the Kick type |
||`2` - It works the same way as in method 2, except players with a Reservation flag/role are not counted towards the total player count. For example, if there are 7/10 players on the server, and Reserved slots are set to 3. Out of those 7 players, two players have a Reservation flag/role. The plugin will then consider that there are 5 players on the server, allowing two more players without a Reservation flag/role to connect. If the server is already full and a player with a Reservation flag/role attempts to join, it will kick a player based on the  Kick type |
| `Leave One Slot Open` | Works only if reserved slots method is set to 1 or 2. If set to `true`, there will always be one slot open. (`true` or `false`) |
| `Kick Immunity Type`  | Who will be immune to the kick? |
||`0` - Players with a Reserved flag/role or an Admin reserved flag/role |
||`1` - Only players with a Reserved flag/role|
||`2` - Only players with an Admin reserved flag/role|
| `Kick Reason` | Reason for the kick (Use the number from [NetworkDisconnectionReason](https://docs.cssharp.dev/api/CounterStrikeSharp.API.ValveConstants.Protobuf.NetworkDisconnectionReason.html?q=NetworkDisconnectionReason)) |
| `Kick Delay` | This means that the player will be kicked after a certain time (`seconds`) (value less than 1 means the player will be kicked immediately)|
| `Kick Check Method`  | When a player will be selected for kick when a player with a Reserved flag/role joins?? |
||`0` - When a player with a Reserved flag/role joins |
||`1` - When a player with a Reserved flag/role choose a team|
| `Kick Type` | How is a players selected to be kicked? |
||`0` - Players will be kicked randomly |
||`1` -  Players will be kicked by highest ping|
||`2` -  Players will be kicked by highest score|
||`3` -  Players will be kicked by lowest score|
| `Kick Players In Spectate` | Kick players who are in spectate first? (`true` or `false`) |
| `Log Kicked Players` | (`true` or `false`) |
| `Display Kicked Players Message` | Who will see the message when a player is kicked due to a reserved slot |
||`0` - None |
||`1` - All players|
||`2` - Only Admins with the `@css/generic` flag|
