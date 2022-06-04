using System;
using System.Collections.Generic;
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

        List<string> steamIdList = new List<string>();
        bool isJoining = false;
        string targetSteamId = "";
        ClientWebSocket webSocket = new ClientWebSocket();
        int sequenceid = 0;
        bool finalized = true;
        public MainForm()
        {

            Environment.SetEnvironmentVariable("SteamAppId", "480");
            Environment.SetEnvironmentVariable("SteamOverlayGameId", "480");
            Environment.SetEnvironmentVariable("SteamGameId", "480");
            SteamAPI.Init();
            webSocket.Options.SetRequestHeader("Origin", "https://steamcommunity.com");
            webSocket.ConnectAsync(new Uri("ws://127.0.0.1:27060/clientsocket/"), CancellationToken.None);
            InitializeComponent();
        }
        void LoadFriends()
        {
            ImageList imageList = new ImageList();
            imageList.ImageSize = new Size(48, 48);
            imageList.ColorDepth = ColorDepth.Depth32Bit;

            listView1.LargeImageList = imageList;
            listView1.Items.Clear();
            steamIdList.Clear();

            var friendCount = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);

            for (int idx = 0; idx < friendCount; idx++)
            {
                var m_Friend = SteamFriends.GetFriendByIndex(idx, EFriendFlags.k_EFriendFlagImmediate);
                string personaName = SteamFriends.GetFriendPersonaName(m_Friend);
                EPersonaState personaState = SteamFriends.GetFriendPersonaState(m_Friend);
                int avatarId = SteamFriends.GetLargeFriendAvatar(m_Friend);
                SteamFriends.GetFriendGamePlayed(m_Friend, out FriendGameInfo_t gameInfo);
                if (gameInfo.m_gameID.m_GameID == 730) {
                    if (avatarId != -1)
                    {
                        SteamUtils.GetImageSize(avatarId, out uint avatarW, out uint avatarH);
                        int bufferSize = (int)(avatarW * avatarH * 4);
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

                        // var personaState = SteamFriends.GetFriendRichPresence(m_Friend, "status");
                        Console.WriteLine(personaState);
                        listView1.Items.Add(new ListViewItem(personaName, m_Friend.ToString()));
                    }
                    else
                    {
                        listView1.Items.Add(new ListViewItem(personaName));
                    }
                    steamIdList.Add(m_Friend.ToString());
                }
            }
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            LoadFriends();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (isJoining)
            {
                isJoining = false;
                button1.Text = "Start";
                progressBar.Style = ProgressBarStyle.Blocks;
            }
            else
            {
                isJoining = true;
                button1.Text = "Stop";
                progressBar.Style = ProgressBarStyle.Marquee;
            }
        }

        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listView1.SelectedIndices.Count > 0)
            {
                targetSteamId = steamIdList[listView1.SelectedIndices[0]];
                toolStripStatusLabel.Text = "SteamID: " + targetSteamId;
            }
        }

        private async void timer1_Tick(object sender, EventArgs e)
        {
            if (!isJoining || !finalized) return;
            finalized = false;
            sequenceid += 1;
            string s = String.Format("{{\"friend_id\":\"{0}\",\"sequenceid\":{1},\"universe\":1,\"message\":\"ShowJoinGameDialog\"}}", targetSteamId, sequenceid);
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

            toolStripStatusLabel.Text = resultStr;
            finalized = true;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            LoadFriends();
        }
    }
}
