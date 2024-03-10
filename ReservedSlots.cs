using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using System.Text.Json.Serialization;

namespace ReservedSlots;

public class ReservedSlotsConfig : BasePluginConfig
{
    [JsonPropertyName("Flag for reserved slots")] public string reservedFlag { get; set; } = "@css/reservation";
    [JsonPropertyName("Admin flag for reserved slots")] public string adminFlag { get; set; } = "@css/ban";
    [JsonPropertyName("Kick immunity type")] public int kickImmunity { get; set; } = 0;
    [JsonPropertyName("Reserved slots")] public int reservedSlots { get; set; } = 1;
    [JsonPropertyName("Reserved slots method")] public int reservedSlotsMethod { get; set; } = 0;
    [JsonPropertyName("Leave one slot open")] public bool openSlot { get; set; } = false;
    [JsonPropertyName("Kick type")] public int kickType { get; set; } = 0;
    [JsonPropertyName("Kick players in spectate")] public bool kickPlayersInSpectate { get; set; } = true;
}

public class ReservedSlots : BasePlugin, IPluginConfig<ReservedSlotsConfig>
{
    public override string ModuleName => "Reserved Slots";
    public override string ModuleAuthor => "SourceFactory.eu";
    public override string ModuleVersion => "1.0.5";

    public enum KickType
    {
        Random,
        HighestPing,
        HighestScore,
        LowestScore,
        //HighestTime,
    }
    public ReservedSlotsConfig Config { get; set; } = null!;
    public void OnConfigParsed(ReservedSlotsConfig config) { Config = config; }

    [GameEventHandler(HookMode.Pre)]
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
                        if (AdminManager.PlayerHasPermissions(player, Config.adminFlag))
                        {
                            return HookResult.Continue;
                        }

