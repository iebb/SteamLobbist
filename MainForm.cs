using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Steamworks;


namespace SteamLobbist
{

    public partial class MainForm : Form
    {

        List<CSteamID> steamIdList = new List<CSteamID>();
        List<string> lobbyIdList = new List<string>();
        bool isJoining = false;
        CSteamID targetSteamId = new CSteamID();
        string targetLobbyId = "";
        ClientWebSocket webSocket = new ClientWebSocket();
        int sequenceid = 0;
        bool sameGameMode = true;
        bool finalized = true;
        List<int> intervals = new List<int> { 50, 100, 200, 333, 500, 750, 1000, 2000, 3000, 5000 };
        public MainForm()
        {
            InitializeComponent();

            Process[] pname = Process.GetProcessesByName("csgo");

            if (pname.Length == 0)
            {
                var result = MessageBox.Show(
                    "请先启动游戏以获得最佳体验\n是否要尝试启动游戏?",
                    "未检测到游戏启动",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Exclamation
                );

                if (result == DialogResult.Yes)
                {
                    while (pname.Length == 0)
                    {
                        var psi = new ProcessStartInfo();
                        psi.UseShellExecute = true;
                        psi.FileName = "steam://rungameid/730";
                        Process.Start(psi);
                        MessageBox.Show(
                            "请等待游戏启动完成再确认",
                            "游戏启动中",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Exclamation
                        );
                        pname = Process.GetProcessesByName("csgo");
                    }
                    Environment.SetEnvironmentVariable("SteamAppId", "730");
                }
                else
                {
                    Environment.SetEnvironmentVariable("SteamAppId", "740");
                    sameGameMode = false;
                }
            }

            //Environment.SetEnvironmentVariable("SteamOverlayGameId", "730");
            //Environment.SetEnvironmentVariable("SteamGameId", "730");
            SteamAPI.Init();

            webSocket.Options.SetRequestHeader("Origin", "https://steamcommunity.com");
            webSocket.ConnectAsync(new Uri("ws://127.0.0.1:27060/clientsocket/"), CancellationToken.None);

            foreach (int interval in intervals)
            {
                var item = new ToolStripMenuItem();
                item.Text = interval.ToString() + "ms";
                item.Click += (_, _) => {
                    timer1.Interval = interval;
                    speedSelector.Text = "频率: " + interval.ToString() + "ms";
                };
                speedSelector.DropDownItems.Add(item);
            }

        }
        void LoadFriends()
        {
            ImageList imageList = new ImageList();
            imageList.ImageSize = new Size(48, 48);
            imageList.ColorDepth = ColorDepth.Depth32Bit;

            listView1.LargeImageList = imageList;
            listView1.Items.Clear();
            steamIdList.Clear();
            lobbyIdList.Clear();

            var friendCount = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);

