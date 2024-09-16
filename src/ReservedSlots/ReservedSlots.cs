using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Modules.Timers;
using Microsoft.Extensions.Logging;

namespace ReservedSlots;

public class ReservedSlotsConfig : BasePluginConfig
{
    [JsonPropertyName("Reserved Flags")] public List<string> reservedFlags { get; set; } = new() { "@css/reservation", "@css/vip" };
    [JsonPropertyName("Admin Reserved Flags")] public List<string> adminFlags { get; set; } = new() { "@css/ban", "@css/admin" };
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
    public override string ModuleName => "Reserved Slots";
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
    public void OnConfigParsed(ReservedSlotsConfig config)
    {
        Config = config;
        if (!Config.reservedFlags.Any())
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
            if (Config.adminFlags.Count == 0 && Config.adminFlags.Count == 0)
                return HookResult.Continue;

            int MaxPlayers = Server.MaxPlayers;
            switch (Config.reservedSlotsMethod)
            {
                case 1:
                    if (GetPlayersCount() > MaxPlayers - Config.reservedSlots)
                    {
                        var playerReservedType = GetPlayersReservedType(player);
                        if (playerReservedType == ReservedType.VIP)
                        {
                            if ((Config.openSlot && GetPlayersCount() >= MaxPlayers) || !Config.openSlot && GetPlayersCount() > MaxPlayers)
                                PerformKickCheckMethod(player);
                            SetKickImmunity(player, playerReservedType);
                        }
                        else if (playerReservedType == ReservedType.None)
                            PerformKick(player, KickReason.ServerIsFull);
                        else
                            SetKickImmunity(player, playerReservedType);
                    }
                    break;
                case 2:
                    if (GetPlayersCount() - GetPlayersCountWithReservationFlag() > MaxPlayers - Config.reservedSlots)
                    {
                        var playerReservedType = GetPlayersReservedType(player);
                        if (playerReservedType == ReservedType.VIP)
                        {
                            if ((Config.openSlot && GetPlayersCount() >= MaxPlayers) || !Config.openSlot && GetPlayersCount() > MaxPlayers)
                                PerformKickCheckMethod(player);
                            SetKickImmunity(player, playerReservedType);
                        }
                        else if (playerReservedType == ReservedType.None)
                            PerformKick(player, KickReason.ServerIsFull);
                        else
                            SetKickImmunity(player, playerReservedType);
                    }
                    break;

                default:
                    if (GetPlayersCount() >= MaxPlayers)
                    {
                        var playerReservedType = GetPlayersReservedType(player);
                        if (playerReservedType == ReservedType.VIP)
                        {
                            PerformKickCheckMethod(player);
                            SetKickImmunity(player, playerReservedType);
                        }
                        else if (playerReservedType == ReservedType.None)
                            PerformKick(player, KickReason.ServerIsFull);
                        else
                            SetKickImmunity(player, playerReservedType);
                    }
                    break;
            }
        }
        return HookResult.Continue;
    }

    public ReservedType GetPlayersReservedType(CCSPlayerController player)
    {
        var adminData = AdminManager.GetPlayerAdminData(player);
        if (adminData == null)
            return ReservedType.None;

        var playerFlags = adminData.GetAllFlags();
        if (!playerFlags.Any())
            return ReservedType.None;

        if (Config.adminFlags.Any())
        {
            var reservedFlags = Config.adminFlags
            .Where(item => !ulong.TryParse(item, out _))
            .ToHashSet();

            if (playerFlags.Any(flag => reservedFlags.Contains(flag)))
                return ReservedType.Admin;
        }

        if (Config.reservedFlags.Any())
        {
            var reservedFlags = Config.reservedFlags
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
                    SendConsoleMessage(text: $"[Reserved Slots] Selected player is NULL, no one is kicked!", ConsoleColor.Red);
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
                playersList.RemoveAll(x => x.Team != CsTeam.None || x.Team != CsTeam.Spectator);
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
