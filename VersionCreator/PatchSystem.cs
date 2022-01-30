using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.IO.Compression;
using System.Diagnostics;
using System.Runtime.Serialization.Formatters.Binary;
using System.Xml.Serialization;

namespace Patcher
{
    static class PatchSystem
    {
        public const string VERSION_FILE_PATH = "versionMap";
        public const string GAME_DIR = "game";
        public const string DOWNLOADING_DIR = "downloading";

        public static string GetMD5FromFile(string filePath) {
            string result;
            using (MD5 md5 = MD5.Create()) {
                using (FileStream stream = File.OpenRead(filePath))
                {
                    result = BitConverter.ToString(md5.ComputeHash(stream)).ToString().Replace("-", "");
                }
            }

            return result;
        }

        public static void SaveToDirectory(string sourceDir, string targetDir, HashMap map, Action<string> onMessage=null, Action<int> onProgress=null) {
            targetDir += (string)map.version;
                
            //check old
            if (Directory.Exists(targetDir))
            {
                Directory.Delete(targetDir, true);
                onMessage?.Invoke("Old directory found, deleted.");
            }

            //create new
            Directory.CreateDirectory(targetDir);
            onMessage?.Invoke($"Directory Created:{targetDir}");

            //Save version map
            string versFileName = $"{targetDir}\\{(string)map.version}";
            map.WriteToFile(versFileName);

            onMessage?.Invoke($"File Created:{versFileName}");

            //files
            List<Task<HashData>> tasks = new List<Task<HashData>>();
            long totalSize = 0;
            map.map.ForEach(data=>totalSize+=data.size);
            
            foreach (var data in map.map) {
                HashData curData = data;
                Task<HashData> curTask = Task.Run(() => {
                    using (FileStream fileOut = File.Create(targetDir + "\\" + curData.hash)){
                        using (GZipStream zip = new GZipStream(fileOut, CompressionLevel.Optimal)){
                            using (FileStream fileIn = File.OpenRead(sourceDir + "\\" + curData.path)){
                                fileIn.CopyTo(zip);
                            }
                        }
                    }
                    return curData;
                });
                tasks.Add(curTask);
            }

            long doneSize = 0;
            while (tasks.Count > 0) {
                int i=Task.WaitAny(tasks.ToArray());

                onMessage?.Invoke($"File Done:{tasks[i].Result.hash}");
                doneSize += tasks[i].Result.size;

                tasks[i].Dispose();
                tasks.RemoveAt(i);

                onProgress?.Invoke((int)((doneSize/(float)totalSize)*100f));
            }

            onMessage?.Invoke("All Done!");
        }

        public static void UploadDirectory(string sourceDirPath, string keyFilePath, string parentDirID, Action<string> onMessage=null, Action<int> onProgress=null) {
            GDrive drive = new GDrive();

            drive.Connect(keyFilePath);

            if (drive.isConnected)
            {
                onMessage?.Invoke("Connected to Drive");

                var files = drive.GetFiles(parentDirID, SearchTypes.FOLDERS);
                DirectoryInfo sourceDirInfo = new DirectoryInfo(sourceDirPath);

                //check existing folder
                foreach (var file in files.Files) {
                    if (file.Name == sourceDirInfo.Name) {
                        drive.Delete(file.Id);
                    }
                }

                //create folder
                string driveFolderID = drive.CreateDir(parentDirID, sourceDirInfo.Name);
                onMessage?.Invoke("Folder created on Drive:" + driveFolderID);

                //find Hashmap
                HashMap map = HashMap.ReadFromFile(sourceDirInfo.FullName + "\\" + sourceDirInfo.Name);

                //upload hashmap
                FileStream hashFile = File.OpenRead(sourceDirInfo.FullName + "\\" + sourceDirInfo.Name);

                Task<bool> uploadTask = Task.Run(() => {
                    return drive.Upload(driveFolderID, hashFile, sourceDirInfo.Name);
                });
                uploadTask.Wait();
                onMessage?.Invoke($"Upload {(uploadTask.Result ? "succes" : "failed")} path: {hashFile.Name}");

                //Upload files
                int counter = map.map.Count;
                int i = 0;
                long totalSize = 0;
                map.map.ForEach(data => totalSize += data.size);
                long uploadedSize = 0;

                foreach (var dataFile in map.map) {
                    i++;

                    Task<bool> upload = Task.Run(()=> {
                        FileStream file = File.OpenRead($"{sourceDirInfo.FullName}\\{dataFile.hash}");
                        bool result = drive.Upload(driveFolderID, file, dataFile.hash);
                        file.Close();
                        return result;
                    });

                    upload.Wait();
                    onMessage?.Invoke($"{i}/{counter} ({dataFile.size} bytes) Upload {(uploadTask.Result ? "succes" : "failed")} path: {dataFile.hash}");
                    uploadedSize += dataFile.size;
                    onProgress?.Invoke((int)((uploadedSize/(float)totalSize)*100));
                }

                onMessage?.Invoke("Upload Completed.");
            }
            else {
                onMessage?.Invoke("Connection failed.");
            }
        }

