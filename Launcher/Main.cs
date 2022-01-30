using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Patcher
{
    public partial class Main : Form
    {
        Version localVersion;
        Version onlineVersion;

        public Main()
        {
            InitializeComponent();
            Init();
        }

        void Init() {
            playButton.Enabled = false;
            progressBar.Visible = false;
            titleLabel.Text = Configs.GetData("appName");
            this.Text = titleLabel.Text + " Launcher";

            localVersion = PatchSystem.GetLocalVersion();
            versionLabel.Text = localVersion;

            //Try get online version
            progressLabel.Text = $"Try Get Online Version";

            try
            {
                Version[] onlineVersions = PatchSystem.CheckOnlineVersions(Configs.GetData("driveDir"));
                if (onlineVersions.Length > 0)
                {
                    onlineVersion = onlineVersions[onlineVersions.Length - 1];
                    if (onlineVersion > localVersion)
                    {
                        progressLabel.Text = $"Version {(string)localVersion} is outdated. Update to {(string)onlineVersion}!";
                        playButton.Text = "Update";
                        playButton.Enabled = true;
                        playButton.MouseClick += OnUpdateClicked;
                    }
                    else {
                        progressLabel.Text = "Game is up to date!";
                        playButton.Enabled = true;
                        playButton.MouseClick += OnPlayClicked;
                    }
                }
                else {
                    progressLabel.Text = "There is no online version.";
                    if (localVersion != "v0") {
                        playButton.Enabled = true;
                        playButton.MouseClick += OnPlayClicked;
                    }
                    return;
                }
            }
            catch (System.Exception e) {
                System.Diagnostics.Debug.WriteLine(e.Message);
                progressLabel.Text = e.Message;

                if (localVersion != "v0") {
                    playButton.Enabled = true;
                    playButton.MouseClick += OnPlayClicked;
                }
                return;
            }
        }

        void OnUpdateClicked(object sender, MouseEventArgs e) {
            playButton.Enabled = false;
            playButton.MouseClick -= OnUpdateClicked;
            progressBar.Visible = true;

            PatchSystem.UpdateVersionTo(onlineVersion, (message)=> {
                progressLabel.Text = message;
            }, (progress)=> {
                progressBar.Value = progress;
            });

            progressBar.Visible = false;
            playButton.Text = "Play";
            playButton.MouseClick += OnPlayClicked;
            playButton.Enabled = true;
        }

        private void OnPlayClicked(object sender, MouseEventArgs e)
        {
            System.IO.FileInfo startFile = new System.IO.FileInfo(PatchSystem.GAME_DIR+"\\"+Configs.GetData("startPath"));
            if (startFile.Exists)
            {
                System.Diagnostics.Process.Start(startFile.FullName);
                Application.Exit();
            }
            else {
                progressLabel.Text = $"Start file not found! {startFile.FullName}";
            }
        }
    }
}
