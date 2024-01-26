using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Admin;
using System.Text.Json.Serialization;


namespace ReservedSlots;

public class ReservedSlotsConfig : BasePluginConfig
{
    [JsonPropertyName("Flag for reserved slots")] public string reservedFlag { get; set; } = "@css/reservation";
    [JsonPropertyName("Reserved slots")] public int reservedSlots { get; set; } = 1;
    [JsonPropertyName("Reserved slots method")] public int reservedSlotsMethod { get; set; } = 0;
    [JsonPropertyName("Kick type")] public int kickType { get; set; } = 0;
    [JsonPropertyName("Kick players in spectate")] public bool kickPlayersInSpectate { get; set; } = true;
    [JsonPropertyName("Admin kick immunity")] public string kickImmunity { get; set; } = "@css/generic";
}

public partial class ReservedSlots : BasePlugin, IPluginConfig<ReservedSlotsConfig>
{
    public override string ModuleName => "Reserved Slots";
    public override string ModuleAuthor => "SourceFactory.eu";
    public override string ModuleVersion => "1.0.0";

    public enum KickType
    {
        Random,
        HighestPing,
        //HighestTime,
    }
    public ReservedSlotsConfig Config { get; set; } = null!;
    public void OnConfigParsed(ReservedSlotsConfig config) { Config = config; }

    [GameEventHandler(HookMode.Pre)]
    public HookResult OnPlayerConnect(EventPlayerConnectFull @event, GameEventInfo info)
    {
        var player = @event.Userid;

        int MaxPlayers = NativeAPI.GetCommandParamValue("-maxplayers", DataType.DATA_TYPE_INT, 64);
        if (!player.IsHLTV && player != null && player.IsValid && player.Connected == PlayerConnectedState.PlayerConnected)
        {
            switch (Config.reservedSlotsMethod)
            {
                case 1:
                    if (GetPlayersCount() >= MaxPlayers - Config.reservedSlots)
                    {
                        if (AdminManager.PlayerHasPermissions(player, Config.reservedFlag))
                        {
                            if (GetPlayersCount() >= MaxPlayers)
                            {
                                var kickedPlayer = getPlayerToKick(player);
                                if (kickedPlayer != null)
                                {
                                    SendConsoleMessage(text: $"[Reserved Slots] Player {kickedPlayer.PlayerName} is kicked because VIP player join! (Method = 1)", ConsoleColor.Red);
                                    Server.ExecuteCommand($"kickid {kickedPlayer.UserId}");
                                }
                            }
                        }
                        else
                        {
                            SendConsoleMessage($"[Reserved Slots] Player {player.PlayerName} is kicked because server is full! (Method = 1)", ConsoleColor.Red);
                            Server.ExecuteCommand($"kickid {player.UserId}");
                        }
                    }
                    break;
                case 2:
                    if (GetPlayersCount() - GetPlayersCountWithReservationFlag() >= MaxPlayers - Config.reservedSlots)
                    {
                        if (AdminManager.PlayerHasPermissions(player, Config.reservedFlag))
                        {
                            if (GetPlayersCount() >= MaxPlayers)
                            {
                                var kickedPlayer = getPlayerToKick(player);
                                if (kickedPlayer != null)
                                {
                                    SendConsoleMessage(text: $"[Reserved Slots] Player {kickedPlayer.PlayerName} is kicked because VIP player join! (Method = 2)", ConsoleColor.Red);
                                    Server.ExecuteCommand($"kickid {kickedPlayer.UserId}");
                                }
                            }
                        }
                        else
                        {
                            SendConsoleMessage($"[Reserved Slots] Player {player.PlayerName} is kicked because server is full! (Method = 2)", ConsoleColor.Red);
                            Server.ExecuteCommand($"kickid {player.UserId}");
                        }
                    }
                    break;
                case 3:
                    if (GetPlayersCount() >= MaxPlayers)
                    {
                        if (!AdminManager.PlayerHasPermissions(player, Config.reservedFlag))
                        {
                            SendConsoleMessage($"[Reserved Slots] Player {player.PlayerName} is kicked because server is full! (Method = 3)", ConsoleColor.Red);
                            Server.ExecuteCommand($"kickid {player.UserId}");
                        }
                    }
                    break;

                default:
                    if (GetPlayersCount() >= MaxPlayers)
                    {
                        if (AdminManager.PlayerHasPermissions(player, Config.reservedFlag))
                        {
                            var kickedPlayer = getPlayerToKick(player);
                            if (kickedPlayer != null)
                            {
                                SendConsoleMessage(text: $"[Reserved Slots] Player {kickedPlayer.PlayerName} is kicked because VIP player join! (Method = 0)", ConsoleColor.Red);
                                Server.ExecuteCommand($"kickid {kickedPlayer.UserId}");
                            }
                        }
                        else
                        {
                            SendConsoleMessage($"[Reserved Slots] Player {player.PlayerName} is kicked because server is full! (Method = 0)", ConsoleColor.Red);
                            Server.ExecuteCommand($"kickid {player.UserId}");
                        }
                    }
                    break;
            }
        }

        return HookResult.Continue;
    }
    private CCSPlayerController getPlayerToKick(CCSPlayerController client)
    {
        List<CCSPlayerController>? playersList = Utilities.GetPlayers()
            .Where(p => p.IsValid && !p.IsHLTV && p.Connected == PlayerConnectedState.PlayerConnected && p != client && !AdminManager.PlayerHasPermissions(p, Config.kickImmunity) && !AdminManager.PlayerHasPermissions(p, Config.reservedFlag))
            .ToList();

        if (Config.kickPlayersInSpectate)
        {
            if (Utilities.GetPlayers().Where(p => p.IsValid && !p.IsHLTV && p.Connected == PlayerConnectedState.PlayerConnected && (p.TeamNum != (byte)CsTeam.None || p.TeamNum != (byte)CsTeam.Spectator)).Count() > 0)
                playersList.RemoveAll(p => p.TeamNum != (byte)CsTeam.None || p.TeamNum != (byte)CsTeam.Spectator);
        }

        switch (Config.kickType)
        {
            case (int)KickType.HighestPing:
                if (playersList.Count() > 0)
                {
                    CCSPlayerController? playerWithHighestPing = null;
                    int highestPing = 0;
                    foreach (var player in playersList)
                    {
                        var playerPing = (int)player.Ping;

                        if (playerPing > highestPing)
                        {
                            highestPing = playerPing;
                            playerWithHighestPing = player;
                        }
                    }
                    return playerWithHighestPing!;
                }
                return null!;

                //case (int)KickType.HighestTime:
                //    break;
                //default:
                //    break;
        }
        if (playersList.Count() > 0)
        {
            Random random = new Random();
            return GetRandomPlayer(playersList, random);
        }
        return null!;
    }

    private static T GetRandomPlayer<T>(List<T> list, Random random)
    {
        int randomIndex = random.Next(list.Count);
        return list[randomIndex];
    }
    private static int GetPlayersCount()
    {
        return Utilities.GetPlayers().Where(p => p.IsValid && !p.IsHLTV && p.Connected == PlayerConnectedState.PlayerConnected).Count();
    }
    private int GetPlayersCountWithReservationFlag()
    {
        return Utilities.GetPlayers().Where(p => p.IsValid && !p.IsHLTV && p.Connected == PlayerConnectedState.PlayerConnected && AdminManager.PlayerHasPermissions(p, Config.reservedFlag)).Count();
    }
    private static void SendConsoleMessage(string text, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ResetColor();
    }
}
