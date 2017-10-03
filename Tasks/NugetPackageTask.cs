using System;
using System.Configuration;
using System.IO;
using log4net;
using SBSBuilder.Config;

namespace SBSBuilder.Tasks
{
    public class NugetPackageTask : AbstractNugetTask
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof (NugetPackageTask));
        public FileInfo ProjectFile { get; set; }
        public string Version { get; set; }
        public string Target { get; set; }
        public DirectoryInfo OutputDirectory { get; set; }

        public void Execute()
        {
            if (ProjectFile == null)
                throw new NullReferenceException("Project file must be set.");

            if (!ProjectFile.Exists)
                throw new ArgumentException(string.Format("Project file {0} does not exist.", ProjectFile.FullName));

            if (Version == null)
                throw new NullReferenceException("Version must be set.");

            if (Target == null || Target.Trim().Length == 0)
                throw new NullReferenceException("Target must be set.");

            if (NugetExe == null)
                throw new NullReferenceException("Nuget executable must be set.");

            if (OutputDirectory == null)
                throw new NullReferenceException("Output directory must be set.");

            var buildDefaults = (BuildDefaultsSection) ConfigurationManager.GetSection("buildDefaults");
            var properties = buildDefaults.PropertiesForTarget(Target);

            string dest;
            dest = Path.Combine(ProjectFile.DirectoryName,
                "anycpu".Equals(properties["PlatformTarget"])
                    ? string.Format("{0}\\{1}", "bin", properties["Configuration"])
                    : string.Format("{0}\\{1}\\{2}", "bin", properties["PlatformTarget"], properties["Configuration"]));
            CopyAll(OutputDirectory, new DirectoryInfo(dest));

            var args =
                string.Format(
                    "pack -Verbose -Verbosity detailed -Version {0} -sym \"{1}\" -OutputDirectory {2} -Prop Platform={3}",
                    GetNugetVersion(Version, Target), ProjectFile.FullName, ProjectFile.DirectoryName,
                    properties["PlatformTarget"]);

            // Need to create a empty solution so Nuget pack will include dependencies...dumb I know.
            var emptySnl = Path.Combine(ProjectFile.Directory.Parent.FullName, "empty.sln");
            File.Create(emptySnl).Dispose();

            ExecuteNugetTask(args);

            File.Delete(emptySnl);
        }

        public static void CopyAll(DirectoryInfo source, DirectoryInfo target)
        {
            // Check if the target directory exists; if not, create it.
            if (Directory.Exists(target.FullName) == false)
            {
                Directory.CreateDirectory(target.FullName);
            }

            // Copy each file into the new directory.
            foreach (var fi in source.GetFiles())
            {
                Log.DebugFormat(@"Copying {0}\{1}", target.FullName, fi.Name);
                fi.CopyTo(Path.Combine(target.FullName, fi.Name), true);
            }

            // Copy each subdirectory using recursion.
            foreach (var diSourceSubDir in source.GetDirectories())
            {
                var nextTargetSubDir =
                    target.CreateSubdirectory(diSourceSubDir.Name);
                CopyAll(diSourceSubDir, nextTargetSubDir);
            }
        }
    }
}