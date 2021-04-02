using System;
using System.Configuration;
using System.IO;

namespace GmrLib
{
    public static class DebugLogger
    {
        private static readonly object _lockFile = new object();
        private static readonly object _lockPath = new object();
        public static string LogFilePath { get; set; }

        private static void checkFile()
        {
            lock (_lockPath)
            {
                if (string.IsNullOrEmpty(LogFilePath))
                {
                    string applicationName = "GmrServer";
                    LogFilePath = ConfigurationManager.AppSettings["LogFolderPath"];
                    LogFilePath = Path.Combine(LogFilePath, applicationName);

                    if (!Directory.Exists(LogFilePath))
                    {
                        Directory.CreateDirectory(LogFilePath);
                    }
                    LogFilePath = Path.Combine(LogFilePath, "debug_log.txt");
                }
            }
        }

        public static void WriteLine(string codeFile, string textToLog)
        {
            checkFile();

            lock (_lockFile)
            {
                StreamWriter file = File.AppendText(LogFilePath);
                file.WriteLine();
                file.WriteLine();
                file.WriteLine("--------------- {0} | {1} ---------------", DateTime.Now.ToShortDateString(),
                    DateTime.Now.ToLongTimeString());
                file.WriteLine("Source: " + codeFile);
                file.Write(textToLog);
                file.Flush();
                file.Close();
            }
        }

        public static void WriteException(Exception exc)
        {
            checkFile();

            lock (_lockFile)
            {
                StreamWriter file = File.AppendText(LogFilePath);
                file.WriteLine();
                file.WriteLine();
                file.WriteLine("--------------- {0} | {1} ---------------", DateTime.Now.ToShortDateString(),
                    DateTime.Now.ToLongTimeString());
                file.Write(exc.ToString());
                file.Flush();
                file.Close();
            }
        }

        public static void WriteException(Exception exc, string comments)
        {
            checkFile();

            lock (_lockFile)
            {
                StreamWriter file = File.AppendText(LogFilePath);
                file.WriteLine();
                file.WriteLine();
                file.WriteLine("--------------- {0} | {1} ---------------", DateTime.Now.ToShortDateString(),
                    DateTime.Now.ToLongTimeString());
                file.Write(exc.ToString());
                file.WriteLine();
                file.WriteLine();
                file.WriteLine(comments);
                file.Flush();
                file.Close();
            }
        }

        public static string GetTempDirectory()
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "GMR");
            try
            {
                Directory.CreateDirectory(path);
            }
            catch (Exception)
            {
            }

            return path;
        }
    }
}