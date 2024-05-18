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
    [JsonPropertyName("Flag for reserved slots")] public string reservedFlag { get; set; } = "@css/reservation";
    [JsonPropertyName("Admin flag for reserved slots")] public string adminFlag { get; set; } = "@css/ban";
    [JsonPropertyName("Kick immunity type")] public int kickImmunity { get; set; } = 0;
    [JsonPropertyName("Reserved slots")] public int reservedSlots { get; set; } = 1;
    [JsonPropertyName("Reserved slots method")] public int reservedSlotsMethod { get; set; } = 0;
    [JsonPropertyName("Leave one slot open")] public bool openSlot { get; set; } = false;
    [JsonPropertyName("Kick Delay")] public int kickDelay { get; set; } = 5;
    [JsonPropertyName("Kick Check Method")] public int kickCheckMethod { get; set; } = 0;
    [JsonPropertyName("Kick type")] public int kickType { get; set; } = 0;
    [JsonPropertyName("Kick players in spectate")] public bool kickPlayersInSpectate { get; set; } = true;
    [JsonPropertyName("Log kicked players")] public bool logKickedPlayers { get; set; } = true;
    [JsonPropertyName("Display kicked players message")] public int displayKickedPlayers { get; set; } = 2;
}

public class ReservedSlots : BasePlugin, IPluginConfig<ReservedSlotsConfig>
{
    public override string ModuleName => "Reserved Slots";
    public override string ModuleAuthor => "Nocky (SourceFactory.eu)";
    public override string ModuleVersion => "1.0.8";

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

