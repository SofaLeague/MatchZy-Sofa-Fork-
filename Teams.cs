using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using Newtonsoft.Json.Linq;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API;

namespace MatchZy
{

    public class Team 
    {
        [JsonPropertyName("id")]
        public string id = "";

        [JsonPropertyName("teamname")]
        public required string teamName;

        [JsonPropertyName("teamflag")]
        public string teamFlag = "";

        [JsonPropertyName("teamtag")]
        public string teamTag = "";

        [JsonPropertyName("teamplayers")]
        public JToken? teamPlayers;

        [JsonIgnore, Newtonsoft.Json.JsonIgnore]
        public HashSet<CCSPlayerController> coach = [];

        [JsonPropertyName("seriesscore")]
        public int seriesScore = 0;
    }

    public partial class MatchZy
    {
        [ConsoleCommand("css_coach", "Sets coach for the requested team")]
        public void OnCoachCommand(CCSPlayerController? player, CommandInfo command)
        {
            HandleCoachCommand(player, command.ArgString);
        }

        [ConsoleCommand("css_uncoach", "Sets coach for the requested team")]
        public void OnUnCoachCommand(CCSPlayerController? player, CommandInfo? command)
        {
            if (player == null || !player.PlayerPawn.IsValid) return;
            if (isPractice)
            {
                ReplyToUserCommand(player, "Uncoach command can only be used in match mode!");
                return;
            }

            if (matchzyTeam1.coach.Contains(player))
            {
                player.Clan = "";
                matchzyTeam1.coach.Remove(player);
                SetPlayerVisible(player);
            }
            else if (matchzyTeam2.coach.Contains(player))
            {
                player.Clan = "";
                matchzyTeam2.coach.Remove(player);
                SetPlayerVisible(player);
            }
            else
            {
                ReplyToUserCommand(player, "You are not coaching any team!");
                return;
            }

            if (player.InGameMoneyServices != null) player.InGameMoneyServices.Account = 0;

            ReplyToUserCommand(player, "You are now not coaching any team!");
        }

        [ConsoleCommand("matchzy_addplayer", "Adds player to the provided team")]
        [ConsoleCommand("get5_addplayer", "Adds player to the provided team")]
        public void OnAddPlayerCommand(CCSPlayerController? player, CommandInfo? command)
        {
            if (player != null || command == null) return;
            if (!isMatchSetup)
            {
                command.ReplyToCommand("No match is setup!");
                return;
            }
            if (IsHalfTimePhase())
            {
                command.ReplyToCommand("Cannot add players during halftime. Please wait until the next round starts.");
                return;
            }
            if (command.ArgCount < 3)
            {
                command.ReplyToCommand("Usage: matchzy_addplayer <steam64> <team> \"<name>\"");
                return;
            }

            string playerSteamId = command.ArgByIndex(1);
            string playerTeam = command.ArgByIndex(2);
            string playerName = command.ArgByIndex(3);
            bool success;
            if (playerTeam == "team1")
            {
                success = AddPlayerToTeam(playerSteamId, playerName, matchzyTeam1.teamPlayers);
            }
            else if (playerTeam == "team2")
            {
                success = AddPlayerToTeam(playerSteamId, playerName, matchzyTeam2.teamPlayers);
            }
            else if (playerTeam == "spec")
            {
                success = AddPlayerToTeam(playerSteamId, playerName, matchConfig.Spectators);
            }
            else
            {
                command.ReplyToCommand("Unknown team: must be one of team1, team2, spec");
                return;
            }
            if (!success)
            {
                command.ReplyToCommand($"Failed to add player {playerName} to {playerTeam}. They may already be on a team or you provided an invalid Steam ID.");
                return;
            }
            command.ReplyToCommand($"Player {playerName} added to {playerTeam} successfully!");
        }

        [ConsoleCommand("matchzy_removeplayer", "Removes the player from all the teams")]
        [ConsoleCommand("get5_removeplayer", "Removes the player from all the teams")]
        [CommandHelper(minArgs: 1, usage: "<steam64>")]
        public void OnRemovePlayerCommand(CCSPlayerController? player, CommandInfo? command)
        {
            if (player != null || command == null) return;
            if (!isMatchSetup)
            {
                command.ReplyToCommand("No match is setup!");
                return;
            }
            if (IsHalfTimePhase())
            {
                command.ReplyToCommand("Cannot remove players during halftime. Please wait until the next round starts.");
                return;
            }

            string arg = command.GetArg(1);

            if (!ulong.TryParse(arg, out ulong steamId))
            {
                command.ReplyToCommand($"Invalid Steam64");
            }

            bool success = RemovePlayerFromTeam(steamId.ToString());
            if (success)
            {
                command.ReplyToCommand($"Successfully removed player {steamId}");
                CCSPlayerController? removedPlayer = Utilities.GetPlayerFromSteamId(steamId);
                if (IsPlayerValid(removedPlayer))
                {
                    Log($"Kicking player {removedPlayer!.PlayerName} - Not a player in this game (removed).");
                    PrintToAllChat($"Kicking player {removedPlayer!.PlayerName} - Not a player in this game.");
                    KickPlayer(removedPlayer);
                }
            }
            else
            {
                command.ReplyToCommand($"Player {steamId} not found in any team or the Steam ID was invalid.");
            }
        }

