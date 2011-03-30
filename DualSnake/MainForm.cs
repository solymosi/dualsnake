using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Solymosi.Networking.Sockets;
namespace DualSnake
{
    public partial class MainForm : Form
    {
        private Client Server;
        private List<Point> SnakeOne = new List<Point>();
        private List<Point> SnakeTwo = new List<Point>();
        private List<Point> Food = new List<Point>();
        private List<Point> Turbo = new List<Point>();
        private Timer UpdateTimer = new Timer();
        int TurboCounter = 0;
        bool TurboEnabled = false;
        bool GameOver = false;
        int Me = 0;
        string Status = "";

        public MainForm()
        {
            InitializeComponent();
        }
        private void MainForm_Load(object sender, EventArgs e)
        {
            this.ClientSize = new Size(500, 535);
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            UpdateTimer.Interval = 5;
            UpdateTimer.Tick += new EventHandler(delegate { this.Invoke((MethodInvoker)delegate { this.Refresh(); }); });
            UpdateTimer.Start();

            Server = new Client();
            Status = "Connecting to server...";
            Server.Connect("10.111.111.221", 1991);
            Server.Connected += new Client.ConnectDelegate(delegate
            {
                Status = "Connected to server. Waiting for an opponent to join...";
                Server.Received += new Client.ReceiveDelegate(Server_Received);
            });
            Server.Closed += new Client.CloseDelegate(delegate(object o, Client.CloseEventArgs ea)
            {
                if (ea.Type == Client.CloseType.Dropped && !GameOver) { Status = "The server has dropped the connection :("; }
            });
        }

        void Server_Received(object sender, Client.TransmitEventArgs e)
        {
            if (e.Text == "#First")
            {
                Me = 1;
                Status = "Connected to server. Waiting for an opponent to join...";
            }

            if (e.Text == "#Second")
            {
                Me = 2;
                Status = "Connected to server. Game will start now...";
            }

            if (e.Text.StartsWith("#Countdown "))
            {
                Status = "The game will start in " + e.Text.Substring(11) + " seconds...";
            }

            if (e.Text == "#Draw")
            {
                GameOver = true;
                Status = "Game over! It's a DRAW! :S";
            }

            if (e.Text.StartsWith("#Winner "))
            {
                GameOver = true;
                Status = "Game over! You " + (int.Parse(e.Text.Substring(8)) == Me ? "WON :)" : "LOST :(");
            }

            if (e.Text.StartsWith("#Status "))
            {
                string[] pqq = e.Text.Substring(8).Split('\t');
                string fud = pqq[0];
                string t = pqq[1];
                string s1 = pqq[2];
                string s2 = pqq[3];
                TurboEnabled = pqq[4] == "E";
                TurboCounter = int.Parse(pqq[5]);
                Food.Clear();
                string[] Foods = fud.Split(new string[] { ";" }, StringSplitOptions.None);
                foreach (string FD in Foods)
                {
                    string[] Parts = FD.Split(new string[] { "," }, StringSplitOptions.None);
                    Food.Add(new Point(int.Parse(Parts[0]), int.Parse(Parts[1])));
                }
                Turbo.Clear();
                string[] Turbos = t.Split(new string[] { ";" }, StringSplitOptions.None);
                foreach (string FD in Turbos)
                {
                    string[] Parts = FD.Split(new string[] { "," }, StringSplitOptions.None);
                    Turbo.Add(new Point(int.Parse(Parts[0]), int.Parse(Parts[1])));
                }
                SnakeOne.Clear();
                string[] Points = s1.Split(new string[] { ";" }, StringSplitOptions.None);
                foreach (string P in Points)
                {
                    string[] Parts = P.Split(new string[] { "," }, StringSplitOptions.None);
                    SnakeOne.Add(new Point(int.Parse(Parts[0]), int.Parse(Parts[1])));
                }
                SnakeTwo.Clear();
                string[] pts = s2.Split(new string[] { ";" }, StringSplitOptions.None);
                foreach (string Q in pts)
                {
                    string[] Parts = Q.Split(new string[] { "," }, StringSplitOptions.None);
                    SnakeTwo.Add(new Point(int.Parse(Parts[0]), int.Parse(Parts[1])));
                }
                this.Status = "TURBO: " + TurboCounter.ToString() + "     Press and hold SPACE to activate";
                this.Invoke((MethodInvoker)delegate { this.Refresh(); });
            }
        }

        private void MainForm_Paint(object sender, PaintEventArgs e)
        {
            Bitmap Temp = new Bitmap(500, 500, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            Graphics GFX = Graphics.FromImage(Temp);
            GFX.Clear(Color.FromArgb(50, 50, 50));
            for (int i = 0; i < Food.Count; i++)
            {
                GFX.FillRectangle(Brushes.Orange, new Rectangle(10 * (Food[i].Y - 1) + 2, 10 * (Food[i].X - 1) + 2, 10, 10));
            }
            for (int i = 0; i < Turbo.Count; i++)
            {
                GFX.FillRectangle(Brushes.Magenta, new Rectangle(10 * (Turbo[i].Y - 1) + 2, 10 * (Turbo[i].X - 1) + 2, 10, 10));
            }
            for (int i = 0; i < SnakeOne.Count; i++)
            {
                GFX.FillRectangle(Me == 1 ? Brushes.LightGreen : Brushes.LightBlue, new Rectangle(10 * (SnakeOne[i].Y - 1) + 2, 10 * (SnakeOne[i].X - 1) + 2, 10, 10));
            }
            for (int i = 0; i < SnakeTwo.Count; i++)
            {
                bool Yellow = false;
                foreach (Point p in SnakeOne)
                {
                    if (p.X == SnakeTwo[i].X && p.Y == SnakeTwo[i].Y) { Yellow = true; }
                }
                GFX.FillRectangle(Yellow ? Brushes.Yellow : (Me == 2 ? Brushes.LightGreen : Brushes.LightBlue), new Rectangle(10 * (SnakeTwo[i].Y - 1) + 2, 10 * (SnakeTwo[i].X - 1) + 2, 10, 10));
            }
            e.Graphics.DrawImage(Temp, 0, 0, Temp.Width, Temp.Height);
            e.Graphics.DrawString(Status, new Font(new FontFamily("trebuchet ms"), 8, FontStyle.Bold), Brushes.White, new PointF(10, 510));
        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                this.Close();
                return;
            }
            if (Server.IsConnected)
            {
                switch (e.KeyCode)
                {
                    case Keys.Up:
                        Server.Send("#D up");
                        break;
                    case Keys.Down:
                        Server.Send("#D down");
                        break;
                    case Keys.Left:
                        Server.Send("#D left");
                        break;
                    case Keys.Right:
                        Server.Send("#D right");
                        break;
                    case Keys.Space:
                        Server.Send("#Turbo on");
                        break;
                    case Keys.D0:
                        if (e.Alt) { Server.Send("#MaxTurbo"); }
                        break;
                }
            }
        }

        private void MainForm_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space && Server.IsConnected)
            {
                Server.Send("#Turbo off");
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            UpdateTimer.Stop();
            Server.Abort();
        }
    }
    public class Point
    {
        public int X = 0;
        public int Y = 0;
        public Point(int x, int y)
        {
            X = x;
            Y = y;
        }
    }
}