    public List<int> waitingForSelectTeam = new();
    public Dictionary<int, KickReason> waitingForKick = new();
    public ReservedSlotsConfig Config { get; set; } = new();
    public void OnConfigParsed(ReservedSlotsConfig config)
    {
        Config = config;
        if (string.IsNullOrEmpty(Config.reservedFlag))
            SendConsoleMessage("[Reserved Slots] Flag for reserved slots cannot be empty!", ConsoleColor.Red);
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
        }

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerConnect(EventPlayerConnectFull @event, GameEventInfo info)
    {
        var player = @event.Userid;
        int MaxPlayers = Server.MaxPlayers;
        if (player != null && player.IsValid && player.Connected == PlayerConnectedState.PlayerConnected && player.SteamID.ToString().Length == 17)
        {
            switch (Config.reservedSlotsMethod)
            {
                case 1:
                    if (GetPlayersCount() > MaxPlayers - Config.reservedSlots)
                    {
                        if (!string.IsNullOrEmpty(Config.adminFlag) && AdminManager.PlayerHasPermissions(player, Config.adminFlag))
                        {
                            return HookResult.Continue;
                        }

                        if (AdminManager.PlayerHasPermissions(player, Config.reservedFlag))
                        {
                            if ((Config.openSlot && GetPlayersCount() >= MaxPlayers) || !Config.openSlot && GetPlayersCount() > MaxPlayers)
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
                                            SendConsoleMessage(text: $"[Reserved Slots] Selected player is NULL, no one is kicked! (Method = 1)", ConsoleColor.Red);
                                        }
                                        break;
                                }
                            }
                        }
                        else
                        {
                            PerformKick(player, KickReason.ServerIsFull);
                        }
                    }
                    break;
                case 2:
                    if (GetPlayersCount() - GetPlayersCountWithReservationFlag() > MaxPlayers - Config.reservedSlots)
                    {
                        if (!string.IsNullOrEmpty(Config.adminFlag) && AdminManager.PlayerHasPermissions(player, Config.adminFlag))
                        {
                            return HookResult.Continue;
                        }
                        if (AdminManager.PlayerHasPermissions(player, Config.reservedFlag))
                        {
                            if ((Config.openSlot && GetPlayersCount() >= MaxPlayers) || !Config.openSlot && GetPlayersCount() > MaxPlayers)
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
                                            SendConsoleMessage(text: $"[Reserved Slots] Selected player is NULL, no one is kicked! (Method = 2)", ConsoleColor.Red);
                                        }
                                        break;
                                }
                            }
                        }
                        else
                        {
                            PerformKick(player, KickReason.ServerIsFull);
                        }
                    }
                    break;

                default:
                    if (GetPlayersCount() >= MaxPlayers)
                    {
                        if (!string.IsNullOrEmpty(Config.adminFlag) && AdminManager.PlayerHasPermissions(player, Config.adminFlag))
                        {
                            return HookResult.Continue;
                        }
                        if (AdminManager.PlayerHasPermissions(player, Config.reservedFlag))
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
                                        SendConsoleMessage(text: $"[Reserved Slots] Selected player is NULL, no one is kicked! (Method = 0)", ConsoleColor.Red);
                                    }
                                    break;
                            }
                        }
                        else
                        {
                            PerformKick(player, KickReason.ServerIsFull);
                        }
                    }
                    break;
            }
        }

        return HookResult.Continue;
    }

    public void PerformKick(CCSPlayerController? player, KickReason reason)
    {
        if (player == null || !player.IsValid)
            return;

        var slot = player.Slot;
        var name = player.PlayerName;
        var steamid = player.SteamID.ToString();
        if (Config.kickDelay > 1)
        {
            waitingForKick.Add(slot, reason);
            AddTimer(Config.kickDelay, () =>
            {
                player = Utilities.GetPlayerFromSlot(slot);
                if (player != null && player.IsValid)
                {
                    Server.ExecuteCommand($"kickid {player.UserId}");
                    LogMessage(name, steamid, reason);
                }

                if (waitingForKick.ContainsKey(slot))
                    waitingForKick.Remove(slot);

            }, TimerFlags.STOP_ON_MAPCHANGE);
        }
        else
        {
            Server.ExecuteCommand($"kickid {player.UserId}");
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

    private CCSPlayerController getPlayerToKick(CCSPlayerController client)
    {
        var allPlayers = Utilities.GetPlayers();
        var playersList = allPlayers
            .Where(p => !p.IsBot && !p.IsHLTV && p.PlayerPawn.IsValid && p.Connected == PlayerConnectedState.PlayerConnected && p.SteamID.ToString().Length == 17 && p != client)
            .Select(player => (player, (int)player.Ping, player.Score, player.Team))
            .ToList();

        switch (Config.kickImmunity)
        {
            case 1:
                playersList.RemoveAll(p => AdminManager.PlayerHasPermissions(p.player, Config.reservedFlag));
                break;
            case 2:
                if (!string.IsNullOrEmpty(Config.adminFlag))
                    playersList.RemoveAll(p => AdminManager.PlayerHasPermissions(p.player, Config.adminFlag));
                break;
            default:
                if (!string.IsNullOrEmpty(Config.adminFlag))
                    playersList.RemoveAll(p => AdminManager.PlayerHasPermissions(p.player, Config.reservedFlag) || AdminManager.PlayerHasPermissions(p.Item1, Config.adminFlag));
                else
                    playersList.RemoveAll(p => AdminManager.PlayerHasPermissions(p.player, Config.reservedFlag));
                break;
        }

        if (Config.kickPlayersInSpectate)
        {
            if (playersList.Count(x => x.Team == CsTeam.None || x.Team == CsTeam.Spectator) > 0)
                playersList.RemoveAll(x => x.Team != CsTeam.None || x.Team != CsTeam.Spectator);
        }

        CCSPlayerController player = null!;
        switch (Config.kickType)
        {
            case (int)KickType.HighestPing:
                if (playersList.Count() > 0)
                {
                    playersList.Sort((x, y) => y.player.Ping.CompareTo(x.player.Ping));
                    player = playersList.FirstOrDefault().player;
                }
                break;

            case (int)KickType.HighestScore:
                if (playersList.Count() > 0)
                {
                    playersList.Sort((x, y) => y.player.Score.CompareTo(x.player.Score));
                    player = playersList.FirstOrDefault().player;
                }
                break;

            case (int)KickType.LowestScore:
                if (playersList.Count() > 0)
                {
                    playersList.Sort((x, y) => x.player.Score.CompareTo(y.player.Score));
                    player = playersList.FirstOrDefault().player;
                }
                break;

            default:
                if (playersList.Count() > 0)
                {
                    playersList = playersList.OrderBy(x => Guid.NewGuid()).ToList();
                    player = playersList.FirstOrDefault().player;
                }
                break;
        }
        return player;
    }
    private static int GetPlayersCount()
    {
        return Utilities.GetPlayers().Where(p => !p.IsHLTV && !p.IsBot && p.PlayerPawn.IsValid && p.Connected == PlayerConnectedState.PlayerConnected && p.SteamID.ToString().Length == 17).Count();
    }
    private int GetPlayersCountWithReservationFlag()
    {
        return Utilities.GetPlayers().Where(p => !p.IsHLTV && !p.IsBot && p.PlayerPawn.IsValid && p.Connected == PlayerConnectedState.PlayerConnected && p.SteamID.ToString().Length == 17 && AdminManager.PlayerHasPermissions(p, Config.reservedFlag)).Count();
    }
    private static void SendConsoleMessage(string text, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ResetColor();
    }
}