        public static Version GetLocalVersion() {
            HashMap result = HashMap.ReadFromFile(VERSION_FILE_PATH);
            return result != null ? result.version : (Version)"";
        }

        public static Version[] CheckOnlineVersions(string onlineDirID) {
            GDrive drive = new GDrive();
            drive.Connect(Configs.GetData("keyFilePath"));
            Version[] result;

            if (drive.isConnected)
            {
                Debug.WriteLine("Connected.");

                var dirList = drive.GetFiles(Configs.GetData("driveDir"), SearchTypes.FOLDERS);
                List<Version> versions = new List<Version>();
                foreach (var dir in dirList.Files) {
                    Debug.WriteLine($"Dir found:{dir.Name}");
                    versions.Add(dir.Name);
                }

                versions.Sort((v1,v2)=>{
                    return v1.CompareTo(v2);
                });

                result = versions.ToArray();
            }
            else {
                Debug.WriteLine("Connection failed.");
                result = new Version[] { };
            }

            Debug.WriteLine($"Return versions {result.Length}");
            return result;
        }

        public static void UpdateVersionTo(Version toVersion, Action<string> onMessage=null, Action<int> onProgress=null) {
            //Get Local Version
            HashMap localMap = null;
            Version localVersion = default;
            if (File.Exists(VERSION_FILE_PATH)) {
                localMap = HashMap.ReadFromFile(VERSION_FILE_PATH);
                if (localMap != null) {
                    localVersion = localMap.version;
                }
            }

            //Local Dir not exist
            if (!Directory.Exists(GAME_DIR)) {
                localVersion = default;
            }

            //versions identical
            if (localVersion == toVersion) {
                onMessage?.Invoke($"The Two Versions are the same! {localVersion} --> {toVersion}");
                return;
            }
                        
            onMessage?.Invoke($"Update version from {localVersion} to {toVersion}");

            //search online map
            GDrive drive = new GDrive();
            drive.Connect(Configs.GetData("keyFilePath"));

            if (!drive.isConnected) {
                onMessage?.Invoke("Connection failed.");
                return;
            }

            if (!Directory.Exists(DOWNLOADING_DIR)) Directory.CreateDirectory(DOWNLOADING_DIR);

            HashMap onlineMap = null;
            Google.Apis.Drive.v3.Data.FileList versionFiles = default;

            if (drive.isConnected)
            {
                var dirList = drive.GetFiles(Configs.GetData("driveDir"));
 
                //Setup onlineMap
                for (int i=0; i<dirList.Files.Count; ++i) {
                    Debug.WriteLine(dirList.Files[i].Name);
                    if (dirList.Files[i].Name == toVersion) {
                        //try download
                        string dirID = dirList.Files[i].Id;

                        Directory.CreateDirectory(DOWNLOADING_DIR);

                        versionFiles = drive.GetFiles(dirID, SearchTypes.FILES);
                        Debug.WriteLine($"Cur ver files:{versionFiles.Files.Count}");

                        foreach (var curVerFile in versionFiles.Files) {
                            Debug.WriteLine($"{curVerFile.Name} - {(string)toVersion}");
                            if (curVerFile.Name == toVersion) {
                                Task downloadTask = Task.Run(()=> {
                                    using (FileStream toMapFile = File.Create($"{DOWNLOADING_DIR}\\{VERSION_FILE_PATH}"))
                                    {
                                        drive.Download(curVerFile.Id, toMapFile);
                                    }

                                    onlineMap = HashMap.ReadFromFile($"{DOWNLOADING_DIR}\\{VERSION_FILE_PATH}");
                                });
                                downloadTask.Wait();
                                downloadTask.Dispose();
                                break;
                            }
                        }

                        break;
                    }
                }

                //Get The updateables of online map.
                if (onlineMap != null)
                {
                    List<string> downloads = new List<string>();
                    //Needs download all
                    if (localMap == null)
                    {
                        foreach (var entry in onlineMap.map) {
                            downloads.Add(entry.hash);
                        }
                    }

                    //Get newest
                    else {
                        HashData localEntry;
                        foreach (var entry in onlineMap.map)
                        {
                            localEntry = localMap.FindData(entry.path);
                            if (localEntry == null || localEntry.hash != entry.hash) {
                                downloads.Add(entry.hash);
                            }
                        }
                    }

                    //Try download them
                    onMessage?.Invoke($"Downloading files...");
                    Debug.WriteLine($"Downloading files:{downloads.Count}");
                    for (int i = 0; i < downloads.Count; i++) {
                        foreach (var onlineFile in versionFiles.Files) {
                            if (onlineFile.Name == downloads[i]) {
                                Debug.WriteLine($"Downloading file {i + 1}/{downloads.Count} {onlineFile.Name}");
                                onMessage?.Invoke($"Downloading file {i + 1}/{downloads.Count} {onlineFile.Name}");

                                Task downloadTask = Task.Run(()=> {
                                    using (FileStream outFile = File.Create($"{DOWNLOADING_DIR}\\{onlineFile.Name}"))
                                    {
                                        drive.Download(onlineFile.Id, outFile);
                                    }
                                });
                                Task.WaitAll(downloadTask);
                                downloadTask.Dispose();

                                onProgress?.Invoke((int)(i*0.5f/downloads.Count*100));
                                break;
                            }
                        }
                    }

                    //update files
                    onMessage?.Invoke("Updateing...");
                    Debug.WriteLine($"Updateing {downloads.Count} files.");

                    HashData curData = default;
                    FileInfo fileInfo;
                    for (int i = 0; i < downloads.Count; ++i)
                    {

                        foreach (var hashData in onlineMap.map) {
                            if (hashData.hash == downloads[i]) {
                                curData = hashData;
                                break;
                            }

                        }

                        //copy and unzip
                        fileInfo = new FileInfo($"{GAME_DIR}\\{curData.path}");
                        Debug.WriteLine($"Create dir:{fileInfo.DirectoryName}");
                        Directory.CreateDirectory(fileInfo.DirectoryName);
                        Debug.WriteLine($"Unpack file: {fileInfo.FullName}");

                        Task unZipTask = Task.Run(() =>
                        {
                            using (FileStream inFile = File.OpenRead($"{DOWNLOADING_DIR}\\{downloads[i]}"))
                            {
                                using (GZipStream zip = new GZipStream(inFile, CompressionMode.Decompress))
                                {
                                    using (FileStream outFile = File.Create(fileInfo.FullName))
                                    {
                                        zip.CopyTo(outFile);
                                    }
                                }                          
                            }
                            
                        });
                        Task.WaitAll(unZipTask);
                        unZipTask.Dispose();

                        onMessage?.Invoke($"File updated {i}/{downloads.Count}");
                        onProgress?.Invoke((int)(i*0.5f/downloads.Count*100)+50);
                    }

                    //Remove files
                    onMessage?.Invoke($"Check old files...");
                    List<string> oldFiles = new List<string>();
                    if (localMap != null) {
                        foreach (var data in localMap.map)
                        {
                            if (onlineMap.FindData(data.path) == null)
                            {
                                oldFiles.Add(data.path);
                            }
                        }
                    }
                    

                    //remove olds
                    foreach (var old in oldFiles) {
                        File.Delete($"{GAME_DIR}\\{old}");
                    }

                    //save new version file
                    if(onlineMap != null)onlineMap.WriteToFile(VERSION_FILE_PATH);

                    onMessage?.Invoke($"Update vas succesfull to {toVersion}");
                }
                else {
                    onMessage?.Invoke("Online version map not readable. :(");
                }
            }
            else {
                onMessage?.Invoke($"Can't connect to online!");
            }

            Directory.Delete(DOWNLOADING_DIR, true);
        }
    }

