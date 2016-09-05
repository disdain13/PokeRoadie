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
        public string GymDirectory { get; set; } = Path.Combine(Directory.GetCurrentDirectory(), "Temp\\Gyms");
        public string EggDirectory { get; set; } = Path.Combine(Directory.GetCurrentDirectory(), "Temp\\Eggs");

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
            EggDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Users", filteredName,  "Eggs");

        }
    }
}
