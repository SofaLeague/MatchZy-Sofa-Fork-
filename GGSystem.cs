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
            
            // Подсчитываем общее количество игроков в команде
            var teamPlayers = playerData.Values
                .Where(p => IsPlayerValid(p) && p.Team == playerTeam)
                .ToList();
            
            if (teamPlayers.Count != 5)
            {
                ReplyToUserCommand(player, $"GG vote requires exactly 5 players in the team! Current: {teamPlayers.Count}");
                return;
            }
            
            // Добавляем голос игрока
            if (!player.UserId.HasValue) return;
            
            if (ggVotes[playerTeam].Contains(player.UserId.Value))
            {
                ReplyToUserCommand(player, "You have already voted for GG!");
                return;
            }
            
            ggVotes[playerTeam].Add(player.UserId.Value);
            
            int votesNeeded = 4;
            int currentVotes = ggVotes[playerTeam].Count;
            
            string teamName = playerTeam == CsTeam.CounterTerrorist ? 
                reverseTeamSides["CT"].teamName : reverseTeamSides["TERRORIST"].teamName;
            
            PrintToAllChat($"{chatPrefix} {player.PlayerName} voted to surrender. ({currentVotes}/{votesNeeded} votes from {teamName})");
            
            // Проверяем, достаточно ли голосов
            if (currentVotes >= votesNeeded)
            {
                // Команда сдается
                string winnerTeam = playerTeam == CsTeam.CounterTerrorist ? 
                    reverseTeamSides["TERRORIST"].teamName : reverseTeamSides["CT"].teamName;
                
                PrintToAllChat($"{chatPrefix} {teamName} has surrendered! {winnerTeam} wins!");
                
                // Получаем текущий счет
                (int t1score, int t2score) = GetTeamsScore();
                
                // Завершаем матч
                if (playerTeam == CsTeam.CounterTerrorist)
                {
                    EndSeries(winnerTeam, 5, t2score, 16);
                }
                else
                {
                    EndSeries(winnerTeam, 5, 16, t1score);
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
                        PrintToAllChat($"{chatPrefix} GG vote for {teamName} has expired!");
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