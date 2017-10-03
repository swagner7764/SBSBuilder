using System;
using System.IO;
using log4net;

namespace SBSBuilder.Tasks
{
    public class NugetPushTask : AbstractNugetTask
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof (NugetPackageTask));
        public DirectoryInfo ProjectDirectory { get; set; }
        public string AssemblyName { get; set; }
        public string Version { get; set; }
        public string Target { get; set; }
        public ArchiveLocation Archive { get; set; }

        public void Execute()
        {
            if (ProjectDirectory == null)
                throw new NullReferenceException("Project directory must be set.");

            if (AssemblyName == null)
                throw new NullReferenceException("Assembly name must be set.");

            if (Version == null)
                throw new NullReferenceException("Version must be set.");

            if (Target == null || Target.Trim().Length == 0)
                throw new NullReferenceException("Target must be set.");

            if (NugetExe == null)
                throw new NullReferenceException("Nuget executable must be set.");

            if (Archive == null)
                throw new NullReferenceException("Archive location must be set.");

            var nupkg = Path.Combine(ProjectDirectory.FullName,
                string.Format("{0}.{1}.symbols.nupkg", AssemblyName, GetNugetVersion(Version, Target)));
            if (!File.Exists(nupkg))
                throw new FileNotFoundException("Nuget package file not found.", nupkg);

            var args = string.Format("push {0} -Source {1} -ApiKey {2}", nupkg, Archive.Uri, Archive.Password);

            ExecuteNugetTask(args);
        }
    }
}