using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Core.Attributes.Registration;

namespace MatchZy
{
    public partial class MatchZy
    {
        private CounterStrikeSharp.API.Modules.Timers.Timer? ffwTimer = null;
        private bool ffwActive = false;
        private CsTeam ffwRequestingTeam = CsTeam.None;
        private CsTeam ffwMissingTeam = CsTeam.None;
        
        [ConsoleCommand("css_ffw", "Request forfeit win when opponent team is missing")]
        public void OnFFWCommand(CCSPlayerController? player, CommandInfo? command)
        {
            if (!IsPlayerValid(player)) return;
            if (!isMatchLive) 
            {
                ReplyToUserCommand(player, "FFW can only be used during a live match!");
                return;
            }
            
            // Проверяем, что команда игрока не пустая
            var playerTeam = player!.Team;
            if (playerTeam != CsTeam.Terrorist && playerTeam != CsTeam.CounterTerrorist)
            {
                ReplyToUserCommand(player, "You must be on a team to request FFW!");
                return;
            }
            
            // Подсчитываем игроков в командах
            int ctCount = 0;
            int tCount = 0;
            
            foreach (var p in playerData.Values)
            {
                if (!IsPlayerValid(p)) continue;
                if (p.Team == CsTeam.CounterTerrorist) ctCount++;
                else if (p.Team == CsTeam.Terrorist) tCount++;
            }
            
            // Проверяем условия для FFW
            bool canRequestFFW = false;
            CsTeam missingTeam = CsTeam.None;
            
            if (playerTeam == CsTeam.CounterTerrorist && tCount == 0)
            {
                canRequestFFW = true;
                missingTeam = CsTeam.Terrorist;
            }
            else if (playerTeam == CsTeam.Terrorist && ctCount == 0)
            {
                canRequestFFW = true;
                missingTeam = CsTeam.CounterTerrorist;
            }
            
            if (!canRequestFFW)
            {
                ReplyToUserCommand(player, "Cannot request FFW - opponent team has players on the server!");
                return;
            }
            
            if (ffwActive)
            {
                ReplyToUserCommand(player, "FFW timer is already active!");
                return;
            }
            
            // Запускаем FFW таймер
            StartFFW(playerTeam, missingTeam);
        }
        
        private void StartFFW(CsTeam requestingTeam, CsTeam missingTeam)
        {
            ffwActive = true;
            ffwRequestingTeam = requestingTeam;
            ffwMissingTeam = missingTeam;
            
            string missingTeamName = GetTeamName(missingTeam);
            
            PrintToAllChat($"{chatPrefix} FFW timer started! {missingTeamName} has 3 minutes to return!");
            
            // Основной таймер на 3 минуты
            ffwTimer = AddTimer(180.0f, () => {
                if (ffwActive)
                {
                    EndFFW(true);
                }
            });
            
            // Таймеры для оповещений
            AddTimer(60.0f, () => {
                if (ffwActive)
                {
                    PrintToAllChat($"{chatPrefix} {missingTeamName} has 2 minutes left to return!");
                }
            });
            
            AddTimer(120.0f, () => {
                if (ffwActive)
                {
                    PrintToAllChat($"{chatPrefix} {missingTeamName} has 1 minute left to return!");
                }
            });
            
            AddTimer(150.0f, () => {
                if (ffwActive)
                {
                    PrintToAllChat($"{chatPrefix} {missingTeamName} has 30 seconds left to return!");
                }
            });
        }
        
        private void EndFFW(bool forfeit)
        {
            ffwTimer?.Kill();
            ffwTimer = null;
            ffwActive = false;
            
            if (forfeit)
            {
                string winnerName = GetTeamName(ffwRequestingTeam);
                PrintToAllChat($"{chatPrefix} {GetTeamName(ffwMissingTeam)} failed to return! {winnerName} wins by forfeit!");
                
                // Завершаем матч
                if (ffwRequestingTeam == CsTeam.CounterTerrorist)
                {
                    EndSeries(reverseTeamSides["CT"].teamName, 5, 16, 0);
                }
                else
                {
                    EndSeries(reverseTeamSides["TERRORIST"].teamName, 5, 16, 0);
                }
            }
            else
            {
                PrintToAllChat($"{chatPrefix} FFW cancelled - player has returned!");
            }
            
            ffwRequestingTeam = CsTeam.None;
            ffwMissingTeam = CsTeam.None;
        }
        
        private void CheckFFWStatus()
        {
            if (!ffwActive) return;
            
            // Проверяем, вернулся ли кто-то из отсутствующей команды
            foreach (var p in playerData.Values)
            {
                if (!IsPlayerValid(p)) continue;
                if (p.Team == ffwMissingTeam)
                {
                    EndFFW(false);
                    return;
                }
            }
        }
        
        private string GetTeamName(CsTeam team)
        {
            if (team == CsTeam.CounterTerrorist)
            {
                return reverseTeamSides["CT"].teamName;
            }
            else if (team == CsTeam.Terrorist)
            {
                return reverseTeamSides["TERRORIST"].teamName;
            }
            return "Unknown Team";
        }
    }
}