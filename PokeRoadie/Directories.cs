using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;

namespace PokeRoadie
{
    public class Directories
    {
        public string TempDirectory { get; set; } = Path.Combine(Directory.GetCurrentDirectory(), "Temp");
        public string ConfigsDirectory { get; set; } = Path.Combine(Directory.GetCurrentDirectory(), "Configs");
        public string PokestopsDirectory { get; set; } = Path.Combine(Directory.GetCurrentDirectory(), "Temp\\Pokestops");
        public string EncountersDirectory { get; set; } = Path.Combine(Directory.GetCurrentDirectory(), "Temp\\Encounters");
        public string PingDirectory { get; set; } = Path.Combine(Directory.GetCurrentDirectory(), "Temp\\Pings");
        public string GymDirectory { get; set; } = Path.Combine(Directory.GetCurrentDirectory(), "Temp\\Gyms");
        public string EggDirectory { get; set; } = Path.Combine(Directory.GetCurrentDirectory(), "Temp\\Eggs");

        public Directories()
        {
            CheckDirectoriesExist();
        }

        public void Setup(string username)
        {
            //\ / : * ? " < > |
            var filteredName = username
                                .Replace("\\", "_")
                                .Replace("/", "_")
                                .Replace(":", "_")
                                .Replace("*", "_")
                                .Replace("?", "_")
                                .Replace("<", "_")
                                .Replace(">", "_")
                                .Replace("|", "_");

            TempDirectory  = Path.Combine(Directory.GetCurrentDirectory(), "Users", filteredName, "Temp");
            ConfigsDirectory  = Path.Combine(Directory.GetCurrentDirectory(), "Users", filteredName, "Configs");

            EncountersDirectory = Path.Combine(TempDirectory, "Encounters");
            PingDirectory = Path.Combine(TempDirectory, "Pings");
            GymDirectory = Path.Combine(TempDirectory, "Gyms");
            PokestopsDirectory = Path.Combine(TempDirectory, "Pokestops");
            EggDirectory = Path.Combine(TempDirectory, "Eggs");

            CheckDirectoriesExist();
        }

        public void CheckDirectoriesExist()
        {
            if (!Directory.Exists(TempDirectory)) Directory.CreateDirectory(TempDirectory);
            if (!Directory.Exists(ConfigsDirectory)) Directory.CreateDirectory(ConfigsDirectory);
            if (!Directory.Exists(PokestopsDirectory)) Directory.CreateDirectory(PokestopsDirectory);
            if (!Directory.Exists(EncountersDirectory)) Directory.CreateDirectory(EncountersDirectory);
            if (!Directory.Exists(GymDirectory)) Directory.CreateDirectory(GymDirectory);
            if (!Directory.Exists(EggDirectory)) Directory.CreateDirectory(EggDirectory);
        }
    }
}