    [System.Serializable]
    public class HashMap
    {
        public Version version;

        public List<HashData> map = new List<HashData>();

        public HashMap() { 
            
        }

        public HashMap(string version)
        {
            this.version = version;
        }

        public HashData FindData(string filePath)
        {
            for (int i = 0; i < map.Count; ++i)
            {
                if (map[i].path.Equals(filePath)) return map[i];
            }
            return null;
        }

        public static HashMap CreateFromDir(string version, string dirPath, Action<string, string> onOneDone = null)
        {
            if (Directory.Exists(dirPath))
            {
                HashMap result = new HashMap(version);

                ReadDir(dirPath, result.map, dirPath, onOneDone);

                return result;
            }
            else throw new System.Exception($"You cant create HashMap from non existing directory:{dirPath}");
        }

        private static void ReadDir(string parentDir, List<HashData> dataList, string relativeDir, Action<string, string> onOneDone = null)
        {
            DirectoryInfo dirInfo = new DirectoryInfo(parentDir);

            FileInfo[] files = dirInfo.GetFiles();
            List<Task<HashData>> fileTasks = new List<Task<HashData>>();
            for (int i = 0; i < files.Length; ++i)
            {
                int j = i;
                fileTasks.Add(Task<HashData>.Run(() => {
                    HashData data = new HashData();
                    data.size = files[j].Length;
                    data.path = files[j].FullName.Replace(relativeDir, "");
                    data.hash = PatchSystem.GetMD5FromFile(files[j].FullName);
                    return data;
                }));
            }

            Task.WaitAll(fileTasks.ToArray());
            foreach (var task in fileTasks)
            {
                dataList.Add(task.Result);
                onOneDone?.Invoke(task.Result.path, task.Result.hash);
                task.Dispose();
            }

            DirectoryInfo[] dirs = dirInfo.GetDirectories();
            for (int i = 0; i < dirs.Length; ++i)
            {
                ReadDir(dirs[i].FullName, dataList, relativeDir, onOneDone);
            }
        }

