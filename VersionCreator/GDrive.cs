using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using System.Diagnostics;
using System.Text;

namespace Patcher
{
    public enum SearchTypes {FILES,FOLDERS,FILES_AND_FOLDERS}

    public class GDrive
    {
        public bool isConnected { get; private set; } = false;

        GoogleCredential credentials;
        DriveService service;

        public void Connect(string keyFilePath) {
            if (isConnected) {
                Debug.WriteLine("Already Connected");
                return;
            }

            credentials = GoogleCredential.FromFile(keyFilePath).CreateScoped(DriveService.ScopeConstants.Drive);
            service = new DriveService(new BaseClientService.Initializer() {HttpClientInitializer = credentials});

            isConnected = true;  
        }

        public FileList GetFiles(string parentDirID, SearchTypes search = SearchTypes.FILES_AND_FOLDERS, string namePattern="*") {
            if (isConnected)
            {
                var request = service.Files.List();

                StringBuilder query = new StringBuilder();

                //parent dir
                query.Append($"parents in '{parentDirID}'");
                
                //Files or Folders
                switch (search) {
                    case SearchTypes.FILES:
                        query.Append(" and mimeType != 'application/vnd.google-apps.folder'");
                        break;
                    case SearchTypes.FOLDERS:
                        query.Append(" and mimeType = 'application/vnd.google-apps.folder'");
                        break;
                    default:
                        break;
                }

                //Name pattern
                if (namePattern != "*") {
                    query.Append($" and name contains '{namePattern}'");
                }

                request.Q = query.ToString();
                request.PageSize = 1000;
                FileList result = request.Execute();
                return result;
            }
            else return null;
        }

        public void Download(string fileID, System.IO.Stream stream) {
            
            if (isConnected)
            {
                var request = service.Files.Get(fileID);
                request.MediaDownloader.ProgressChanged += (status) => Debug.WriteLine($"{status.Status.ToString()}\n{status.Exception?.Message}") ;
                request.Download(stream);  
            }
        }

        public bool isFileExist(string fileName) {
            if (isConnected)
            {
                var query = service.Files.List();
                query.Q = $"name contains '{fileName}'";
                FileList result = query.Execute();
                return result.Files.Count > 0;
            }
            else return false;
        }

        public File GetFileData(string fileID) {
            if (isConnected)
            {
                var request = service.Files.Get(fileID);
                request.Fields = "id,parents,description";
                return request.Execute();
            }
            else return null;
        }

        public bool Upload(string parentDirID, System.IO.Stream sourceStream, string fileName) {
            if (isConnected)
            {
                var driveFile = new Google.Apis.Drive.v3.Data.File();
                driveFile.Name = fileName;
                driveFile.Parents = new string[] { parentDirID };

                var request = service.Files.Create(driveFile, sourceStream, "");
                request.Fields = "id";

                var response = request.Upload();

                if (response.Exception != null) Debug.WriteLine(response.Exception.Message);

                return response.Status == Google.Apis.Upload.UploadStatus.Completed;
            }
            else return false;
        }

        public bool Update(string onlineFileID, System.IO.Stream sourceStream) {
            if (isConnected)
            {
                var oldMeta = GetFileData(onlineFileID);
                File newMeta = new File();
                newMeta.Name = oldMeta.Name;
                newMeta.Parents = oldMeta.Parents;
                newMeta.Description = oldMeta.Description;

                var request = service.Files.Update(newMeta, onlineFileID, sourceStream, "");

                var response = request.Upload();

                if (response.Exception != null) Debug.WriteLine(response.Exception.Message);

                return response.Status == Google.Apis.Upload.UploadStatus.Completed;
            }
            else return false;
        }

        public bool Rename(string fileID, string newName) {
            if (isConnected)
            {
                var oldMeta = GetFileData(fileID);

                File newMeta = new File();
                newMeta.Name = newName;
                newMeta.Description = oldMeta.Description;
                newMeta.Parents = oldMeta.Parents;

                var request = service.Files.Update(newMeta, fileID);
                request.Fields = "id";

                request.Execute();

                return true;
            }
            else return false;
        }

        public string CreateDir(string parentDirID, string dirName) {
            if (isConnected)
            {
                var driveFile = new File();
                driveFile.Name = dirName;
                driveFile.MimeType = "application/vnd.google-apps.folder";
                driveFile.Parents = new string[] { parentDirID };

                var request = service.Files.Create(driveFile);
                request.Fields = "id";
                var result = request.Execute();

                Debug.WriteLine($"Folder created:{result.Id}");
                return result.Id;
            }
            else return "";
        }

        public void Delete(string fileID) {
            if (isConnected)
            {
                var request = service.Files.Delete(fileID);
                var result = request.Execute();

                Debug.WriteLine(result);
            }
        }

        public bool Move(string sourceFileID, string targetFolderID) {
            if (isConnected)
            {
                var oldMeta = GetFileData(sourceFileID);
                if (oldMeta == null) {
                    Debug.WriteLine("File not found on drive!");
                    return false;
                }

                if (oldMeta.Parents == null) Debug.WriteLine("oldmeta parents null");

                File newMeta = new File();
                newMeta.Name = oldMeta.Name;
                newMeta.Description = oldMeta.Description;

                var request = service.Files.Update(newMeta, sourceFileID);
                request.RemoveParents = oldMeta.Parents[0];
                request.AddParents = targetFolderID;
                request.Fields = "id";

                request.Execute();
                return true;
            }
            else return false;
        }

        public void Copy(string sourceID, string targetParentID, string targetName) {
            if (isConnected)
            {
                var driveFile = GetFileData(sourceID);

                File newFile = new File();
                newFile.Name = targetName;
                newFile.Description = driveFile.Description;
                newFile.Parents = new string[] {targetParentID};

                var request = service.Files.Copy(newFile, sourceID);
                request.Execute();
            }
        }
    }
}
