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
        private CounterStrikeSharp.API.Modules.Timers.Timer? ffwCheckTimer = null;
        private bool ffwActive = false;
        private CsTeam ffwRequestingTeam = CsTeam.None;
        private CsTeam ffwMissingTeam = CsTeam.None;
        
        // Автоматическая проверка отсутствующих команд
        public void CheckForMissingTeams()
        {
            if (!isMatchLive || ffwActive) return;
            
            // Подсчитываем игроков в командах
            int ctCount = 0;
            int tCount = 0;
            
            foreach (var p in playerData.Values)
            {
                if (!IsPlayerValid(p)) continue;
                if (p.Team == CsTeam.CounterTerrorist) ctCount++;
                else if (p.Team == CsTeam.Terrorist) tCount++;
            }
            
            // Проверяем, есть ли команда без игроков
            if (ctCount > 0 && tCount == 0)
            {
                StartFFW(CsTeam.CounterTerrorist, CsTeam.Terrorist);
            }
            else if (tCount > 0 && ctCount == 0)
            {
                StartFFW(CsTeam.Terrorist, CsTeam.CounterTerrorist);
            }
        }
        
        private void StartFFW(CsTeam requestingTeam, CsTeam missingTeam)
        {
            ffwActive = true;
            ffwRequestingTeam = requestingTeam;
            ffwMissingTeam = missingTeam;
            
            string missingTeamName = GetTeamName(missingTeam);
            
            PrintToAllChat($"FFW timer started! {ChatColors.Green}{missingTeamName}{ChatColors.Default} has {ChatColors.Green}4{ChatColors.Default} minutes to return!");
            
            // Основной таймер на 4 минуты
            ffwTimer = AddTimer(240.0f, () => {
                if (ffwActive)
                {
                    EndFFW(true);
                }
            });
            
            // Таймеры для оповещений
            AddTimer(60.0f, () => {
                if (ffwActive)
                {
                    PrintToAllChat($"{ChatColors.Green}{missingTeamName}{ChatColors.Default} has {ChatColors.Green}3{ChatColors.Default} minutes left to return!");
                }
            });
            
            AddTimer(120.0f, () => {
                if (ffwActive)
                {
                    PrintToAllChat($"{ChatColors.Green}{missingTeamName}{ChatColors.Default} has {ChatColors.Green}2{ChatColors.Default} minutes left to return!");
                }
            });
            
            AddTimer(180.0f, () => {
                if (ffwActive)
                {
                    PrintToAllChat($"{ChatColors.Green}{missingTeamName}{ChatColors.Default} has {ChatColors.Green}1{ChatColors.Default} minute left to return!");
                }
            });
            
            AddTimer(210.0f, () => {
                if (ffwActive)
                {
                    PrintToAllChat($"{ChatColors.Green}{missingTeamName}{ChatColors.Default} has {ChatColors.Green}30{ChatColors.Default} seconds left to return!");
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
                PrintToAllChat($"{GetTeamName(ffwMissingTeam)} failed to return! {ChatColors.Green}{winnerName}{ChatColors.Default} wins by forfeit!");
                
                // Останавливаем мониторинг перед завершением матча
                StopFFWMonitoring();
                
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
                PrintToAllChat($"{ChatColors.Green}{GetTeamName(ffwMissingTeam)}{ChatColors.Default} has returned! FFW cancelled.");
            }
            
            ffwRequestingTeam = CsTeam.None;
            ffwMissingTeam = CsTeam.None;
        }
        
        public void CheckFFWStatus()
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
        
        // Запуск периодической проверки FFW
        public void StartFFWMonitoring()
        {
            if (ffwCheckTimer != null) return;
            
            ffwCheckTimer = AddTimer(5.0f, () => {
                if (isMatchLive && !ffwActive)
                {
                    CheckForMissingTeams();
                }
            }, CounterStrikeSharp.API.Modules.Timers.TimerFlags.REPEAT);
        }
        
        // Остановка периодической проверки FFW
        public void StopFFWMonitoring()
        {
            ffwCheckTimer?.Kill();
            ffwCheckTimer = null;
        }
    }
}