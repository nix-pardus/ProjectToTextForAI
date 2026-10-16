using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ProjectToTextForAI
{
    public static class FileWalker
    {
        public static List<string> GetAllFiles(string rootPath, string[] excludeFolderPatterns, string[] excludeFilePatterns)
        {
            var result = new List<string>();
            var excludeDirs = excludeFolderPatterns
                .Where(p => !p.Contains("*") && !p.StartsWith("*"))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var excludeExtensions = excludeFolderPatterns
                .Where(p => p.StartsWith("*"))
                .Select(p => p.Substring(1).ToLowerInvariant())
                .ToHashSet();

            // Для файлов будем проверять соответствие любому из паттернов
            // Простая реализация: поддерживаем точное совпадение и шаблоны с '*'
            var filePatterns = excludeFilePatterns ?? Array.Empty<string>();

            WalkDirectory(rootPath, result, excludeDirs, excludeExtensions, filePatterns);
            return result;
        }

        private static void WalkDirectory(string currentDir, List<string> files,
        HashSet<string> excludeDirs, HashSet<string> excludeExtensions,
        string[] filePatterns)
        {
            try
            {
                string dirName = Path.GetFileName(currentDir);
                if (excludeDirs.Contains(dirName)) return;

                foreach (string file in Directory.GetFiles(currentDir))
                {
                    string fileName = Path.GetFileName(file);
                    string ext = Path.GetExtension(file).ToLowerInvariant();

                    // Фильтр по расширению
                    if (excludeExtensions.Contains(ext)) continue;

                    // Фильтр по шаблону имени файла
                    if (MatchesAnyPattern(fileName, filePatterns)) continue;

                    files.Add(file);
                }

                foreach (string dir in Directory.GetDirectories(currentDir))
                {
                    WalkDirectory(dir, files, excludeDirs, excludeExtensions, filePatterns);
                }
            }
            catch (UnauthorizedAccessException) { /* игнорируем */ }
        }

        private static bool MatchesAnyPattern(string fileName, string[] patterns)
        {
            if (patterns == null || patterns.Length == 0) return false;

            foreach (var pattern in patterns)
            {
                if (string.IsNullOrEmpty(pattern)) continue;

                // Точное совпадение (без учёта регистра)
                if (string.Equals(fileName, pattern, StringComparison.OrdinalIgnoreCase))
                    return true;

                // Шаблон с '*' (простой вариант: только '*' в начале или конце)
                if (pattern.Contains("*"))
                {
                    // Например, "*.log" -> совпадение, если заканчивается на ".log"
                    if (pattern.StartsWith("*") && pattern.Length > 1)
                    {
                        string suffix = pattern.Substring(1);
                        if (fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                    else if (pattern.EndsWith("*") && pattern.Length > 1)
                    {
                        string prefix = pattern.Substring(0, pattern.Length - 1);
                        if (fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                    // Более сложные паттерны можно обработать через System.Text.RegularExpressions при необходимости
                }
            }
            return false;
        }
    }
}
