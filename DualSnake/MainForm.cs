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

        int TurboCounter = 0;
        int Me = 0;

        bool TurboEnabled = false;
        bool GameOver = false;

        const int BlockWidth = 70;
        const int BlockHeight = 40;
        const int BlockDisplaySize = 10;
        
        string Status = "";

        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            this.ClientSize = new Size(BlockWidth * BlockDisplaySize, BlockHeight * BlockDisplaySize + 35);
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
 
            Server = new Client();
            SetStatus("Connecting to server...");
            Server.Connect("10.111.111.221", 1991);
            Server.Received += new Client.ReceiveDelegate(Server_Received);
            Server.Connected += new Client.ConnectDelegate(delegate
            {
                SetStatus("Connected to server. Waiting for an opponent to join...");
            });
            Server.Closed += new Client.CloseDelegate(delegate(object o, Client.CloseEventArgs ea)
            {
                if (ea.Type == Client.CloseType.Dropped && !GameOver) { SetStatus("The server has dropped the connection :("); }
            });
        }

        private void SetStatus(string Message)
        {
            Status = Message;
            RePaint();
        }

        private void RePaint()
        {
            if (this.InvokeRequired) { this.Invoke((MethodInvoker)delegate { this.RePaint(); }); return; }
            this.Refresh();
        }

        void Server_Received(object sender, Client.TransmitEventArgs e)
        {
            if (e.Text == "#First")
            {
                Me = 1;
                SetStatus("Connected to server. Waiting for an opponent to join...");
            }

            if (e.Text == "#Second")
            {
                Me = 2;
                SetStatus("Connected to server. Game will start now...");
            }

            if (e.Text.StartsWith("#Countdown "))
            {
                SetStatus("The game will start in " + e.Text.Substring(11) + " seconds...");
            }

            if (e.Text == "#Draw")
            {
                GameOver = true;
                SetStatus("Game over! It's a DRAW! :S");
            }

            if (e.Text.StartsWith("#Winner "))
            {
                GameOver = true;
                SetStatus("Game over! You " + (int.Parse(e.Text.Substring(8)) == Me ? "WON :)" : "LOST :("));
            }

            if (e.Text.StartsWith("#Status "))
            {
                string[] pqq = e.Text.Substring(8).Split('\t');
                Food = FromRepresentation(pqq[0]);
                Turbo = FromRepresentation(pqq[1]);
                SnakeOne = FromRepresentation(pqq[2]);
                SnakeTwo = FromRepresentation(pqq[3]);
                TurboEnabled = pqq[4] == "E";
                TurboCounter = int.Parse(pqq[5]);

                SetStatus("TURBO: " + TurboCounter.ToString() + "     Press and hold SPACE to activate");
                RePaint();
            }
        }

        private List<Point> FromRepresentation(string Input)
        {
            List<Point> list = new List<Point>();
            char[] chars = Input.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (i % 2 == 0) { list.Add(new Point(((int)chars[i]) - 20, ((int)chars[i + 1]) - 20)); }
            }
            return list;
        }

        private void MainForm_Paint(object sender, PaintEventArgs e)
        {
            Bitmap Temp = new Bitmap(BlockWidth * BlockDisplaySize, BlockHeight * BlockDisplaySize, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            Graphics GFX = Graphics.FromImage(Temp);
            GFX.Clear(Color.FromArgb(50, 50, 50));
            for (int i = 0; i < Food.Count; i++)
            {
                GFX.FillRectangle(Brushes.Orange, new Rectangle(BlockDisplaySize * (Food[i].X - 1), BlockDisplaySize * (Food[i].Y - 1), BlockDisplaySize, BlockDisplaySize));
            }
            for (int i = 0; i < Turbo.Count; i++)
            {
                GFX.FillRectangle(Brushes.Magenta, new Rectangle(BlockDisplaySize * (Turbo[i].X - 1), BlockDisplaySize * (Turbo[i].Y - 1), BlockDisplaySize, BlockDisplaySize));
            }
            for (int i = 0; i < SnakeOne.Count; i++)
            {
                GFX.FillRectangle(Me == 1 ? Brushes.LightGreen : Brushes.LightBlue, new Rectangle(BlockDisplaySize * (SnakeOne[i].X - 1), BlockDisplaySize * (SnakeOne[i].Y - 1), BlockDisplaySize, BlockDisplaySize));
            }
            for (int i = 0; i < SnakeTwo.Count; i++)
            {
                bool Yellow = false;
                foreach (Point p in SnakeOne)
                {
                    if (p.X == SnakeTwo[i].X && p.Y == SnakeTwo[i].Y) { Yellow = true; }
                }
                GFX.FillRectangle(Yellow ? Brushes.Yellow : (Me == 2 ? Brushes.LightGreen : Brushes.LightBlue), new Rectangle(BlockDisplaySize * (SnakeTwo[i].X - 1), BlockDisplaySize * (SnakeTwo[i].Y - 1), BlockDisplaySize, BlockDisplaySize));
            }
            e.Graphics.DrawImage(Temp, 0, 0, Temp.Width, Temp.Height);
            e.Graphics.DrawString(Status, new Font(new FontFamily("trebuchet ms"), 8, FontStyle.Bold), Brushes.White, new PointF(10, BlockHeight * BlockDisplaySize + 10));
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
                        Server.Send("#D U");
                        break;
                    case Keys.Down:
                        Server.Send("#D D");
                        break;
                    case Keys.Left:
                        Server.Send("#D L");
                        break;
                    case Keys.Right:
                        Server.Send("#D R");
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