//using Microsoft.Web.Publishing.Tasks;

using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Fclp;
using log4net;
using log4net.Config;
using SBSBuilder.Tasks;

namespace SBSBuilder
{
    internal class Program
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof (Program));

        private static void Main(string[] args)
        {

            XmlConfigurator.Configure();

            var options = new Options();
            var result = options.Parse(args);

            Log.Debug(options);

            if (result.HasErrors)
            {
                Log.Error(result.ErrorText);
                Environment.Exit(-1);
            }

            if (options.HasErrors())
            {
                Log.Error(options.ErrorText);
                Environment.Exit(-1);
            }

            Log.Debug(options);
            var projectFile = new FileInfo(options.ProjectFile);
            var outputDir = new DirectoryInfo(options.TempDirectory + "\\" + Guid.NewGuid());

            var archiveLocation = new ArchiveLocation
            {
                Uri = options.ArchiveLocation,
                User = options.ArchiveUser,
                Password = options.ArchivePassword
            };

            var exitCode = 0;
            try
            {
                // ReSharper disable once PossibleInvalidOperationException
                ExecuteBuild(projectFile, options.Version, options.TargetsAsArray, (InstallType)options.Type,
                    options.ServiceName, options.WebsiteName, options.AppPoolName, outputDir, archiveLocation);
            }
            catch (Exception e)
            {
                Log.Error("Unable to build project, cause: " + e, e);
                exitCode = 1;
            }
            finally
            {
                if (outputDir.Exists)
                    outputDir.Delete(true);
            }

            Environment.Exit(exitCode);
        }

        private static void ExecuteBuild(FileInfo projectFile, string version, IEnumerable<string> targets,
            InstallType installType, string serviceName, string websiteName, string appPoolName,
            DirectoryInfo outputDir, ArchiveLocation archiveLocation)
        {
            var projectDir = projectFile.Directory;
            var assemblyName = GetAssemblyName(projectFile);
            var nugetExe = new FileInfo(ConfigurationManager.AppSettings["NuGet.exe"]);

            var nugetRestoreTask = new NugetRestoreTask
            {
                ProjectDirectory = projectDir,
                NugetExe = nugetExe
            };
            nugetRestoreTask.Execute();

            var updateAssemblyTask = new UpdateAssemblyTask
            {
                ProjectDirectory = projectDir,
                Version = version
            };
            updateAssemblyTask.Execute();

            foreach (var s in targets)
            {
                var target = s.ToLower();
                var compileProjectTask = new CompileProjectTask
                {
                    ProjectFile = projectFile,
                    OutputDirectory = outputDir,
                    Target = target
                };
                compileProjectTask.Execute();

                var transformConfigTask = new TransformConfigTask
                {
                    ProjectDirectory = projectDir,
                    BuildDirectory = outputDir,
                    ProjectName = Path.GetFileNameWithoutExtension(projectFile.FullName),
                    Type = installType,
                    Executable = compileProjectTask.Executable,
                    Target = target
                };
                transformConfigTask.Execute();

                if (InstallType.Library == installType)
                {
                    var nugetPackageTask = new NugetPackageTask
                    {
                        ProjectFile = projectFile,
                        Version = version,
                        Target = target,
                        NugetExe = nugetExe,
                        OutputDirectory = outputDir
                    };
                    nugetPackageTask.Execute();

                    var nugetPushTask = new NugetPushTask
                    {
                        ProjectDirectory = projectDir,
                        Version = version,
                        AssemblyName = assemblyName,
                        Target = target,
                        NugetExe = nugetExe,
                        Archive = archiveLocation
                    };
                    nugetPushTask.Execute();
                }
                else
                {
                    var buildSetupExeTask = new BuildSetupExeTask
                    {
                        BuildDirectory = outputDir,
                        ProjectFile = projectFile,
                        AssemblyName = assemblyName,
                        Target = target,
                        Version = version,
                        Type = installType,
                        ServiceName = serviceName,
                        WebsiteName = websiteName,
                        AppPoolName = appPoolName,
                        Archive = archiveLocation
                    };
                    buildSetupExeTask.Execute();
                }
            }
        }

        private static string GetAssemblyName(FileInfo projectFile)
        {
            var doc = new XmlDocument();
            doc.Load(projectFile.OpenRead());
            var root = doc.DocumentElement;
            if (root == null)
                throw new Exception(string.Format("Invalid project file: {0}, file does not contain a root element.",
                    projectFile));

            var ns = root.NamespaceURI;
            XmlNode node;
            if (ns.Length > 0)
            {
                var nsmgr = new XmlNamespaceManager(doc.NameTable);
                nsmgr.AddNamespace("x", root.NamespaceURI);
                node = doc.SelectSingleNode("//x:PropertyGroup/x:AssemblyName", nsmgr);
            }
            else
            {
                node = doc.SelectSingleNode("//PropertyGroup/AssemblyName");
            }

            if (node == null)
            {
                var name = Path.GetFileNameWithoutExtension(projectFile.FullName);
                Log.Warn(
                    string.Format(
                        "Unable to determine assembly name for project file {0}, assuming assembly name from project file name.",
                        projectFile.FullName));
                return name;
            }

            return node.InnerText;
        }
    }


    // Define a class to receive parsed values
    internal class Options
    {
        private readonly string _archiveLocation = Environment.GetEnvironmentVariable("BUILD_ARCHIVE_LOCATION");
        private readonly string _archivePassword = Environment.GetEnvironmentVariable("SBS_ARTIFACTORY_PASSWORD");
        private readonly string _archiveUser = Environment.GetEnvironmentVariable("SBS_ARTIFACTORY_USER");
        private readonly FluentCommandLineParser _parser;
        private readonly string _projectFile = Environment.GetEnvironmentVariable("BUILD_PROJECT_FILE");
        private readonly string _serviceName = Environment.GetEnvironmentVariable("BUILD_SERVICE_NAME");
        private readonly string _websiteName = Environment.GetEnvironmentVariable("BUILD_WEBSITE_NAME");
        private readonly string _appPoolName = Environment.GetEnvironmentVariable("BUILD_APPPOOL_NAME");
        private readonly string _targets = Environment.GetEnvironmentVariable("BUILD_TARGETS");
        private readonly string _tempDirectory = Path.GetTempPath();
        private readonly string _typeString = Environment.GetEnvironmentVariable("BUILD_TYPE");
        private readonly string _version = Environment.GetEnvironmentVariable("BUILD_VERSION");

        public Options()
        {
            _parser = new FluentCommandLineParser();

            _parser.Setup<string>('p', "project")
                   .Callback(project => ProjectFile = project)
                   .SetDefault(_projectFile)
                   .WithDescription("Project file to build.");
            _parser.Setup<string>('i', "install_type")
                   .Callback(type => TypeString = type)
                   .SetDefault(_typeString)
                   .WithDescription(
                       "The type of application we are installing [WebService, Service, Application, Scheduled task, Library].");
            _parser.Setup<string>('s', "service_name")
                   .Callback(s => ServiceName = s)
                   .SetDefault(_serviceName)
                   .WithDescription("The service name. Only applies to WebService and Service installations.");
            _parser.Setup<string>('w', "website_name")
                   .Callback(s => WebsiteName = s)
                   .SetDefault(_websiteName)
                   .WithDescription("The IIS website name. Only applies to WebService installations.");
            _parser.Setup<string>('a', "apppool_name")
                   .Callback(s => AppPoolName = s)
                   .SetDefault(_appPoolName)
                   .WithDescription("The IIS app pool name. Only applies to WebService installations.");
            _parser.Setup<string>('v', "version")
                   .Callback(s => Version = s)
                   .SetDefault(_version)
                   .WithDescription("Version number of project build.");
            _parser.Setup<string>('t', "targets")
                   .Callback(s => Targets = s)
                   .SetDefault(_targets)
                   .WithDescription("Comma separated list of targets to build.");
            _parser.Setup<string>("archive_location")
                   .Callback(s => ArchiveLocation = s)
                   .SetDefault(_archiveLocation)
                   .WithDescription("Sets the archive location (file system or HTTP).");
            _parser.Setup<string>("archive_user")
                   .Callback(s => ArchiveUser = s)
                   .SetDefault(_archiveUser)
                   .WithDescription("The optional user name of the archive location.");
            _parser.Setup<string>("archive_password")
                   .Callback(s => ArchivePassword = s)
                   .SetDefault(_archivePassword)
                   .WithDescription("The optional user password of the archive location.");
            _parser.Setup<string>("temp_directory")
                   .Callback(s => TempDirectory = s)
                   .SetDefault(_tempDirectory)
                   .WithDescription("Overrides the configued temp/working directory for builds.");
            _parser.Setup<string>("archive_directory")
                   .Callback(s => ArchiveLocation = s)
                   .WithDescription("DEPRECATED: Sets the archive location.");
        }

        public string ProjectFile { get; set; }
        public string TypeString { get; set; }
        public string ServiceName { get; set; }
        public string WebsiteName { get; set; }
        public string AppPoolName { get; set; }
        public string Version { get; set; }
        public string Targets { get; set; }
        public string ArchiveLocation { get; set; }
        public string ArchiveUser { get; set; }
        public string ArchivePassword { get; set; }
        public string TempDirectory { get; set; }
        public string ErrorText { get; private set; }

        public string ArchiveDirectory
        {
            set { ArchiveLocation = value; }
            get { return ArchiveLocation; }
        }

        public string[] TargetsAsArray
        {
            get
            {
                var t = Targets;
                if (t == null)
                    return new string[0];

                var values = t.Split(',').Select(sValue => sValue.Trim()).ToArray();
                return values;
            }
        }

        public InstallType? Type
        {
            get
            {
                switch (TypeString.ToUpper())
                {
                    case "APP":
                    case "APPLICATION":
                        return InstallType.Application;
                    case "SCHED":
                    case "SCHEDULEDTASK":
                        return InstallType.ScheduledTask;
                    case "WEB":
                    case "WEBSERVICE":
                    case "WEB_SERVICE":
                        return InstallType.WebService;
                    case "SVC":
                    case "SERVICE":
                        return InstallType.Service;
                    case "LIB":
                    case "LIBRARY":
                        return InstallType.Library;
                    case "SELFHOSTEDSERVICE":
                        return InstallType.SelfHostedService;
                    default:
                        return null;
                }
            }
        }

        public ICommandLineParserResult Parse(string[] args)
        {
            return _parser.Parse(args);
        }

        public bool HasErrors()
        {
            ErrorText = null;
            if (string.IsNullOrEmpty(ProjectFile))
            {
                ErrorText = "Project file must be specified.";
                return true;
            }

            if (Type == null)
            {
                ErrorText = string.Format("Invalid installation type: {0}, type must be {1}, {2}, {3}, {4} or {5}",
                    TypeString, InstallType.Application, InstallType.Service, InstallType.WebService,
                    InstallType.ScheduledTask,
                    InstallType.Library);
                return true;
            }

            if ((Type == InstallType.Service || Type == InstallType.WebService) &&
                string.IsNullOrEmpty(ServiceName))
            {
                ErrorText = "Service name must be specified for Service and WebService installations.";
                return true;
            }

            if (string.IsNullOrEmpty(Version))
            {
                ErrorText = "Version must be specifed.";
                return true;
            }

            if (string.IsNullOrEmpty(Targets))
            {
                ErrorText = "Targets must be specifed.";
                return true;
            }

            if (string.IsNullOrEmpty(ArchiveLocation))
            {
                ErrorText = "Archive location must be specified.";
                return true;
            }

            return false;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("ProjectFile: ").Append(ProjectFile).AppendLine();
            sb.Append("InstallType: ").Append(TypeString).AppendLine();
            sb.Append("ServiceName: ").Append(ServiceName).AppendLine();
            sb.Append("WebsiteName: ").Append(WebsiteName).AppendLine();
            sb.Append("AppPoolName: ").Append(AppPoolName).AppendLine();
            sb.Append("Version: ").Append(Version).AppendLine();
            sb.Append("Targets: ").Append(Targets).AppendLine();
            sb.Append("ArchiveLocation: ").Append(ArchiveLocation).AppendLine();
            sb.Append("ArchiveUser: ").Append(ArchiveUser).AppendLine();
            sb.Append("ArchivePassword: ").Append(ArchivePassword ?? "******************").AppendLine();
            sb.Append("TempDirectory: ").Append(TempDirectory).AppendLine();
            return sb.ToString();
        }
    }
}