        public void WriteToFile(string filePath)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                XmlSerializer serializer = new XmlSerializer(typeof(HashMap));
                serializer.Serialize(memory, this);
                memory.Position = 0;

                using (FileStream outFile = File.Create(filePath))
                {
                    using (GZipStream compressor = new GZipStream(outFile, CompressionLevel.Optimal))
                    {
                        memory.CopyTo(compressor);
                    }
                }
            }           

            /*
            using (MemoryStream memory = new MemoryStream()) {
                BinaryFormatter formatter = new BinaryFormatter();
                formatter.Serialize(memory, this);
                memory.Position = 0;

                using (FileStream outFile = File.Create(filePath)) {
                    using (GZipStream compressor = new GZipStream(outFile, CompressionLevel.Optimal)) {
                        memory.CopyTo(compressor);
                    }
                }               
            }
            */
        }

        public static HashMap ReadFromFile(string path)
        {
            if (File.Exists(path))
            {
                HashMap result;

                using (FileStream inFile = File.OpenRead(path)) {
                    using (MemoryStream memory = new MemoryStream()) {
                        using (GZipStream zip = new GZipStream(inFile, CompressionMode.Decompress)) {
                            zip.CopyTo(memory);
                        }

                        memory.Position = 0;
                        /*
                        BinaryFormatter formatter = new BinaryFormatter();
                        result = (HashMap)formatter.Deserialize(memory);
                        */
                        XmlSerializer serializer = new XmlSerializer(typeof(HashMap));
                        result = (HashMap)serializer.Deserialize(memory);
                    }
                }
                return result;
            }
            else return null;
        }
    }

    [System.Serializable]
    public struct Version : IComparable
    {
        public string[] values;

        public int length => values == null ? 0 : values.Length;

        public static implicit operator string(Version vers)
        {
            if (vers.length < 1) return "v0";
            return string.Join('.', vers.values);
        }

        public static implicit operator Version(string text)
        {
            if (string.IsNullOrEmpty(text)) return new Version() {values = new string[] {"v0"} };
            return new Version() { values = text.Split('.') };
        }

        public string this[int i]
        {
            get
            {
                if (values != null && i < values.Length)
                {
                    return values[i];
                }
                else
                {
                    return "0";
                }
            }
        }

        public static bool operator ==(Version a, Version b)
        {
            for (int i = 0; i < a.length || i < b.length; ++i)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }

        public static bool operator !=(Version a, Version b)
        {
            return !(a == b);
        }

        public static bool operator <(Version a, Version b)
        {
            for (int i = 0; i < a.length || i < b.length; ++i)
            {
                if (a[i].CompareTo(b[i]) < 0) return true;
            }
            return false;
        }

        public static bool operator >(Version a, Version b)
        {
            for (int i = 0; i < a.length || i < b.length; ++i)
            {
                if (a[i].CompareTo(b[i]) > 0) return true;
            }
            return false;
        }

        public int CompareTo(Object vers)
        {
            if (this == (Version)vers)
            {
                return 0;
            }
            else if (this < (Version)vers)
            {
                return -1;
            }
            else return 1;
        }

        public override string ToString()
        {
            return (string)this;
        }
    }

    [System.Serializable]
    public class HashData
    {
        public string path;
        public string hash;
        public long size;
    }
}
