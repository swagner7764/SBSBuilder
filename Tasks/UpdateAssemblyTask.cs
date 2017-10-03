using System;
using System.Configuration;
using System.IO;
using log4net;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SBSBuilder.Build;
using AssemblyInfo = MSBuild.ExtensionPack.Framework.AssemblyInfo;

namespace SBSBuilder.Tasks
{
    public class UpdateAssemblyTask : ITask
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof (UpdateAssemblyTask));
        public DirectoryInfo ProjectDirectory { get; set; }
        public string Version { get; set; }

        public void Execute()
        {
            if (ProjectDirectory == null)
                throw new NullReferenceException("Project directory must be set.");

            if (!ProjectDirectory.Exists)
                throw new ArgumentException(string.Format("Project directory {0} does not exist.",
                    ProjectDirectory.FullName));

            var paths = Directory.GetFiles(ProjectDirectory.FullName, "AssemblyInfo.*", SearchOption.AllDirectories);
            if (paths.Length == 0)
            {
                Log.Warn(string.Format("Cannot locate assembly file in project directory {0}. ",
                    ProjectDirectory.FullName));
                return;
            }

            if (paths.Length > 1)
            {
                Log.Warn(string.Format("Found multiple assembly files found in project directory {0}. ",
                    ProjectDirectory.FullName));
                foreach (var p in paths)
                {
                    Log.Warn(p);
                }
                return;
            }

            if (Version == null)
                throw new NullReferenceException("Version must be set.");

            var taskItems = new ITaskItem[paths.Length];
            for (var i = 0; i < paths.Length; i++)
            {
                taskItems[i] = new TaskItem(paths[i]);
                Log.Info(string.Format("Updating assembly file: {0}.", paths[i]));
            }

            var year = DateTime.Today.Year;
            var assemblyInfo = new AssemblyInfo();
            assemblyInfo.AssemblyCompany = ConfigurationManager.AppSettings["Company"];
            assemblyInfo.AssemblyCopyright = string.Format(ConfigurationManager.AppSettings["Copyright"], year,
                assemblyInfo.AssemblyCompany);
            assemblyInfo.AssemblyVersion = Version;
            assemblyInfo.AssemblyFileVersion = Version;
            assemblyInfo.BuildEngine = new FakeBuildEngine();
            assemblyInfo.AssemblyInfoFiles = taskItems;
            assemblyInfo.Execute();

            Log.Info("Assembly update complete.");
        }
    }
}