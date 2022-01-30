using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;

namespace Patcher
{
    class Configs
    {
        const string CONF_FILE_PATH = "conf.dat";

        static List<KeyValuePair<string, string>> data = new List<KeyValuePair<string, string>>();

        static Configs()
        {
            try
            {
                string[] lineData;
                foreach (string line in File.ReadLines(CONF_FILE_PATH)){
                    lineData = line.Split("=");
                    data.Add(new KeyValuePair<string, string>(lineData[0],lineData[1]));
                }
            }
            catch (System.Exception e) {
                Debug.WriteLine(e.Message);
            }
        }

        public static string GetData(string key) {
            foreach (var curDat in data) {
                if (curDat.Key.Equals(key)) return curDat.Value;
            }

            Debug.WriteLine($"Data not found {key}");
            return "";
        }
    }
}
