using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Design.Serialization;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using IServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

namespace ProjectToTextForAI
{
    [Guid("374ED931-131F-437C-856C-50676320CE88")]
    [ComVisible(true)]
    public class AiContextGenerator : IVsSingleFileGenerator, IObjectWithSite
    {
        private object _site;

        public int DefaultExtension(out string pbstrDefaultExtension)
        {
            pbstrDefaultExtension = ".ai.txt";
            return VSConstants.S_OK;
        }

        public int Generate(string wszInputFilePath, string bstrInputFileContents, string wszDefaultNamespace, IntPtr[] rgbOutputFileContents, out uint pcbOutput, IVsGeneratorProgress pGenerateProgress)
        {
            try
            {
                // Логирование для отладки
                string logPath = @"C:\Temp\ai_gen_log.txt";
                File.AppendAllText(logPath, $"{DateTime.Now}: Generate started for {wszInputFilePath}\n");

                // Загрузка конфигурации
                var config = AiContextConfig.Load(wszInputFilePath);
                File.AppendAllText(logPath, $"Config loaded. IncludeFolders: {string.Join(", ", config.IncludeFolders)}\n");

                // Определение корня решения (используем DTE, если доступно, иначе папка файла)
                string solutionRoot = GetSolutionRoot() ?? Path.GetDirectoryName(wszInputFilePath);
                File.AppendAllText(logPath, $"Solution root: {solutionRoot}\n");

                // Сбор файлов
                var allFiles = new List<string>();
                foreach (string relativeFolder in config.IncludeFolders)
                {
                    string fullFolderPath = Path.GetFullPath(Path.Combine(solutionRoot, relativeFolder));
                    if (Directory.Exists(fullFolderPath))
                    {
                        var folderFiles = FileWalker.GetAllFiles(
                            fullFolderPath,
                            config.GetExcludePatterns(),      // папки и расширения
                            config.GetExcludeFilePatterns()   // имена файлов
                        );
                        allFiles.AddRange(folderFiles);
                        File.AppendAllText(logPath, $"Folder {fullFolderPath}: found {folderFiles.Count} files\n");
                    }
                    else
                    {
                        File.AppendAllText(logPath, $"WARNING: Folder not found: {fullFolderPath}\n");
                        pGenerateProgress?.GeneratorError(0, 0, $"Folder not found: {fullFolderPath}", 0, 0);
                    }
                }

                // Удаление дубликатов (если папки пересекаются)
                allFiles = allFiles.Distinct().ToList();
                File.AppendAllText(logPath, $"Total files after distinct: {allFiles.Count}\n");

                string output = FormatFilesAsContext(allFiles, solutionRoot);

                // Возврат полного содержимого для перезаписи файла
                byte[] bytes = Encoding.UTF8.GetBytes(output);
                rgbOutputFileContents[0] = Marshal.AllocCoTaskMem(bytes.Length);
                Marshal.Copy(bytes, 0, rgbOutputFileContents[0], bytes.Length);
                pcbOutput = (uint)bytes.Length;

                File.AppendAllText(logPath, $"Generate completed successfully. Output size: {bytes.Length} bytes\n");
                pGenerateProgress?.Progress(100, 100);
                return VSConstants.S_OK;
            }
            catch (Exception ex)
            {
                string logPath = @"C:\Temp\ai_gen_log.txt";
                File.AppendAllText(logPath, $"{DateTime.Now}: EXCEPTION: {ex}\n");
                pGenerateProgress?.GeneratorError(0, 0, ex.ToString(), 0, 0);
                pcbOutput = 0;
                rgbOutputFileContents[0] = IntPtr.Zero;
                return VSConstants.E_FAIL;
            }
        }

        // Получение корня решения через DTE
        private string GetSolutionRoot()
        {
            File.AppendAllText(@"C:\Temp\ai_gen_log.txt", "GetSolutionRoot called\n");

            var oleSp = _site as IServiceProvider;
            if (oleSp == null)
            {
                File.AppendAllText(@"C:\Temp\ai_gen_log.txt", "oleSp is null\n");
                return null;
            }

            // Получаем DTE через QueryService
            Guid guidSvc = typeof(DTE).GUID;
            Guid guidIUnknown = VSConstants.IID_IUnknown;
            IntPtr ppvObject;
            int hr = oleSp.QueryService(ref guidSvc, ref guidIUnknown, out ppvObject);
            if (hr != 0 || ppvObject == IntPtr.Zero)
            {
                File.AppendAllText(@"C:\Temp\ai_gen_log.txt", $"QueryService failed, hr={hr}\n");
                return null;
            }

            object dteObj = Marshal.GetObjectForIUnknown(ppvObject);
            Marshal.Release(ppvObject);
            var dte = dteObj as DTE2;
            if (dte?.Solution?.FullName == null)
            {
                File.AppendAllText(@"C:\Temp\ai_gen_log.txt", "dte or Solution.FullName is null\n");
                return null;
            }

            File.AppendAllText(@"C:\Temp\ai_gen_log.txt", $"Solution FullName: {dte.Solution.FullName}\n");
            return Path.GetDirectoryName(dte.Solution.FullName);
        }

