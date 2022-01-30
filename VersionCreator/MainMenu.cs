using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.Threading;
using System.IO;

namespace Patcher
{
    public partial class MainMenu : Form
    {
        string folderPath;
        HashMap createdMap;
        string version;

        public MainMenu()
        {
            InitializeComponent();
            InitStartState();
            StartPosition = FormStartPosition.CenterScreen;
            progressBar.Visible = false;
            ActivateButtons(true);
        }

        void InitStartState() {
            textBox.AcceptsReturn = true;
            textBox.AppendText($"Started at: {DateTime.Now.ToLocalTime()}");
            textBox.AppendText($"{Environment.NewLine}There is no target directory!");
        }

        void ActivateButtons(bool on) {
            //ON
            if (on)
            {
                openDirButton.Enabled = true;
                createPatchMapButton.Enabled = !string.IsNullOrEmpty(folderPath);
                saveButton.Enabled = createdMap != null;
                uploadButton.Enabled = createdMap != null && Directory.Exists(folderPath+"\\..\\"+version);
            }

            //OFF
            else {
                openDirButton.Enabled = false;
                createPatchMapButton.Enabled = false;
                saveButton.Enabled = false;
                uploadButton.Enabled = false;
            }
        }

        private void menu_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            switch (e.ClickedItem.Name) {
                case "openDirButton":
                    OpenDirButtonClicked();
                    break;
                case "createPatchMapButton":
                    CreatePatchMapClicked();
                    break;
                case "saveButton":
                    SaveButtonClicked();
                    break;
                case "uploadButton":
                    UploadClicked();
                    break;
                default:
                    Debug.WriteLine($"Unhandled menü item clicked:{e.ClickedItem.Name}");
                    break;
            }
        }

        void UploadClicked() {
            ActivateButtons(false);
            progressBar.Visible = true;
            progressBar.Value = 0;

            PatchSystem.UploadDirectory($"{folderPath}\\..\\{version}", Configs.GetData("keyFilePath"), Configs.GetData("driveDir"), (message)=> {
                textBox.AppendText($"\r\n{message}");
            }, (progress)=> {
                progressBar.Value = progress;
            });

            ActivateButtons(true);
            progressBar.Visible = false;
        }

        void SaveButtonClicked() {
            ActivateButtons(false);
            textBox.AppendText($"{Environment.NewLine}{Environment.NewLine}Createing new version:{(string)createdMap.version}");
            progressBar.Visible = true;
            progressBar.Value = 0;

            string dirName = $"{folderPath}\\..\\";

            PatchSystem.SaveToDirectory(folderPath, dirName, createdMap, (message) =>
            {
                textBox.AppendText($"\r\n{message}");
            }, (progress)=> {
                progressBar.Value = progress;
            });

            progressBar.Visible = false;

            ActivateButtons(true);
        }

        void CreatePatchMapClicked() {
            ActivateButtons(false);
            VersionNameForm versionBox = new VersionNameForm();
            
            versionBox.ShowDialog(this);
            version = versionBox.version;

            textBox.AppendText($"{Environment.NewLine}Create version:{version}");

            createdMap = HashMap.CreateFromDir(versionBox.version,folderPath, (path,hash)=> {
                textBox.AppendText($"{Environment.NewLine}{hash} - {path}");
            });

            textBox.AppendText($"{Environment.NewLine}Done!");
            ActivateButtons(true);
        }

        void OpenDirButtonClicked() {
            folderBrowserDialoge.SelectedPath = folderPath;
            DialogResult result = folderBrowserDialoge.ShowDialog();
            switch (result) {
                case DialogResult.OK:
                    folderPath = folderBrowserDialoge.SelectedPath;
                    textBox.AppendText($"{Environment.NewLine}Folder Selected:{folderPath}");
                    ActivateButtons(true);
                    break;
                default:
                    break;
            }
            Debug.WriteLine($"Folder selected:{folderBrowserDialoge.SelectedPath}");
        }
    }
}
