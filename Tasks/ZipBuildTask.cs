using System;
using System.IO;
using log4net;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using MSBuild.ExtensionPack.Compression;
using SBSBuilder.Build;

namespace SBSBuilder.Tasks
{
    public class ZipBuildTask : ITask
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof (ZipBuildTask));
        public DirectoryInfo BuildDirectory { get; set; }
        public string ProjectName { get; set; }
        public string AssemblyName { get; set; }
        public string Target { get; set; }
        public string Version { get; set; }
        public DirectoryInfo OutputDirectory { get; set; }
        public FileInfo ZipFile { get; private set; }

        public void Execute()
        {
            if (BuildDirectory == null)
                throw new NullReferenceException("Build directory must be set.");

            BuildDirectory.Refresh();
            if (!BuildDirectory.Exists)
                throw new ArgumentException(string.Format("Build directory {0} does not exist.",
                    BuildDirectory.FullName));

            if (Target == null || Target.Trim().Length == 0)
                throw new NullReferenceException("Target must be set.");

            if (Version == null)
                throw new NullReferenceException("Version must be set.");

            if (OutputDirectory == null)
                throw new NullReferenceException("Output directory must be set.");

            OutputDirectory.Refresh();
            if (!OutputDirectory.Exists)
                OutputDirectory.Create();

            var zipDir = BuildDirectory.FullName + "\\_PublishedWebsites\\" + ProjectName;
            if (!Directory.Exists(zipDir))
                zipDir = BuildDirectory.FullName;

            var paths = Directory.GetFiles(zipDir, "*.*", SearchOption.AllDirectories);
            if (paths.Length == 0)
            {
                Log.Warn(string.Format("No files found to zip in directory {0}. ", zipDir));
                return;
            }

            var taskItems = new ITaskItem[paths.Length];
            for (var i = 0; i < paths.Length; i++)
            {
                taskItems[i] = new TaskItem(paths[i]);
                Log.Info(string.Format("Compressing file: {0}.", paths[i]));
            }

            var zipFileName = string.Format("{0}\\{1}-{2}-{3}.zip", OutputDirectory.FullName, AssemblyName, Target,
                Version);
            var task = new Zip();
            task.TaskAction = "Create";
            task.RemoveRoot = new TaskItem(zipDir);
            task.CompressFiles = taskItems;
            task.ZipFileName = new TaskItem(zipFileName);
            task.BuildEngine = new FakeBuildEngine();
            task.Execute();

            ZipFile = new FileInfo(zipFileName);
            Log.Info(string.Format("Compression of directory {0} to zip file {1} complete.", zipDir, zipFileName));
        }
    }
}