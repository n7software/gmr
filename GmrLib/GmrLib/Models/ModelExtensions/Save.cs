using System;
using System.Configuration;
using System.IO;
using System.Web;

namespace GmrLib.Models
{
    public partial class Save
    {
        public static readonly string SaveDir = Path.Combine
            (HttpContext.Current != null
                ? HttpContext.Current.Request.PhysicalApplicationPath
                : Directory.GetCurrentDirectory(),
                ConfigurationManager.AppSettings["SaveDir"] ?? @".\Saves");

        private byte[] SaveData;

        public byte[] Data
        {
            get
            {
                if (SaveData == null)
                {
                    if (File.Exists(FilePath))
                    {
                        SaveData = File.ReadAllBytes(FilePath);
                    }
                }

                return SaveData;
            }
            set
            {
                SaveData = value;
                Modified = DateTime.UtcNow;
            }
        }


        private string filePath = null;
        public string FilePath
        {
            get
            {
                if (string.IsNullOrEmpty(filePath) || SaveID < 1)
                {
                    filePath = Path.Combine(SaveDir, SaveID + ".Civ5Save");
                }

                return filePath;
            }
        }
    }
}