﻿using System;
using System.Windows.Threading;
using NLog;
using Wox.Infrastructure;
using Wox.Infrastructure.Exception;

namespace Wox.Helper {
    public static class ErrorReporting {
        private static void Report(Exception e) {
            Logger logger = LogManager.GetLogger("UnHandledException");
            logger.Error(ExceptionFormatter.FormatExcpetion(e));
            ReportWindow reportWindow = new ReportWindow(e);
            reportWindow.Show();
        }

        public static void UnhandledExceptionHandle(object sender, UnhandledExceptionEventArgs e) {
            //handle non-ui thread exceptions
            Report((Exception) e.ExceptionObject);
        }

        public static void DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e) {
            //handle ui thread exceptions
            Report(e.Exception);
            //prevent application exist, so the user can copy prompted error info
            e.Handled = true;
        }

        public static string RuntimeInfo() {
            string info = $"\nWox version: {Constant.Version}" +
                          $"\nOS Version: {Environment.OSVersion.VersionString}" +
                          $"\nIntPtr Length: {IntPtr.Size}" +
                          $"\nx64: {Environment.Is64BitOperatingSystem}";
            return info;
        }

        public static string DependenciesInfo() {
            string info = $"\nPython Path: {Constant.PythonPath}" +
                          $"\nEverything SDK Path: {Constant.EverythingSDKPath}";
            return info;
        }
    }
}