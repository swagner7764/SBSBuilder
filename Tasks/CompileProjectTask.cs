using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using log4net;
using Microsoft.Build.BuildEngine;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using SBSBuilder.Config;

namespace SBSBuilder.Tasks
{
    public class CompileProjectTask : ITask
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof (CompileProjectTask));
        public FileInfo ProjectFile { get; set; }
        public DirectoryInfo OutputDirectory { get; set; }
        public string Target { get; set; }
        public FileInfo Executable { get; private set; }

        public void Execute()
        {
            if (ProjectFile == null)
                throw new NullReferenceException("Project file must be set.");

            if (!ProjectFile.Exists)
                throw new ArgumentException(string.Format("Project file {0} does not exist.", ProjectFile.FullName));

            if (Target == null || Target.Trim().Length == 0)
                throw new NullReferenceException("Target must be set.");

            var buildDefaults = (BuildDefaultsSection) ConfigurationManager.GetSection("buildDefaults");
            var buildTarget = buildDefaults[Target];
            if (buildTarget == null)
                Log.Warn(string.Format("Target {0} is not recognized, default build parameters will be used.", Target));

            if (OutputDirectory.Exists)
                OutputDirectory.Delete(true);

            OutputDirectory.Create();

            var logger = new ConsoleLogger();
            var bp = new BuildParameters(new ProjectCollection())
            {
                DetailedSummary = true,
                Loggers = new List<ILogger> {logger}
            };

            Log.Info(string.Format("Compiling project file {0}.", ProjectFile.FullName));

            var properties = buildDefaults.PropertiesForTarget(Target);
            properties.Add("OutputPath", OutputDirectory.FullName);
            var buildRequest = new BuildRequestData(ProjectFile.FullName, properties, null, new[] {"Build"}, null);
            var buildResult = BuildManager.DefaultBuildManager.Build(bp, buildRequest);
            // Todo - handle build result return values.
            var targetResult = buildResult.ResultsByTarget["Build"];
            if (targetResult != null)
            {
                var targetResultItem = targetResult.Items[0];
                if (targetResultItem != null)
                    Executable = new FileInfo(targetResultItem.ItemSpec);
            }

            Log.Info(string.Format("Compilation of project file {0} complete.", ProjectFile.FullName));
        }
    }
}