using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ProjectToTextForAI
{
    internal sealed class GenerateAiContextCommand
    {
        public const int CommandId = 0x0100;
        public static readonly Guid CommandSet = new Guid("a1b2c3d4-e5f6-4789-0a1b-2c3d4e5f6a7b");

        private readonly AsyncPackage package;

        private GenerateAiContextCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(Execute, menuCommandID);
            commandService.AddCommand(menuItem);
        }

        public static async System.Threading.Tasks.Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            new GenerateAiContextCommand(package, commandService);
        }

        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var dte = (DTE2)Package.GetGlobalService(typeof(DTE));
            if (dte?.Solution == null || string.IsNullOrEmpty(dte.Solution.FullName))
            {
                MessageBox.Show("Нет открытого решения.", "AI Context Generator", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string solutionRoot = Path.GetDirectoryName(dte.Solution.FullName);
            var config = AiContextConfig.Load(solutionRoot);

            // Диалог сохранения
            using (var dialog = new SaveFileDialog())
            {
                dialog.Title = "Сохранить AI контекст как...";
                dialog.Filter = "Text Files (*.ai.txt)|*.ai.txt|All Files (*.*)|*.*";
                dialog.DefaultExt = ".ai.txt";
                dialog.FileName = "SolutionContext.ai.txt";
                dialog.InitialDirectory = solutionRoot;

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    string outputPath = dialog.FileName;
                    GenerateContextFile(outputPath, solutionRoot, config);
                    MessageBox.Show($"Файл сохранён:\n{outputPath}", "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void GenerateContextFile(string outputPath, string solutionRoot, AiContextConfig config)
        {
            var allFiles = new System.Collections.Generic.List<string>();
            foreach (string relativeFolder in config.IncludeFolders)
            {
                string fullFolderPath = Path.GetFullPath(Path.Combine(solutionRoot, relativeFolder));
                if (Directory.Exists(fullFolderPath))
                {
                    var folderFiles = FileWalker.GetAllFiles(fullFolderPath, config.GetExcludePatterns(), config.GetExcludeFilePatterns());
                    allFiles.AddRange(folderFiles);
                }
            }
            allFiles = allFiles.Distinct().ToList();

            string output = AiContextGenerator.FormatFilesAsContextStatic(allFiles, solutionRoot);
            File.WriteAllText(outputPath, output, Encoding.UTF8);
        }
    }
}