            for (int idx = 0; idx < friendCount; idx++)
            {
                var m_Friend = SteamFriends.GetFriendByIndex(idx, EFriendFlags.k_EFriendFlagImmediate);
                string personaName = SteamFriends.GetFriendPersonaName(m_Friend);
                // EPersonaState personaState = SteamFriends.GetFriendPersonaState(m_Friend);
                int avatarId = SteamFriends.GetLargeFriendAvatar(m_Friend);
                SteamFriends.GetFriendGamePlayed(m_Friend, out FriendGameInfo_t gameInfo);
                if (gameInfo.m_gameID.m_GameID == 730)
                {
                    if (avatarId != -1)
                    {
                        if (!imageList.Images.ContainsKey(m_Friend.ToString()))
                        {
                            SteamUtils.GetImageSize(avatarId, out uint avatarW, out uint avatarH);
                            int bufferSize = (int)(avatarW * avatarH * 4);
                            if (bufferSize > 0)
                            {
                                byte[] avatarARGB = new byte[bufferSize];
                                SteamUtils.GetImageRGBA(avatarId, avatarARGB, bufferSize);

                                for (int j = 0; j < bufferSize; j += 4)
                                {
                                    avatarARGB[j + 0] ^= avatarARGB[j + 2];
                                    avatarARGB[j + 2] ^= avatarARGB[j + 0];
                                    avatarARGB[j + 0] ^= avatarARGB[j + 2];
                                }

                                Bitmap avatar = new Bitmap((int)avatarW, (int)avatarH, PixelFormat.Format32bppRgb);
                                Rectangle BoundsRect = new Rectangle(0, 0, (int)avatarW, (int)avatarH);
                                BitmapData bmpData = avatar.LockBits(BoundsRect, ImageLockMode.WriteOnly, avatar.PixelFormat);

                                IntPtr ptr = bmpData.Scan0;
                                Marshal.Copy(avatarARGB, 0, ptr, (int)avatarW * (int)avatarH * 4);
                                avatar.UnlockBits(bmpData);
                                imageList.Images.Add(m_Friend.ToString(), avatar);
                            }
                        }
                        // var personaState = SteamFriends.GetFriendRichPresence(m_Friend, "status");
                    }
                    listView1.Items.Add(new ListViewItem(personaName, m_Friend.ToString()));

                    var richPresence = SteamFriends.GetFriendRichPresence(m_Friend, "steam_player_group");
                    steamIdList.Add(m_Friend);
                    lobbyIdList.Add(richPresence);
                }
            }
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            LoadFriends();
        }

        void StopJoin()
        {
            joinButton.Text = "开跟!";
            progressBar.Style = ProgressBarStyle.Blocks;
            isJoining = false;
        }
        void StartJoin()
        {
            joinButton.Text = "停止";
            progressBar.Style = ProgressBarStyle.Marquee;
            isJoining = true;
        }

        private void joinButton_Click(object sender, EventArgs e)
        {
            if (isJoining)
            {
                StopJoin();
            }
            else
            {
                StartJoin();
            }
        }

        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listView1.SelectedIndices.Count > 0)
            {
                var idx = listView1.SelectedIndices[0];
                targetSteamId = steamIdList[idx];
                targetLobbyId = lobbyIdList[idx];
                toolStripStatusLabel.Text = "SteamID: " + targetSteamId + " 房间: " + targetLobbyId;
            }
        }

        private async void timer1_Tick(object sender, EventArgs e)
        {
            if (!isJoining || !finalized) return;
            finalized = false;
            var target_player_group = SteamFriends.GetFriendRichPresence(targetSteamId, "steam_player_group");
            if (target_player_group != "" || !sameGameMode)
            {
                sequenceid += 1;

                if (sameGameMode)
                {
                    var current_player_group = SteamFriends.GetFriendRichPresence(SteamUser.GetSteamID(), "steam_player_group");
                    if (current_player_group == target_player_group)
                    {
                        var isWatching = SteamFriends.GetFriendRichPresence(SteamUser.GetSteamID(), "game:act");
                        if (isWatching != "watch")
                        {
                            StopJoin();
                        }
                        finalized = true;
                        return;
                    }
                    toolStripStatusLabel.Text = "房间: " + target_player_group + " / 当前: " + current_player_group; // resultStr;
                }
                else
                {
                    toolStripStatusLabel.Text = "检测房间功能未开启，请手动中止";
                }


                string s = String.Format("{{\"friend_id\":\"{0}\",\"sequenceid\":{1},\"universe\":1,\"message\":\"ShowJoinGameDialog\"}}", targetSteamId.ToString(), sequenceid);
                byte[] b = Encoding.ASCII.GetBytes(s);
                await webSocket.SendAsync(b, WebSocketMessageType.Text, true, CancellationToken.None);
                var buffer = new byte[1024];
                var resultStr = "";
                WebSocketReceiveResult result;
                do
                {
                    result = await webSocket.ReceiveAsync(buffer, CancellationToken.None);
                    resultStr += Encoding.ASCII.GetString(buffer, 0, result.Count).Replace("\n", "");
                }
                while (!result.EndOfMessage);

                finalized = true;
                return;
            }
            StopJoin();
            finalized = true;
        }

        private void refreshButton_Click(object sender, EventArgs e)
        {
            LoadFriends();
        }
    }
}
