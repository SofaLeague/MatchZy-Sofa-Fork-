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
                string loserName = GetTeamName(ffwMissingTeam);

                PrintToAllChat($"{loserName} failed to return! {ChatColors.Green}{winnerName}{ChatColors.Default} wins by forfeit!");

                // Останавливаем мониторинг перед завершением матча
                StopFFWMonitoring();

                // Получаем текущий счет на карте
                (int currentT1score, int currentT2score) = GetTeamsScore();

                // Определяем, какая команда является team1, а какая team2
                int t1score, t2score;

                if (ffwRequestingTeam == CsTeam.CounterTerrorist)
                {
                    // Если CT команда остались (победили)
                    if (reverseTeamSides["CT"] == matchzyTeam1)
                    {
                        // CT = team1, значит team1 победила
                        t1score = Math.Max(currentT1score, 16); // Минимум 16 для победы по форфейту
                        t2score = currentT2score; // Текущий счет проигравших
                        matchzyTeam1.seriesScore++;
                    }
                    else
                    {
                        // CT = team2, значит team2 победила
                        t1score = currentT1score; // Текущий счет проигравших
                        t2score = Math.Max(currentT2score, 16); // Минимум 16 для победы по форфейту
                        matchzyTeam2.seriesScore++;
                    }
                }
                else
                {
                    // Если T команда остались (победили)
                    if (reverseTeamSides["TERRORIST"] == matchzyTeam1)
                    {
                        // T = team1, значит team1 победила
                        t1score = Math.Max(currentT1score, 16); // Минимум 16 для победы по форфейту
                        t2score = currentT2score; // Текущий счет проигравших
                        matchzyTeam1.seriesScore++;
                    }
                    else
                    {
                        // T = team2, значит team2 победила
                        t1score = currentT1score; // Текущий счет проигравших
                        t2score = Math.Max(currentT2score, 16); // Минимум 16 для победы по форфейту
                        matchzyTeam2.seriesScore++;
                    }
                }

                // Завершаем матч с правильным winner name и счетом
                EndSeries(winnerName, 5, t1score, t2score);
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
                if (reverseTeamSides["CT"] == matchzyTeam1)
                {
                    return matchzyTeam1.teamName;
                }
                else
                {
                    return matchzyTeam2.teamName;
                }
            }
            else if (team == CsTeam.Terrorist)
            {
                if (reverseTeamSides["TERRORIST"] == matchzyTeam1)
                {
                    return matchzyTeam1.teamName;
                }
                else
                {
                    return matchzyTeam2.teamName;
                }
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