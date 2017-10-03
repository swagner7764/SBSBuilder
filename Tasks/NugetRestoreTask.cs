using System;
using System.IO;
using log4net;

namespace SBSBuilder.Tasks
{
    public class NugetRestoreTask : AbstractNugetTask
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof (NugetRestoreTask));
        public DirectoryInfo ProjectDirectory { get; set; }

        public void Execute()
        {
            if (ProjectDirectory == null)
                throw new NullReferenceException("Project directory must be set.");

            if (!ProjectDirectory.Exists)
                throw new ArgumentException(string.Format("Project directory {0} does not exist.",
                    ProjectDirectory.FullName));

            if (NugetExe == null)
                throw new NullReferenceException("Nuget executable must be set.");

            if (!NugetExe.Exists)
                throw new ArgumentException(string.Format("Nuget executable {0} does not exist.", NugetExe.FullName));

            // ReSharper disable once PossibleNullReferenceException
            var packagesDir = ProjectDirectory.Parent.FullName + "\\packages";
            var test = Directory.GetFiles(ProjectDirectory.FullName, "*.sln", SearchOption.TopDirectoryOnly);
            if (test.Length > 0)
                packagesDir = ProjectDirectory.FullName + "\\packages";

            Log.Info(string.Format("Restoring NuGet packages in directory {0}.", packagesDir));

            var paths = ProjectDirectory.Parent.GetFiles("packages.config", SearchOption.AllDirectories);
            foreach (var path in paths)
            {
                var args = string.Format("restore -PackagesDirectory \"{0}\" \"{1}\"", packagesDir, path.FullName);
                ExecuteNugetTask(args);
            }

            Log.Info(string.Format("Restore of packages in directory {0} complete.", packagesDir));
        }
    }
}