                        if (AdminManager.PlayerHasPermissions(player, Config.reservedFlag))
                        {
                            if ((Config.openSlot && GetPlayersCount() >= MaxPlayers) || !Config.openSlot && GetPlayersCount() > MaxPlayers)
                            {
                                var kickedPlayer = getPlayerToKick(player);
                                if (kickedPlayer != null)
                                {
                                    SendConsoleMessage(text: $"[Reserved Slots] Player {kickedPlayer.PlayerName} is kicked because VIP player join! (Method = 1)", ConsoleColor.Red);
                                    Server.ExecuteCommand($"kickid {kickedPlayer.UserId}");
                                }
                                else
                                {
                                    SendConsoleMessage(text: $"[Reserved Slots] Selected player is NULL, no one is kicked! (Method = 1)", ConsoleColor.Red);
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
                    if (GetPlayersCount() - GetPlayersCountWithReservationFlag() > MaxPlayers - Config.reservedSlots)
                    {
                        if (AdminManager.PlayerHasPermissions(player, Config.adminFlag))
                        {
                            return HookResult.Continue;
                        }
                        if (AdminManager.PlayerHasPermissions(player, Config.reservedFlag))
                        {
                            if ((Config.openSlot && GetPlayersCount() >= MaxPlayers) || !Config.openSlot && GetPlayersCount() > MaxPlayers)
                            {
                                var kickedPlayer = getPlayerToKick(player);
                                if (kickedPlayer != null)
                                {
                                    SendConsoleMessage(text: $"[Reserved Slots] Player {kickedPlayer.PlayerName} is kicked because VIP player join! (Method = 2)", ConsoleColor.Red);
                                    Server.ExecuteCommand($"kickid {kickedPlayer.UserId}");
                                }
                                else
                                {
                                    SendConsoleMessage(text: $"[Reserved Slots] Selected player is NULL, no one is kicked! (Method = 2)", ConsoleColor.Red);
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
                /*case 3:
                    if (GetPlayersCount() > MaxPlayers)
                    {
                        if (!AdminManager.PlayerHasPermissions(player, Config.reservedFlag))
                        {
                            SendConsoleMessage($"[Reserved Slots] Player {player.PlayerName} is kicked because server is full! (Method = 3)", ConsoleColor.Red);
                            Server.ExecuteCommand($"kickid {player.UserId}");
                        }
                    }
                    break;*/

                default:
                    if (GetPlayersCount() >= MaxPlayers)
                    {
                        if (AdminManager.PlayerHasPermissions(player, Config.adminFlag))
                        {
                            return HookResult.Continue;
                        }
                        if (AdminManager.PlayerHasPermissions(player, Config.reservedFlag))
                        {
                            var kickedPlayer = getPlayerToKick(player);
                            if (kickedPlayer != null)
                            {
                                SendConsoleMessage(text: $"[Reserved Slots] Player {kickedPlayer.PlayerName} is kicked because VIP player join! (Method = 0)", ConsoleColor.Red);
                                Server.ExecuteCommand($"kickid {kickedPlayer.UserId}");
                            }
                            else
                            {
                                SendConsoleMessage(text: $"[Reserved Slots] Selected player is NULL, no one is kicked! (Method = 0)", ConsoleColor.Red);
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
        var allPlayers = Utilities.GetPlayers();
        var playersList = allPlayers
            .Where(p => p.IsValid && !p.IsHLTV && p.Connected == PlayerConnectedState.PlayerConnected && p.SteamID.ToString().Length == 17 && p != client)
            .Select(player => (player, (int)player.Ping, player.Score))
            .ToList();

        switch (Config.kickImmunity)
        {
            case 1:
                playersList.RemoveAll(p => AdminManager.PlayerHasPermissions(p.Item1, Config.reservedFlag));
                break;
            case 2:
                playersList.RemoveAll(p => AdminManager.PlayerHasPermissions(p.Item1, Config.adminFlag));
                break;
            default:
                playersList.RemoveAll(p => AdminManager.PlayerHasPermissions(p.Item1, Config.reservedFlag) || AdminManager.PlayerHasPermissions(p.Item1, Config.adminFlag));
                break;
        }

        if (Config.kickPlayersInSpectate)
        {
            if (Utilities.GetPlayers().Where(p => p.IsValid && !p.IsHLTV && p != client && p.Connected == PlayerConnectedState.PlayerConnected && p.SteamID.ToString().Length == 17 && (p.TeamNum == (byte)CsTeam.None || p.TeamNum == (byte)CsTeam.Spectator)).Count() > 0)
                playersList.RemoveAll(p => p.Item1.TeamNum != (byte)CsTeam.None || p.Item1.TeamNum != (byte)CsTeam.Spectator);
        }

        CCSPlayerController player = null!;
        switch (Config.kickType)
        {
            case (int)KickType.HighestPing:
                if (playersList.Count() > 0)
                {
                    playersList.Sort((x, y) => y.Item2.CompareTo(x.Item2));
                    player = playersList.FirstOrDefault().Item1;
                }
                break;

            case (int)KickType.HighestScore:
                if (playersList.Count() > 0)
                {
                    playersList.Sort((x, y) => y.Item3.CompareTo(x.Item3));
                    player = playersList.FirstOrDefault().Item1;
                }
                break;

            case (int)KickType.LowestScore:
                if (playersList.Count() > 0)
                {
                    playersList.Sort((x, y) => x.Item3.CompareTo(y.Item3));
                    player = playersList.FirstOrDefault().Item1;
                }
                break;

            default:
                playersList = playersList.OrderBy(x => Guid.NewGuid()).ToList();
                player = playersList.FirstOrDefault().Item1;
                break;
        }
        return player;
    }

    private static int GetPlayersCount()
    {
        return Utilities.GetPlayers().Where(p => p.IsValid && !p.IsHLTV && !p.IsBot && p.Connected == PlayerConnectedState.PlayerConnected && p.SteamID.ToString().Length == 17).Count();
    }
    private int GetPlayersCountWithReservationFlag()
    {
        return Utilities.GetPlayers().Where(p => p.IsValid && !p.IsHLTV && !p.IsBot && p.Connected == PlayerConnectedState.PlayerConnected && p.SteamID.ToString().Length == 17 && AdminManager.PlayerHasPermissions(p, Config.reservedFlag)).Count();
    }
    private static void SendConsoleMessage(string text, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ResetColor();
    }
}
