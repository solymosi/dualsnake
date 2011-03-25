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
        bool TurboEnabled = false;

        public MainForm()
        {
            InitializeComponent();
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            this.ClientSize = new Size(500, 500);
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Server = new Client();
            Server.Connect("localhost", 1991);
            Server.Connected += new Client.ConnectDelegate(delegate
            {
                Server.Send("NAME valaki");
                Server.Received += new Client.ReceiveDelegate(Server_Received);
            });
        }

        void Server_Received(object sender, Client.TransmitEventArgs e)
        {
            if (e.Text.StartsWith("FOOD "))
            {
                Food.Clear();
                string[] Foods = e.Text.Substring(5).Split(new string[] { ";" }, StringSplitOptions.None);
                foreach (string FD in Foods)
                {
                    string[] Parts = FD.Split(new string[] { "," }, StringSplitOptions.None);
                    Food.Add(new Point(int.Parse(Parts[0]), int.Parse(Parts[1])));
                }
            }

            if (e.Text.StartsWith("TURBO "))
            {
                Turbo.Clear();
                string[] Turbos = e.Text.Substring(5).Split(new string[] { ";" }, StringSplitOptions.None);
                foreach (string FD in Turbos)
                {
                    string[] Parts = FD.Split(new string[] { "," }, StringSplitOptions.None);
                    Turbo.Add(new Point(int.Parse(Parts[0]), int.Parse(Parts[1])));
                }
            }

            if (e.Text.StartsWith("SNAKE ONE "))
            {
                SnakeOne.Clear();
                string[] Points = e.Text.Substring(10).Split(new string[] { ";" }, StringSplitOptions.None);
                foreach (string P in Points)
                {
                    string[] Parts = P.Split(new string[] { "," }, StringSplitOptions.None);
                    SnakeOne.Add(new Point(int.Parse(Parts[0]), int.Parse(Parts[1])));
                }
            }

            if (e.Text.StartsWith("SNAKE TWO "))
            {
                SnakeTwo.Clear();
                string[] Points = e.Text.Substring(10).Split(new string[] { ";" }, StringSplitOptions.None);
                foreach (string P in Points)
                {
                    string[] Parts = P.Split(new string[] { "," }, StringSplitOptions.None);
                    SnakeTwo.Add(new Point(int.Parse(Parts[0]), int.Parse(Parts[1])));
                }
            }

            if (e.Text.StartsWith("TRB "))
            {
                TurboEnabled = (e.Text.Substring(4) == "ENABLED");
            }
            if (e.Text.StartsWith("MY TURBO "))
            {
                TurboCounter = int.Parse(e.Text.Substring(9));
            }

            this.Invoke((MethodInvoker)delegate { this.Refresh(); });
        }

        private void Form1_Paint(object sender, PaintEventArgs e)
        {
            Bitmap Temp = new Bitmap(this.Width, this.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            Graphics GFX = Graphics.FromImage(Temp);
            GFX.Clear(Color.FromArgb(50, 50, 50));
            GFX.DrawRectangle(new Pen(Brushes.White, 4), new Rectangle(0, 0, this.Width, this.Height));
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
                GFX.FillRectangle(Brushes.LightGreen, new Rectangle(10 * (SnakeOne[i].Y - 1) + 2, 10 * (SnakeOne[i].X - 1) + 2, 10, 10));
            }
            for (int i = 0; i < SnakeTwo.Count; i++)
            {
                bool Yellow = false;
                foreach (Point p in SnakeOne)
                {
                    if (p.X == SnakeTwo[i].X && p.Y == SnakeTwo[i].Y) { Yellow = true; }
                }
                GFX.FillRectangle(Yellow ? Brushes.Yellow : Brushes.LightBlue, new Rectangle(10 * (SnakeTwo[i].Y - 1) + 2, 10 * (SnakeTwo[i].X - 1) + 2, 10, 10));
            }
            e.Graphics.DrawImage(Temp, 0, 0, Temp.Width, Temp.Height);
            e.Graphics.DrawString("Turbo is " + (TurboEnabled ? "ACTIVE (press SPACE to deactivate)" : "inactive (press SPACE to activate)") + " - " + TurboCounter.ToString() + " units remaining.", new Font(new FontFamily("trebuchet ms"), 8, FontStyle.Bold), Brushes.Yellow, new PointF(10, this.Height - 20));
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                this.Close();
                return;
            }
            switch (e.KeyCode)
            {
                case Keys.Up:
                    Server.Send("DIRECTION UP");
                    break;
                case Keys.Down:
                    Server.Send("DIRECTION DOWN");
                    break;
                case Keys.Left:
                    Server.Send("DIRECTION LEFT");
                    break;
                case Keys.Right:
                    Server.Send("DIRECTION RIGHT");
                    break;
            }
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