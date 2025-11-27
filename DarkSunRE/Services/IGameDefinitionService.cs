using DarkSunRE.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace DarkSunRE.Services
{
    public interface IGameDefinitionService
    {
        ObservableCollection<GameDefinition> GetSupportedGames();
        GameDefinition GetGameDefinition(ProjectType type);
        bool ValidateGamePath(string gamePath, ProjectType gameType);
        string[] GetExpectedFiles(ProjectType gameType);
        string[] GetGffFilePatterns(ProjectType gameType);
        string DetectGameVersion(string gamePath, ProjectType gameType);
        ImageSource GetGameIcon(ProjectType gameType);
        string GetGameExecutable(ProjectType gameType);
    }
}
