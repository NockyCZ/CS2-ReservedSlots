using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Modules.Timers;
using Microsoft.Extensions.Logging;
using DiscordUtilitiesAPI;
using CounterStrikeSharp.API.Core.Capabilities;

namespace ReservedSlots;

public class ReservedSlotsConfig : BasePluginConfig
{
    [JsonPropertyName("Reserved Flags and Roles")] public List<string> reservedFlagsAndRoles { get; set; } = new() { "@css/reservation", "DISCORD_ROLE_ID" };
    [JsonPropertyName("Admin Reserved Flags and Roles")] public List<string> adminFlagsAndRoles { get; set; } = new() { "@css/ban", "DISCORD_ROLE_ID" };
    [JsonPropertyName("Reserved Slots")] public int reservedSlots { get; set; } = 1;
    [JsonPropertyName("Reserved Slots Method")] public int reservedSlotsMethod { get; set; } = 0;
    [JsonPropertyName("Leave One Slot Open")] public bool openSlot { get; set; } = false;
    [JsonPropertyName("Kick Immunity Type")] public int kickImmunity { get; set; } = 0;
    [JsonPropertyName("Kick Reason")] public int kickReason { get; set; } = 135;
    [JsonPropertyName("Kick Delay")] public int kickDelay { get; set; } = 5;
    [JsonPropertyName("Kick Check Method")] public int kickCheckMethod { get; set; } = 0;
    [JsonPropertyName("Kick Type")] public int kickType { get; set; } = 0;
    [JsonPropertyName("Kick Players In Spectate")] public bool kickPlayersInSpectate { get; set; } = true;
    [JsonPropertyName("Log Kicked Players")] public bool logKickedPlayers { get; set; } = true;
    [JsonPropertyName("Display Kicked Players Message")] public int displayKickedPlayers { get; set; } = 2;
}

public class ReservedSlots : BasePlugin, IPluginConfig<ReservedSlotsConfig>
{
    public override string ModuleName => "Reserved Slots (With DU Support)";
    public override string ModuleAuthor => "Nocky (SourceFactory.eu)";
    public override string ModuleVersion => "1.1.0";

    public enum KickType
    {
        Random,
        HighestPing,
        HighestScore,
        LowestScore,
        //HighestTime,
    }

    public enum KickReason
    {
        ServerIsFull,
        ReservedPlayerJoined,
    }

    public enum ReservedType
    {
        VIP,
        Admin,
        None
    }

    public List<int> waitingForSelectTeam = new();
    public Dictionary<int, bool> reservedPlayers = new();
    public Dictionary<int, KickReason> waitingForKick = new();
    public ReservedSlotsConfig Config { get; set; } = new();
    private IDiscordUtilitiesAPI? DiscordUtilities { get; set; }
    public void OnConfigParsed(ReservedSlotsConfig config)
    {
        Config = config;
        if (!Config.reservedFlagsAndRoles.Any())
            SendConsoleMessage("[Reserved Slots] Reserved Flags and Roles cannot be empty!", ConsoleColor.Red);
    }

