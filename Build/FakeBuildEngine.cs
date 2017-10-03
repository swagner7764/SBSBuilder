using System;
using System.Collections;
using log4net;
using Microsoft.Build.Framework;

namespace SBSBuilder.Build
{
    public class FakeBuildEngine : IBuildEngine
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof (FakeBuildEngine));

        public bool BuildProjectFile(
            string projectFileName, string[] targetNames,
            IDictionary globalProperties,
            IDictionary targetOutputs)
        {
            throw new NotImplementedException();
        }

        public int ColumnNumberOfTaskNode
        {
            get { return 0; }
        }

        public bool ContinueOnError
        {
            get { throw new NotImplementedException(); }
        }

        public int LineNumberOfTaskNode
        {
            get { return 0; }
        }

        public void LogCustomEvent(CustomBuildEventArgs e)
        {
            Log.Debug(e.Message);
        }

        public void LogErrorEvent(BuildErrorEventArgs e)
        {
            Log.Error(e.Message);
        }

        public void LogMessageEvent(BuildMessageEventArgs e)
        {
            Log.Info(e.Message);
        }

        public void LogWarningEvent(BuildWarningEventArgs e)
        {
            Log.Warn(e.Message);
        }

        public string ProjectFileOfTaskNode
        {
            get { return "fake ProjectFileOfTaskNode"; }
        }
    }
}