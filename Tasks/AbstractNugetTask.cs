using System;
using System.Diagnostics;
using System.IO;
using log4net;

namespace SBSBuilder.Tasks
{
    public abstract class AbstractNugetTask
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof (AbstractNugetTask));
        public FileInfo NugetExe { get; set; }

        protected void ExecuteNugetTask(string args)
        {
            Log.DebugFormat("{0} {1}", NugetExe.FullName, args);

            var p = new Process();
            // Redirect the output stream of the child process.
            p.StartInfo.CreateNoWindow = false;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.FileName = NugetExe.FullName;
            p.StartInfo.Arguments = args;
            p.Start();

            var reader = p.StandardOutput;
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                Log.Debug(line);
            }
            p.WaitForExit();
            if (p.ExitCode != 0)
                throw new Exception("Nuget command failed with exit code: " + p.ExitCode + ".");
        }

        protected string GetNugetVersion(string version, string target)
        {
            var v = new Version(version);
            var nugetVersion = string.Format("{0}.{1}.{2}", v.Major, v.Minor, v.Build);
            if (!"prod".Equals(target.ToLower()))
                nugetVersion += "-" + target.ToLower();
            return nugetVersion;
        }
    }
}