        public bool AddPlayerToTeam(string steamId, string name, JToken? team)
        {
            if (matchzyTeam1.teamPlayers != null && matchzyTeam1.teamPlayers[steamId] != null) return false;
            if (matchzyTeam2.teamPlayers != null && matchzyTeam2.teamPlayers[steamId] != null) return false;
            if (matchConfig.Spectators != null && matchConfig.Spectators[steamId] != null) return false;

            if (team is JObject jObjectTeam)
            {
                jObjectTeam.Add(steamId, name);
                LoadClientNames();
                return true;
            }
            else if (team is JArray jArrayTeam)
            {
                jArrayTeam.Add(name);
                LoadClientNames();
                return true;
            }
            return false;
        }

        public bool RemovePlayerFromTeam(string steamId)
        {
            bool removed = false;
            List<JToken?> teams = [matchzyTeam1.teamPlayers, matchzyTeam2.teamPlayers, matchConfig.Spectators];

            foreach (var team in teams)
            {
                if (team is null) continue;

                if (team is JObject jObjectTeam)
                {
                    if (jObjectTeam.ContainsKey(steamId))
                    {
                        jObjectTeam.Remove(steamId);
                        removed = true;
                        Log($"[RemovePlayerFromTeam] Removed player {steamId} from team (JObject)");
                    }
                }
                else if (team is JArray jArrayTeam)
                {
                    // Для JArray нужно найти и удалить элемент
                    var itemToRemove = jArrayTeam.FirstOrDefault(item =>
                        item.Type == JTokenType.String && item.ToString() == steamId);

                    if (itemToRemove != null)
                    {
                        jArrayTeam.Remove(itemToRemove);
                        removed = true;
                        Log($"[RemovePlayerFromTeam] Removed player {steamId} from team (JArray)");
                    }
                }
            }

            // Очищаем кэшированные данные игрока
            if (removed)
            {
                // Обновляем список имен клиентов
                LoadClientNames();

                // Очищаем все связанные данные игрока
                CleanupPlayerData(steamId);
            }

            return removed;
        }
        private void CleanupPlayerData(string steamId)
        {
            try
            {
                // Находим игрока по SteamID
                CCSPlayerController? playerToRemove = null;
                foreach (var kvp in playerData)
                {
                    if (kvp.Value.SteamID.ToString() == steamId)
                    {
                        playerToRemove = kvp.Value;
                        break;
                    }
                }

                if (playerToRemove != null && playerToRemove.UserId.HasValue)
                {
                    int userId = playerToRemove.UserId.Value;

                    // Удаляем из всех словарей
                    playerData.Remove(userId);
                    playerReadyStatus.Remove(userId);
                    noFlashList.Remove(userId);
                    lastGrenadesData.Remove(userId);
                    nadeSpecificLastGrenadeData.Remove(userId);

                    // Удаляем из списков тренеров
                    matchzyTeam1.coach.Remove(playerToRemove);
                    matchzyTeam2.coach.Remove(playerToRemove);

                    Log($"[CleanupPlayerData] Cleaned up all data for player {steamId} (UserId: {userId})");
                }
            }
            catch (Exception)
            {
            }
        }

        private Dictionary<string, DateTime> temporaryBlacklist = new();

        private void AddToTemporaryBlacklist(string steamId, int seconds)
        {
            temporaryBlacklist[steamId] = DateTime.Now.AddSeconds(seconds);
            Log($"[AddToTemporaryBlacklist] Added {steamId} to temporary blacklist for {seconds} seconds");
        }

        private bool IsInTemporaryBlacklist(string steamId)
        {
            if (temporaryBlacklist.TryGetValue(steamId, out DateTime expiryTime))
            {
                if (DateTime.Now < expiryTime)
                {
                    return true;
                }
                else
                {
                    temporaryBlacklist.Remove(steamId);
                }
            }
            return false;
        }

        // Метод для периодической очистки устаревших записей (можно вызывать в OnMapStart или по таймеру)
        private void CleanupTemporaryBlacklist()
        {
            var expiredEntries = temporaryBlacklist
                .Where(kvp => DateTime.Now >= kvp.Value)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var steamId in expiredEntries)
            {
                temporaryBlacklist.Remove(steamId);
                Log($"[CleanupTemporaryBlacklist] Removed expired entry for {steamId}");
            }
        }
    }
}
