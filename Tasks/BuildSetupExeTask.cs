using System;
using System.IO;
using System.Net;
using System.Text;
using Chilkat;
using log4net;
using SBSBuilder.Config;

namespace SBSBuilder.Tasks
{
    public class BuildSetupExeTask : ITask
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(BuildSetupExeTask));

        public DirectoryInfo BuildDirectory { get; set; }
        public FileInfo ProjectFile { get; set; }
        public string AssemblyName { get; set; }
        public string Target { get; set; }
        public string Version { get; set; }
        public InstallType? Type { get; set; }
        public string ServiceName { get; set; }
        public string WebsiteName { get; set; }
        public string AppPoolName { get; set; }
        public ArchiveLocation Archive { get; set; }
        public string ArchiveZipExeFile { get; private set; }

        public void Execute()
        {
            var sbsInstallHome = Environment.GetEnvironmentVariable("SBS_INSTALL_HOME");
            if (sbsInstallHome == null)
                throw new NullReferenceException("The location of the SBSInstall.exe directory must be set. Set the %SBS_INSTALL_HOME% environment variable on your computer.");

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

            if (Archive == null)
                throw new NullReferenceException("Archive location must be set.");

            if (Type == null)
                throw new Exception("Installation must be set.");

            if ((Type == InstallType.Service || Type == InstallType.WebService) &&
                     string.IsNullOrEmpty(ServiceName))
                throw new Exception("Sevice name must be specified for Service and WebService installations.");

            var projectName = Path.GetFileNameWithoutExtension(ProjectFile.FullName);
            var zipDir = BuildDirectory.FullName + "\\_PublishedWebsites\\" + projectName;
            if (!Directory.Exists(zipDir))
                zipDir = BuildDirectory.FullName;

            var paths = Directory.GetFiles(zipDir, "*.*", SearchOption.AllDirectories);
            if (paths.Length == 0)
            {
                Log.Warn(string.Format("No files found to zip in directory {0}. ", zipDir));
                return;
            }
            
            var zipFileName = string.Format("{0}\\Setup-{1}-{2}-{3}.zip", Archive.Uri, AssemblyName, Target, Version);

            var zip = new Zip();
            zip.ExeXmlConfig = ((SfxConfig)System.Configuration.ConfigurationManager.GetSection("SfxConfig")).ExeXmlConfig;
            zip.UnlockComponent("SHUTTEZIP_sYpChNabpHrd");
            zip.NewZip(zipFileName);

            zip.PathPrefix = "application/";
            zip.AppendFiles(zipDir, true);

            zip.PathPrefix = "installer/";
            zip.AppendFiles(sbsInstallHome, true);

            zip.AutoTemp = true;
            zip.AutoRun = "installer/SBSInstaller.exe";
            var args = "-t {0} -a {1} -d application";
            if (!string.IsNullOrEmpty(ServiceName))
                args += " -s \"{2}\"";
            if (!string.IsNullOrEmpty(WebsiteName))
                args += " -w \"{3}\"";
            if (!string.IsNullOrEmpty(AppPoolName))
                args += " -p \"{4}\"";
            zip.AutoRunParams = string.Format(args, Type, AssemblyName, ServiceName, WebsiteName, AppPoolName);

            zip.ExeTitle = string.Format("Self Extracting Installation for {0} {1}", AssemblyName, Type);
            
            var fileName = string.Format("Setup--{0}-{1}.exe", Target, Version);
            var zipExeFileName = Archive.Archive(zip, Target, fileName);
            ArchiveZipExeFile = zipExeFileName;
            
            Log.Info(string.Format("Compression of directory {0} to installation file {1} complete.", zipDir, zipExeFileName));
        }
    }

    public class ArchiveLocation
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(ArchiveLocation));

        public string Uri { get; set; }
        public string User { get; set; }
        public string Password { get; set; }

        public string Archive(Zip zip, string target, string fileName)
        {
            return Uri.StartsWith(@"http://") ? DoHttpPut(zip, target, fileName) : DoFilePut(zip, target, fileName);
        }

        private string DoFilePut(Zip zip, string target, string fileName)
        {
            var netCache = new CredentialCache();
            if (Uri.StartsWith(@"\\"))
            {
                // Todo - when dealing with UNC paths's that require auth, this doesn't work.  
                // Todo - I found this solution online, but it doesn't work.  We probably don't
                // Todo - deal with this anyway since we are pushing everything to Artificatory and 
                // Todo - not file shares.
                var credentials = ToNetworkCredentials();
                if (credentials != null)
                {
                    var uri = new Uri(Uri);
                    netCache.Add(new Uri("\\\\" + uri.Host), "Basic", credentials);
                }
            }

            var outputDirectory = new DirectoryInfo(Uri);
            if (!outputDirectory.Exists)
                outputDirectory.Create();

            var targetUri = string.Format(Uri, target);
            var zipExeFileName = string.Format("{0}\\{1}", targetUri, fileName);
            Log.Info("Writing file to: " + zipExeFileName + ".");
            if (!zip.WriteExe(zipExeFileName))
            {
                var msg = string.Format("Unable to create self extracting zip file: {0}, cause: {1}", zipExeFileName,
                    zip.LastErrorText);
                Log.Error(msg);
                throw new Exception(msg);
            }
            return zipExeFileName;
        }

        private string DoHttpPut(Zip zip, string target, string fileName)
        {
            var targetUri = string.Format(Uri, target);
            var zipExeFileName = string.Format("{0}/{1}", targetUri, fileName);
            Log.Info("Putting file to: " + zipExeFileName + ".");
            var webClient = new WebClient();
            using (webClient)
            {
                webClient.Credentials = ToNetworkCredentials();
                byte[] data = zip.WriteExeToMemory();
                byte[] responseArray = webClient.UploadData(zipExeFileName, "PUT", data);

                // Decode and display the response.
                Log.Info(string.Format("Response Received.The contents of the file uploaded are:\n{0}",
                    Encoding.ASCII.GetString(responseArray)));
            }
            return zipExeFileName;
        }

        private NetworkCredential ToNetworkCredentials()
        {
            if (!string.IsNullOrEmpty(User) || !string.IsNullOrEmpty(User))
            {
                return new NetworkCredential(User, Password);
            }
            return null;
        }
    }
}