        // Методы форматирования (без изменений, кроме добавления информации о проекте)
        private static string FormatFilesAsContext(List<string> files, string rootPath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== SOLUTION CONTEXT FOR AI ===");
            sb.AppendLine($"Generated on {DateTime.Now}");
            sb.AppendLine($"Total files: {files.Count}");
            sb.AppendLine();

            sb.AppendLine("Folder structure:");
            sb.AppendLine(GetFolderTree(files, rootPath));
            sb.AppendLine();

            // Группируем файлы по проектам (по относительному пути, содержащему имя проекта)
            var grouped = files.GroupBy(f => GetProjectNameFromPath(f, rootPath));
            foreach (var group in grouped)
            {
                sb.AppendLine($"\n## Project: {group.Key}");
                foreach (string file in group.OrderBy(f => f))
                {
                    string relativePath = GetRelativePath(rootPath, file);
                    sb.AppendLine($"\n--- File: {relativePath} ---");
                    sb.AppendLine(new string('=', 60));
                    try
                    {
                        string content = File.ReadAllText(file, Encoding.UTF8);
                        sb.AppendLine(content);
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine($"[File reading error: {ex.Message}]");
                    }
                }
            }
            return sb.ToString();
        }

        private static string GetProjectNameFromPath(string fullPath, string solutionRoot)
        {
            // Извлекаем имя первого каталога после корня решения
            if (string.IsNullOrEmpty(solutionRoot)) return "Unknown";
            var rel = GetRelativePath(solutionRoot, fullPath);
            var parts = rel.Split(Path.DirectorySeparatorChar);
            return parts.Length > 0 ? parts[0] : "Unknown";
        }

        private static string GetRelativePath(string relativeTo, string path)
        {
            if (!relativeTo.EndsWith(Path.DirectorySeparatorChar.ToString()))
                relativeTo += Path.DirectorySeparatorChar;

            var uri1 = new Uri(relativeTo);
            var uri2 = new Uri(path);
            return Uri.UnescapeDataString(uri1.MakeRelativeUri(uri2).ToString())
                      .Replace('/', Path.DirectorySeparatorChar);
        }

        private static string GetFolderTree(IEnumerable<string> files, string rootPath)
        {
            var tree = new StringBuilder();
            var dirs = files.Select(f => Path.GetDirectoryName(f))
                .Distinct()
                .OrderBy(d => d);

            foreach (string dir in dirs)
            {
                string relDir = GetRelativePath(rootPath, dir);
                int depth = relDir.Split(Path.DirectorySeparatorChar).Length;
                string indent = new string(' ', depth * 2);
                tree.Append($"{indent}📁 {Path.GetFileName(dir)}");
            }

            return tree.ToString();
        }

        public void SetSite(object pUnkSite)
        {
            _site = pUnkSite;
        }

        public void GetSite(ref Guid riid, out IntPtr ppvSite)
        {
            ppvSite = IntPtr.Zero;
            throw new NotImplementedException();
        }

        public static string FormatFilesAsContextStatic(List<string> files, string rootPath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== SOLUTION CONTEXT FOR AI ===");
            sb.AppendLine($"Generated on {DateTime.Now}");
            sb.AppendLine($"Total files: {files.Count}");
            sb.AppendLine();

            sb.AppendLine("Folder structure:");
            sb.AppendLine(GetFolderTree(files, rootPath));
            sb.AppendLine();

            // Группируем файлы по проектам (по относительному пути, содержащему имя проекта)
            var grouped = files.GroupBy(f => GetProjectNameFromPath(f, rootPath));
            foreach (var group in grouped)
            {
                sb.AppendLine($"\n## Project: {group.Key}");
                foreach (string file in group.OrderBy(f => f))
                {
                    string relativePath = GetRelativePath(rootPath, file);
                    sb.AppendLine($"\n--- File: {relativePath} ---");
                    sb.AppendLine(new string('=', 60));
                    try
                    {
                        string content = File.ReadAllText(file, Encoding.UTF8);
                        sb.AppendLine(content);
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine($"[File reading error: {ex.Message}]");
                    }
                }
            }
            return sb.ToString();
        }

    }
}