    public override void Load(bool hotReload)
    {
        RegisterListener<Listeners.OnTick>(() =>
        {
            if (waitingForKick.Count > 0)
            {
                foreach (var item in waitingForKick)
                {
                    var player = Utilities.GetPlayerFromSlot(item.Key);
                    if (player != null && player.IsValid)
                    {
                        var kickMessage = item.Value == KickReason.ServerIsFull ? Localizer["Hud.ServerIsFull"] : Localizer["Hud.ReservedPlayerJoined"];
                        player.PrintToCenterHtml(kickMessage);
                    }
                }
            }
        });
    }

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        try
        {
            DiscordUtilities = new PluginCapability<IDiscordUtilitiesAPI>("discord_utilities").Get();
            if (DiscordUtilities == null)
            {
                SendConsoleMessage($"[Reserved Slots] Discord Utilities plugin was not found! (https://github.com/NockyCZ/CS2-Discord-Utilities)", ConsoleColor.Red);
            }
        }
        catch (Exception ex)
        {
            SendConsoleMessage($"[Reserved Slots] An error occurred while loading Discord Utilities: {ex.Message}", ConsoleColor.Red);
        }
    }

    [GameEventHandler(HookMode.Pre)]
    public HookResult OnPlayerTeam(EventPlayerTeam @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player != null && player.IsValid && waitingForSelectTeam.Contains(player.Slot))
        {
            waitingForSelectTeam.Remove(player.Slot);
            var kickedPlayer = getPlayerToKick(player);
            if (kickedPlayer != null)
            {
                PerformKick(kickedPlayer, KickReason.ReservedPlayerJoined);
            }
            else
            {
                SendConsoleMessage(text: $"[Reserved Slots] Selected player is NULL, no one is kicked!", ConsoleColor.Red);
            }
        }
        return HookResult.Continue;
    }

    [GameEventHandler(HookMode.Pre)]
    public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player != null && player.IsValid)
        {
            if (waitingForSelectTeam.Contains(player.Slot))
                waitingForSelectTeam.Remove(player.Slot);

            if (waitingForKick.ContainsKey(player.Slot))
                waitingForKick.Remove(player.Slot);

            if (reservedPlayers.ContainsKey(player.Slot))
                reservedPlayers.Remove(player.Slot);
        }

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerConnect(EventPlayerConnectFull @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player != null && player.IsValid && player.SteamID.ToString().Length == 17)
        {
            if (Config.adminFlagsAndRoles.Count == 0 && Config.reservedFlagsAndRoles.Count == 0)
                return HookResult.Continue;

            int MaxPlayers = Server.MaxPlayers;
            var playerReservedType = GetPlayersReservedType(player);
            if (playerReservedType == ReservedType.VIP || playerReservedType == ReservedType.Admin)
                SetKickImmunity(player, playerReservedType);

            switch (Config.reservedSlotsMethod)
            {
                case 1:
                    if (GetPlayersCount() > MaxPlayers - Config.reservedSlots)
                    {
                        if (playerReservedType == ReservedType.VIP)
                        {
                            if ((Config.openSlot && GetPlayersCount() >= MaxPlayers) || !Config.openSlot && GetPlayersCount() > MaxPlayers)
                                PerformKickCheckMethod(player);
                        }
                        else if (playerReservedType == ReservedType.None)
                            PerformKick(player, KickReason.ServerIsFull);
                    }
                    break;
                case 2:
                    if (GetPlayersCount() - GetPlayersCountWithReservationFlag() > MaxPlayers - Config.reservedSlots)
                    {
                        if (playerReservedType == ReservedType.VIP)
                        {
                            if ((Config.openSlot && GetPlayersCount() >= MaxPlayers) || !Config.openSlot && GetPlayersCount() > MaxPlayers)
                                PerformKickCheckMethod(player);
                        }
                        else if (playerReservedType == ReservedType.None)
                            PerformKick(player, KickReason.ServerIsFull);
                    }
                    break;

                default:
                    if (GetPlayersCount() >= MaxPlayers)
                    {
                        if (playerReservedType == ReservedType.VIP)
                            PerformKickCheckMethod(player);
                        else if (playerReservedType == ReservedType.None)
                            PerformKick(player, KickReason.ServerIsFull);
                    }
                    break;
            }
        }
        return HookResult.Continue;
    }

    public ReservedType GetPlayersReservedType(CCSPlayerController player)
    {
        if (DiscordUtilities != null)
        {
            bool HasRole(List<ulong> userRoles, List<string> roleIds)
            {
                var reservedRoles = roleIds
                    .Where(x => ulong.TryParse(x, out _))
                    .Select(x => ulong.Parse(x))
                    .ToHashSet();

                return userRoles.Any(role => reservedRoles.Contains(role));
            }

            if (DiscordUtilities.GetLinkedPlayers().TryGetValue(player.SteamID, out var userId))
            {
                var data = DiscordUtilities.GetUserDataByUserID(userId);
                if (data != null && data.RolesIds.Any())
                {
                    var userRoles = data.RolesIds;
                    if (Config.adminFlagsAndRoles.Any())
                    {
                        if (HasRole(userRoles, Config.adminFlagsAndRoles))
                            return ReservedType.Admin;
                    }

                    if (Config.reservedFlagsAndRoles.Any())
                    {
                        if (HasRole(userRoles, Config.reservedFlagsAndRoles))
                            return ReservedType.VIP;
                    }
                }
            }
        }

        var adminData = AdminManager.GetPlayerAdminData(player);
        if (adminData == null)
            return ReservedType.None;

        var playerFlags = adminData.GetAllFlags();
        if (!playerFlags.Any())
            return ReservedType.None;

        if (Config.adminFlagsAndRoles.Any())
        {
            var reservedFlags = Config.adminFlagsAndRoles
            .Where(item => !ulong.TryParse(item, out _))
            .ToHashSet();

            if (playerFlags.Any(flag => reservedFlags.Contains(flag)))
                return ReservedType.Admin;
        }

        if (Config.reservedFlagsAndRoles.Any())
        {
            var reservedFlags = Config.reservedFlagsAndRoles
            .Where(item => !ulong.TryParse(item, out _))
            .ToHashSet();

            if (playerFlags.Any(flag => reservedFlags.Contains(flag)))
                return ReservedType.VIP;
        }

        return ReservedType.None;
    }

    public void SetKickImmunity(CCSPlayerController player, ReservedType type)
    {
        if (reservedPlayers.ContainsKey(player.Slot))
            return;

        bool isImmune = Config.kickImmunity switch
        {
            1 => type == ReservedType.Admin,
            2 => type == ReservedType.VIP,
            _ => type == ReservedType.Admin || type == ReservedType.VIP
        };
        reservedPlayers.Add(player.Slot, isImmune);
    }

    public void PerformKickCheckMethod(CCSPlayerController player)
    {
        switch (Config.kickCheckMethod)
        {
            case 1:
                if (!waitingForSelectTeam.Contains(player.Slot))
                    waitingForSelectTeam.Add(player.Slot);
                break;
            default:
                var kickedPlayer = getPlayerToKick(player);
                if (kickedPlayer != null)
                {
                    PerformKick(kickedPlayer, KickReason.ReservedPlayerJoined);
                }
                else
                {
                    Logger.LogError($"[Reserved Slots] Selected player is NULL, no one is kicked!");
                }
                break;
        }
    }

    public void PerformKick(CCSPlayerController? player, KickReason reason)
    {
        if (player == null || !player.IsValid)
            return;

        var name = player.PlayerName;
        var steamid = player.SteamID.ToString();
        if (Config.kickDelay > 1)
        {
            var slot = player.Slot;
            waitingForKick.Add(slot, reason);
            AddTimer(Config.kickDelay, () =>
            {
                player = Utilities.GetPlayerFromSlot(slot);
                if (player != null && player.IsValid)
                {
                    player.Disconnect((CounterStrikeSharp.API.ValveConstants.Protobuf.NetworkDisconnectionReason)Config.kickReason);
                    //Server.ExecuteCommand($"kickid {player.UserId}");
                    LogMessage(name, steamid, reason);
                }

                if (waitingForKick.ContainsKey(slot))
                    waitingForKick.Remove(slot);

            }, TimerFlags.STOP_ON_MAPCHANGE);
        }
        else
        {
            player.Disconnect((CounterStrikeSharp.API.ValveConstants.Protobuf.NetworkDisconnectionReason)Config.kickReason);
            //Server.ExecuteCommand($"kickid {player.UserId}");
            LogMessage(name, steamid, reason);
        }
    }

    public void LogMessage(string name, string steamid, KickReason reason)
    {
        switch (reason)
        {
            case KickReason.ServerIsFull:
                if (Config.logKickedPlayers)
                    Logger.LogInformation($"Player {name} ({steamid}) was kicked, because the server is full.");

                if (Config.displayKickedPlayers == 1)
                    Server.PrintToChatAll(Localizer["Chat.PlayerWasKicked.ServerIsFull", name]);
                else if (Config.displayKickedPlayers == 2)
                {
                    foreach (var admin in Utilities.GetPlayers().Where(p => AdminManager.PlayerHasPermissions(p, "@css/generic")))
                    {
                        admin.PrintToChat(Localizer["Chat.PlayerWasKicked.ServerIsFull", name]);
                    }
                }
                break;

            case KickReason.ReservedPlayerJoined:
                if (Config.logKickedPlayers)
                    Logger.LogInformation($"Player {name} ({steamid}) was kicked, because player with a reservation slot joined.");

                if (Config.displayKickedPlayers == 1)
                    Server.PrintToChatAll(Localizer["Chat.PlayerWasKicked.ReservedPlayerJoined", name]);
                else if (Config.displayKickedPlayers == 2)
                {
                    foreach (var admin in Utilities.GetPlayers().Where(p => AdminManager.PlayerHasPermissions(p, "@css/generic")))
                    {
                        admin.PrintToChat(Localizer["Chat.PlayerWasKicked.ReservedPlayerJoined", name]);
                    }
                }
                break;
        }
    }

    private CCSPlayerController? getPlayerToKick(CCSPlayerController client)
    {
        var allPlayers = Utilities.GetPlayers();
        var playersList = allPlayers
            .Where(p => !p.IsBot && !p.IsHLTV && p.PlayerPawn.IsValid && p.Connected == PlayerConnectedState.PlayerConnected && p.SteamID.ToString().Length == 17 && p != client && (!reservedPlayers.ContainsKey(p.Slot) || (reservedPlayers.ContainsKey(p.Slot) && reservedPlayers[p.Slot] == false)))
            .Select(player => (player, (int)player.Ping, player.Score, player.Team))
            .ToList();

        if (Config.kickPlayersInSpectate)
        {
            if (playersList.Count(x => x.Team == CsTeam.None || x.Team == CsTeam.Spectator) > 0)
                playersList.RemoveAll(x => x.Team != CsTeam.None && x.Team != CsTeam.Spectator);
        }

        if (!playersList.Any())
            return null;

        CCSPlayerController? player = null;
        switch (Config.kickType)
        {
            case (int)KickType.HighestPing:
                playersList.Sort((x, y) => y.player.Ping.CompareTo(x.player.Ping));
                player = playersList.FirstOrDefault().player;
                break;

            case (int)KickType.HighestScore:
                playersList.Sort((x, y) => y.player.Score.CompareTo(x.player.Score));
                player = playersList.FirstOrDefault().player;
                break;

            case (int)KickType.LowestScore:
                playersList.Sort((x, y) => x.player.Score.CompareTo(y.player.Score));
                player = playersList.FirstOrDefault().player;
                break;

            default:
                playersList = playersList.OrderBy(x => Guid.NewGuid()).ToList();
                player = playersList.FirstOrDefault().player;
                break;
        }
        return player;
    }

    private static int GetPlayersCount()
    {
        return Utilities.GetPlayers().Where(p => !p.IsHLTV && !p.IsBot && p.Connected == PlayerConnectedState.PlayerConnected && p.SteamID.ToString().Length == 17).Count();
    }

    private int GetPlayersCountWithReservationFlag()
    {
        return Utilities.GetPlayers().Where(p => !p.IsHLTV && !p.IsBot && p.Connected == PlayerConnectedState.PlayerConnected && p.SteamID.ToString().Length == 17 && reservedPlayers.ContainsKey(p.Slot)).Count();
    }

    private static void SendConsoleMessage(string text, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ResetColor();
    }
}
