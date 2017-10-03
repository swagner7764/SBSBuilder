using System;
using System.IO;
using log4net;
using Microsoft.Web.Publishing.Tasks;
using SBSBuilder.Build;

namespace SBSBuilder.Tasks
{
    public class TransformConfigTask : ITask
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof (TransformConfigTask));
        public DirectoryInfo ProjectDirectory { get; set; }
        public DirectoryInfo BuildDirectory { get; set; }
        public string ProjectName { get; set; }
        public InstallType? Type { get; set; }
        public FileInfo Executable { get; set; }
        public string Target { get; set; }

        public void Execute()
        {
            if (ProjectDirectory == null)
                throw new NullReferenceException("Project directory must be set.");

            if (!ProjectDirectory.Exists)
                throw new ArgumentException(string.Format("Project directory {0} does not exist.",
                    ProjectDirectory.FullName));

            if (BuildDirectory == null)
                throw new NullReferenceException("Build directory must be set.");

            BuildDirectory.Refresh();
            if (!BuildDirectory.Exists)
                throw new ArgumentException(string.Format("Build directory {0} does not exist.",
                    BuildDirectory.FullName));

            if (Executable == null)
                throw new NullReferenceException("Assembly executable must be set.");

            if (!Executable.Exists)
                throw new ArgumentException(string.Format("Assembly executable {0} does not exist.", Executable.FullName));

            if (Target == null || Target.Trim().Length == 0)
                throw new NullReferenceException("Target must be set.");

            if (Type == null)
                throw new Exception("Installation must be set.");

            if (Type == InstallType.WebService)
                TransformWebConfig();
            else
                TransformAppConfig();

            if (Type == InstallType.ScheduledTask || Type == InstallType.SelfHostedService)
                TransformTaskConfig();

            TransformLog4NetConfig();

            Log.Info("Config file transormation complete.");
        }

        private void TransformWebConfig()
        {
            var destDir = BuildDirectory.FullName + "\\_PublishedWebsites\\" + ProjectName;
            var transformFile = ProjectDirectory.FullName + string.Format("\\Web.{0}.Config", Target);
            var sourceFile = ProjectDirectory.FullName + "\\Web.Config";

            // Delete all web config files in the destination directory, we will create the one we want.
            foreach (var f in Directory.EnumerateFiles(destDir, "Web*.Config"))
            {
                File.Delete(f);
            }

            var destFile = destDir + "\\Web.Config";
            DoTransform(sourceFile, transformFile, destFile);
        }

        private void TransformAppConfig()
        {
            var destFile = Executable.FullName + ".config";
            string[] transconfig = System.IO.Directory.GetFiles(ProjectDirectory.FullName, string.Format("App.{0}.Config", Target), SearchOption.AllDirectories);
            string[] origconfig = System.IO.Directory.GetFiles(ProjectDirectory.FullName, "App.Config", SearchOption.AllDirectories);
            if (transconfig.Length > 0 && origconfig.Length > 0)
            {
                var transformFile = transconfig[0];
                var sourceFile = origconfig[0];
                DoTransform(sourceFile, transformFile, destFile);
            }
        }

        private void TransformTaskConfig()
        {
            var destFile = Executable.DirectoryName + "\\~TaskDefs.xml";
            string[] transxml = System.IO.Directory.GetFiles(ProjectDirectory.FullName, string.Format("TaskDefs.{0}.xml", Target), SearchOption.AllDirectories);
            string[] origxml = System.IO.Directory.GetFiles(ProjectDirectory.FullName, "TaskDefs.xml", SearchOption.AllDirectories);
            if (origxml.Length > 0)
            {
                if (transxml.Length < 1)
                {
                    var sourceFile = origxml[0];
                    File.Copy(sourceFile, destFile);
                }
                else if (transxml.Length > 0)
                {
                    var transformFile = transxml[0];
                    var sourceFile = origxml[0];
                    DoTransform(sourceFile, transformFile, destFile);
                }
            }
        }

        private void TransformLog4NetConfig()
        {
            var destDir = Executable.DirectoryName;
            if (Type == InstallType.WebService)
                destDir = BuildDirectory.FullName + "\\_PublishedWebsites\\" + ProjectName;
            var transformFile = ProjectDirectory.FullName + string.Format("\\log4net_config\\log4net.{0}.config", Target);
            var sourceFile = ProjectDirectory.FullName + "\\log4net_config\\log4net.config";
            if (!File.Exists(sourceFile))
                sourceFile = ProjectDirectory.FullName + "\\log4net.config";
            var destFile = destDir + "\\log4net.config";

            if (!File.Exists(transformFile) && File.Exists(sourceFile))
                File.Copy(sourceFile, destFile);
            else
                DoTransform(sourceFile, transformFile, destFile);
        }

        private void DoTransform(string sourceFile, string transformFile, string destFile)
        {
            if (!File.Exists(sourceFile))
            {
                Log.Warn(string.Format("No config file found in directory {0}, transformation ignored.",
                    ProjectDirectory.FullName));
                return;
            }

            if (!File.Exists(transformFile))
            {
                Log.Info(
                    string.Format(
                        "No target config file found for target {0} in directory {1}, transformation ignored.", Target,
                        ProjectDirectory.FullName));
                return;
            }

            Log.Info(string.Format("Transforming file {0} and {1} ==> {2}", sourceFile, transformFile, destFile));
            var task = new TransformXml
            {
                Source = sourceFile,
                Destination = destFile,
                Transform = transformFile,
                BuildEngine = new FakeBuildEngine()
            };
            task.Execute();
        }
    }
}