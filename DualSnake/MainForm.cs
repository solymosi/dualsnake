using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Solymosi.Networking.Sockets;
using Microsoft.Win32;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using System.Net.Sockets;
using DualSnake.Properties;

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

        Color ErrorColor = Color.FromArgb(150, 128, 0, 0);
        Color WarningColor = Color.FromArgb(150, 128, 64, 0);
        Color SuccessColor = Color.FromArgb(150, 0, 128, 0);

        bool Draw = false;
        
        string ConnectTo = "";

        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            this.ClientSize = new Size(BlockWidth * BlockDisplaySize, BlockHeight * BlockDisplaySize + 60);
            this.Left = Screen.PrimaryScreen.WorkingArea.Width / 2 - this.Width / 2;
            this.Top = Screen.PrimaryScreen.WorkingArea.Height / 2 - this.Height / 2;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            AgainPanel.Visible = false;
            AgainPanel.Top = this.ClientRectangle.Height - AgainPanel.Height;
            AgainPanel.Left = 0;
            AgainPanel.Width = this.Width;
            AgainButton.Enabled = false;
            AgainButton.Left = this.Width / 2 - AgainButton.Width / 2;
            StatusLabel.Left = 0;
            StatusLabel.Width = this.Width;
            StatusLabel.Top = BlockHeight * BlockDisplaySize / 2 - StatusLabel.Height / 2;
            ConnectTo = (string)Registry.GetValue(@"HKEY_CURRENT_USER\Software\Solymosi\DualSnake", "LastIP", "");
            PopupConnectionDialog();
        }

        private void Connect()
        {
            AgainPanel.Visible = false;
            AgainButton.Enabled = false;
            Draw = false;
            Server = new Client();
            Server.Socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);
            SetStatus("Connecting to " + ConnectTo + "...");
            Server.Connect(ConnectTo, 1991);
            Server.Received += new Client.ReceiveDelegate(Server_Received);
            Server.Connected += new Client.ConnectDelegate(delegate
            {
                this.Invoke((MethodInvoker)delegate { AgainPanel.Visible = false; AgainButton.Enabled = false; });
            });
            Server.Closed += new Client.CloseDelegate(delegate(object o, Client.CloseEventArgs ea)
            {
                if (!GameOver) { SetStatus("Connection failed", ErrorColor); }
                this.Invoke((MethodInvoker)delegate { AgainPanel.Visible = true; AgainButton.Enabled = true; });
            });
        }

        private void PopupConnectionDialog()
        {
            ConnectForm CF = new ConnectForm();
            CF.ConnectTo.Text = ConnectTo;
            if (CF.ShowDialog() == DialogResult.OK)
            {
                ConnectTo = CF.ConnectTo.Text;
                Registry.SetValue(@"HKEY_CURRENT_USER\Software\Solymosi\DualSnake", "LastIP", ConnectTo);
                Connect();
            }
            else
            {
                this.Close();
            }
        }

        private void SetStatus(string Message) { SetStatus(Message, Color.Black); }
        private void SetStatus(string Message, Color Color)
        {
            if (this.InvokeRequired) { this.Invoke((MethodInvoker)delegate { this.SetStatus(Message, Color); }); return; }
            StatusLabel.Visible = true;
            StatusLabel.Text = Message;
            StatusLabel.BackColor = Color;
            this.Refresh();
        }

        private void ClearStatus()
        {
            if (this.InvokeRequired) { this.Invoke((MethodInvoker)delegate { this.ClearStatus(); }); return; }
            StatusLabel.Visible = false;
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
                SetStatus("Waiting for an opponent...");
            }

            if (e.Text == "#Second")
            {
                Me = 2;
            }

            if (e.Text.StartsWith("#Countdown "))
            {
                SetStatus("Starting game...", WarningColor);
            }

            if (e.Text == "#Draw")
            {
                GameOver = true;
                SetStatus("It's a DRAW!", WarningColor);
            }

            if (e.Text.StartsWith("#Winner "))
            {
                GameOver = true;
                if (int.Parse(e.Text.Substring(8)) == Me)
                {
                    SetStatus("You're the WINNER!", SuccessColor);
                }
                else
                {
                    SetStatus("You LOST!", ErrorColor);
                }
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
                ClearStatus();
                Draw = true;
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
            if (!Draw)
            {
                e.Graphics.Clear(Color.Black);
                return;
            }
            Graphics GFX = e.Graphics;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Low;
            e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.None;
            GFX.FillRectangle(new SolidBrush(Color.FromArgb(50, 50, 50)), new Rectangle(0, 0, BlockWidth * BlockDisplaySize, BlockHeight * BlockDisplaySize));
            for (int i = 0; i < Food.Count; i++)
            {
                GFX.FillRectangle(new TextureBrush(Resources.Food), new Rectangle(BlockDisplaySize * (Food[i].X - 1), BlockDisplaySize * (Food[i].Y - 1), BlockDisplaySize, BlockDisplaySize));
            }
            for (int i = 0; i < Turbo.Count; i++)
            {
                GFX.FillRectangle(new TextureBrush(Resources.Turbo), new Rectangle(BlockDisplaySize * (Turbo[i].X - 1), BlockDisplaySize * (Turbo[i].Y - 1), BlockDisplaySize, BlockDisplaySize));
            }
            for (int i = 0; i < SnakeOne.Count; i++)
            {
                GFX.FillRectangle(Me == 1 ? new TextureBrush(Resources.Me) : new TextureBrush(Resources.Them), new Rectangle(BlockDisplaySize * (SnakeOne[i].X - 1), BlockDisplaySize * (SnakeOne[i].Y - 1), BlockDisplaySize, BlockDisplaySize));
            }
            for (int i = 0; i < SnakeTwo.Count; i++)
            {
                bool Both = false;
                foreach (Point p in SnakeOne)
                {
                    if (p.X == SnakeTwo[i].X && p.Y == SnakeTwo[i].Y) { Both = true; }
                }
                GFX.FillRectangle(Both ? new TextureBrush(Resources.Both) : (Me == 2 ? new TextureBrush(Resources.Me) : new TextureBrush(Resources.Them)), new Rectangle(BlockDisplaySize * (SnakeTwo[i].X - 1), BlockDisplaySize * (SnakeTwo[i].Y - 1), BlockDisplaySize, BlockDisplaySize));
            }

            GFX.DrawImage(Resources.TurboIcon, new PointF(15, BlockHeight * BlockDisplaySize + 15));
            GFX.DrawRectangle(new Pen(Color.FromArgb(170, 106, 0)), new Rectangle(55, BlockHeight * BlockDisplaySize + 19, 201, 21));
            GFX.FillRectangle(new SolidBrush(Color.FromArgb(50, 50, 50)), new Rectangle(56, BlockHeight * BlockDisplaySize + 20, 200, 20));
            GFX.DrawImageUnscaledAndClipped(Resources.PowerMeter, new Rectangle(56, BlockHeight * BlockDisplaySize + 20, TurboCounter * 2, 20));
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
            if (Server != null) { Server.Abort(); }
        }

        private void AgainButton_Click(object sender, EventArgs e)
        {
            PopupConnectionDialog();
        }

        private void MainForm_HelpButtonClicked(object sender, CancelEventArgs e)
        {
            new HelpForm().ShowDialog();
            e.Cancel = true;
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