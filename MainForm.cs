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
        ImageList imageList = new ImageList();
        int sequenceid = 0;
        bool finalized = true;
        public MainForm()
        {

            imageList.ImageSize = new Size(48, 48);
            imageList.ColorDepth = ColorDepth.Depth32Bit;

            SteamClient.Init(480);

            webSocket.Options.SetRequestHeader("Origin", "https://steamcommunity.com");
            webSocket.ConnectAsync(new Uri("ws://127.0.0.1:27060/clientsocket/"), CancellationToken.None);
            InitializeComponent();
        }

        async void LoadAvatarForFriend(Steamworks.Friend friend)
        {
            var img_ = await friend.GetMediumAvatarAsync();
            if (img_ != null)
            {
                Steamworks.Data.Image img = (Steamworks.Data.Image)img_;
                int bufferSize = (int)(img.Width * img.Height * 4);
                byte[] avatarARGB = new byte[bufferSize];

                for (int j = 0; j < bufferSize; j += 4)
                {
                    avatarARGB[j + 0] = img.Data[j + 2];
                    avatarARGB[j + 1] = img.Data[j + 1];
                    avatarARGB[j + 2] = img.Data[j + 0];
                }

                Bitmap avatar = new Bitmap((int)img.Width, (int)img.Height, PixelFormat.Format32bppRgb);
                Rectangle BoundsRect = new Rectangle(0, 0, (int)img.Width, (int)img.Height);
                BitmapData bmpData = avatar.LockBits(BoundsRect, ImageLockMode.WriteOnly, avatar.PixelFormat);

                IntPtr ptr = bmpData.Scan0;
                Marshal.Copy(avatarARGB, 0, ptr, (int)img.Width * (int)img.Height * 4);
                avatar.UnlockBits(bmpData);
                imageList.Images.Add(friend.Id.ToString(), avatar);
            }

        }


        void LoadFriends()
        {

            listView1.LargeImageList = imageList;
            listView1.Items.Clear();
            steamIdList.Clear();

            var friends = SteamFriends.GetFriends();

            foreach (var friend in friends)
            {
                string personaName = friend.Name;
                var personaState = friend.State;
                //int avatarId = friend.GetLargeAvatarAsync();
                // FriendGameInfo gameInfo = friend.GameInfo;

                if (!imageList.Images.ContainsKey(friend.Id.ToString()))
                {
                    LoadAvatarForFriend(friend);
                }
                listView1.Items.Add(new ListViewItem(personaName, friend.Id.ToString()));


                /*
                if (avatarId != -1)
                {
                   
                }
                else
                {
                    listView1.Items.Add(new ListViewItem(personaName));
                }
                if (gameInfo.GameID == 730) {
                   
                    steamIdList.Add(friend.Id.ToString());
                }*/
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
            await webSocket.SendAsync(new ArraySegment<byte>(b), WebSocketMessageType.Text, true, CancellationToken.None);
            var buffer = new byte[1024];
            var resultStr = "";
            WebSocketReceiveResult result;

            do
            {
                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
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
