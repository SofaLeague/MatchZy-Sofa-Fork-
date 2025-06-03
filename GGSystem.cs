using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using System.Collections.Generic;
using System.Linq;

namespace MatchZy
{
    public partial class MatchZy
    {
        private Dictionary<CsTeam, HashSet<int>> ggVotes = new()
        {
            { CsTeam.CounterTerrorist, new HashSet<int>() },
            { CsTeam.Terrorist, new HashSet<int>() }
        };
        
        private CounterStrikeSharp.API.Modules.Timers.Timer? ggResetTimer = null;
        
        [ConsoleCommand("css_gg", "Vote to surrender the match")]
        public void OnGGCommand(CCSPlayerController? player, CommandInfo? command)
        {
            if (!IsPlayerValid(player)) return;
            if (!isMatchLive)
            {
                ReplyToUserCommand(player, "GG can only be used during a live match!");
                return;
            }
            
            var playerTeam = player!.Team;
            if (playerTeam != CsTeam.Terrorist && playerTeam != CsTeam.CounterTerrorist)
            {
                ReplyToUserCommand(player, "You must be on a team to vote for GG!");
                return;
            }
            
            // Получаем текущий счет
            (int t1score, int t2score) = GetTeamsScore();
            int playerTeamScore = 0;
            int opponentTeamScore = 0;
            string playerTeamName = "";
            
            if (playerTeam == CsTeam.CounterTerrorist)
            {
                if (reverseTeamSides["CT"] == matchzyTeam1)
                {
                    playerTeamScore = t1score;
                    opponentTeamScore = t2score;
                }
                else
                {
                    playerTeamScore = t2score;
                    opponentTeamScore = t1score;
                }
                playerTeamName = reverseTeamSides["CT"].teamName;
            }
            else if (playerTeam == CsTeam.Terrorist)
            {
                if (reverseTeamSides["TERRORIST"] == matchzyTeam1)
                {
                    playerTeamScore = t1score;
                    opponentTeamScore = t2score;
                }
                else
                {
                    playerTeamScore = t2score;
                    opponentTeamScore = t1score;
                }
                playerTeamName = reverseTeamSides["TERRORIST"].teamName;
            }
            
            // Проверяем, что команда проигрывает на 6 или более раундов
            int scoreDifference = opponentTeamScore - playerTeamScore;
            if (scoreDifference < 6)
            {
                ReplyToUserCommand(player, $"Your team must be losing by at least 6 rounds to surrender! Current score: {playerTeamScore}-{opponentTeamScore}");
                return;
            }
            
            // Подсчитываем общее количество игроков в команде
            var teamPlayers = playerData.Values
                .Where(p => IsPlayerValid(p) && p.Team == playerTeam)
                .ToList();
            
            // Добавляем голос игрока
            if (!player.UserId.HasValue) return;
            
            if (ggVotes[playerTeam].Contains(player.UserId.Value))
            {
                ReplyToUserCommand(player, "You have already voted for GG!");
                return;
            }
            
            ggVotes[playerTeam].Add(player.UserId.Value);

            int votesNeeded;
            if (matchConfig.MinPlayersToReady == 1)
                votesNeeded = 1;
            else
                votesNeeded = Math.Max(2, matchConfig.MinPlayersToReady - 1);

            int currentVotes = ggVotes[playerTeam].Count;
            
            PrintToAllChat($"{ChatColors.Green}{player.PlayerName}{ChatColors.Default} voted to surrender. {ChatColors.Green}({currentVotes}/{votesNeeded}){ChatColors.Default} votes from {ChatColors.Green}{playerTeamName}{ChatColors.Default} [Score: {playerTeamScore}-{opponentTeamScore}]");
            
            // Проверяем, достаточно ли голосов
            if (currentVotes >= votesNeeded)
            {
                // Команда сдается
                string winnerTeam = playerTeam == CsTeam.CounterTerrorist ? 
                    reverseTeamSides["TERRORIST"].teamName : reverseTeamSides["CT"].teamName;
                
                PrintToAllChat($"{ChatColors.Green}{playerTeamName}{ChatColors.Default} has surrendered! {ChatColors.Green}{winnerTeam}{ChatColors.Default} wins!");
                
                // Завершаем матч с текущим счетом
                if (playerTeam == CsTeam.CounterTerrorist)
                {
                    // CT сдается, победа T
                    EndSeries(reverseTeamSides["TERRORIST"].teamName, 5, opponentTeamScore, playerTeamScore);
                }
                else
                {
                    // T сдается, победа CT
                    EndSeries(reverseTeamSides["CT"].teamName, 5, playerTeamScore, opponentTeamScore);
                }
                
                ResetGGVotes();
            }
            else
            {
                // Устанавливаем таймер для сброса голосов через 60 секунд
                ggResetTimer?.Kill();
                ggResetTimer = AddTimer(60.0f, () => {
                    if (ggVotes[playerTeam].Count > 0)
                    {
                        PrintToAllChat($"GG vote for {ChatColors.Green}{playerTeamName}{ChatColors.Default} has expired!");
                        ggVotes[playerTeam].Clear();
                    }
                });
            }
        }
        
        private void ResetGGVotes()
        {
            ggResetTimer?.Kill();
            ggResetTimer = null;
            ggVotes[CsTeam.CounterTerrorist].Clear();
            ggVotes[CsTeam.Terrorist].Clear();
        }
        
        // Вызывать при завершении матча
        private void OnMatchEnd()
        {
            ResetGGVotes();
        }
    }
}