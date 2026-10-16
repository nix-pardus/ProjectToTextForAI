using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

namespace ProjectToTextForAI
{
    public class AiContextConfig
    {
        // Папки для сканирования относительно корня решения
        public List<string> IncludeFolders { get; set; } = new List<string> { "." };
        // Имена папок, которые пропускаются
        public List<string> ExcludeFolderNames { get; set; } = new List<string> { "bin", "obj", ".git", ".vs", "packages" };
        // Расширения файлов для исключения
        public List<string> ExcludeExtensions { get; set; } = new List<string> { ".dll", ".exe", ".pdb", ".suo", ".user" };
        // Исключения файлов по имени или шаблону
        public List<string> ExcludeFilePatterns { get; set; } = new List<string> { ".gitattributes", ".gitignore", "*.log", "Thumbs.db" };

        public static AiContextConfig Load(string generatorInputFilePath)
        {
            string directory = Path.GetDirectoryName(generatorInputFilePath);
            string configPath = Path.Combine(directory, ".aiconfig.json");
            if (File.Exists(configPath))
            {
                string json = File.ReadAllText(configPath);
                return JsonConvert.DeserializeObject<AiContextConfig>(json) ?? new AiContextConfig();
            }
            // Поиск на уровень выше (корень решения)
            string solutionConfigPath = Path.GetFullPath(Path.Combine(directory, "..", ".aiconfig.json"));
            if (File.Exists(solutionConfigPath))
            {
                string json = File.ReadAllText(solutionConfigPath);
                return JsonConvert.DeserializeObject<AiContextConfig>(json) ?? new AiContextConfig();
            }
            return new AiContextConfig();
        }

        public string[] GetExcludePatterns()
        {
            var patterns = new List<string>();
            patterns.AddRange(ExcludeFolderNames);
            patterns.AddRange(ExcludeExtensions.ConvertAll(ext => "*" + ext));
            return patterns.ToArray();
        }

        public string[] GetExcludeFilePatterns()
        {
            return ExcludeFilePatterns.ToArray();
        }
    }
}
