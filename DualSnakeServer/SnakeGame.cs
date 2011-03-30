using System;
using System.Collections.Generic;
using System.Text;
using System.Timers;
using Solymosi.Networking.Sockets;
using System.Linq;

namespace DualSnakeServer
{
    public class SnakeGame
    {
        public const int StartLength = 8;
        public const int ClockInterval = 50;
        public const int CountdownDuration = 3;
        public const int TurboAmount = 20;
        public const int InitialFood = 2;
        public const int InitialTurbo = 2;
        public const int BlockWidth = 50;
        public const int BlockHeight = 50;

        public List<Point> Food = new List<Point>();
        public List<Point> Turbo = new List<Point>();

        public List<SnakePlayer> Players = new List<SnakePlayer>(2);

        protected Timer Clock = new Timer();
        protected Timer CountDown = new Timer();

        public int ID = 0;

        protected bool TurboRound = false;
        protected bool Aborting = false;

        public delegate void GameOverDelegate(object sender, EventArgs e);
        public event GameOverDelegate GameOver = delegate { };

        public delegate void LogMessageDelegate(object sender, LogEventArgs e);
        public event LogMessageDelegate MessageLogged = delegate { };

        public class LogEventArgs : EventArgs
        {
            public string Message;
            public LogEventArgs(string Message) { this.Message = Message; }
        }

        public GameStatus Status
        {
            get
            {
                if (Players.Count < 2) { return GameStatus.WaitingForOpponent; }
                if (!Clock.Enabled && CountDown.Enabled) { return GameStatus.CountDown; }
                if (Clock.Enabled) { return GameStatus.Playing; }
                return GameStatus.GameOver;
            }
        }

        public SnakeGame(SnakePlayer FirstPlayer)
        {
            AddPlayer(FirstPlayer);
            Players.First().Send("#First");
            MessageLogged(this, new LogEventArgs("First player connected"));
        }

        public void AddSecondPlayer(SnakePlayer SecondPlayer)
        {
            AddPlayer(SecondPlayer);
            Players.Last().Send("#Second");
            MessageLogged(this, new LogEventArgs("Second player connected"));
            StartGame();
        }

        protected void AddPlayer(SnakePlayer Player)
        {
            Players.Add(Player);
            Player.Game = this;
            Player.Closed += new Client.CloseDelegate(AbortGame);
        }

        protected void StartGame()
        {
            Send("#Countdown " + CountdownDuration.ToString());
            CountDown.Interval = CountdownDuration * 1000;
            CountDown.Elapsed += new ElapsedEventHandler(delegate
            {
                CountDown.Stop();
                CreateSnakes();
                for (int i = 0; i < InitialFood; i++) { PlaceFood(); }
                for (int i = 0; i < InitialTurbo; i++) { PlaceTurbo(); }
                Clock.Interval = ClockInterval;
                Clock.Elapsed += new ElapsedEventHandler(Clock_Elapsed);
                Clock.Start();
                MessageLogged(this, new LogEventArgs("Game started"));
            });
            CountDown.Start();
            MessageLogged(this, new LogEventArgs("Starting game in " + CountdownDuration.ToString() + " seconds"));
        }

        public void CreateSnakes()
        {
            for (int i = 1; i <= StartLength; i++)
            {
                Players.First().Snake.Add(new Point(BlockHeight / 2, i + 2));
                Players.Last().Snake.Add(new Point(BlockHeight / 2, BlockWidth - i - 1));
            }
            Players.First().CurrentDirection = Direction.Right;
            Players.Last().CurrentDirection = Direction.Left;
        }

        void Clock_Elapsed(object sender, ElapsedEventArgs e)
        {
            TurboRound = !TurboRound;

            bool[] Fail = new bool[2];
            bool[] AtFood = new bool[2];
            bool[] AtTurbo = new bool[2];

            try { NextMove(Players.First()); }
            catch (InvalidOperationException) { Fail[0] = true; }
            try { NextMove(Players.Last()); }
            catch (InvalidOperationException) { Fail[1] = true; }

            AtFood[0] = IsAtFood(Players.First());
            AtFood[1] = IsAtFood(Players.Last());

            if (AtFood[0] && AtFood[1] && Players.First().Head.Equals(Players.Last().Head))
            {
                AteFood(Players.First().Head);
            }
            else
            {
                if (AtFood[0])
                {
                    Players.Last().Snake.RemoveAt(0);
                    AteFood(Players.First().Head);
                    MessageLogged(this, new LogEventArgs("Player 1 ate a food"));
                }
                else { if (!TurboRound || Players.First().TurboEnabled) { Players.First().Snake.RemoveAt(0); } }
                if (AtFood[1])
                {
                    Players.First().Snake.RemoveAt(0);
                    AteFood(Players.Last().Head);
                    MessageLogged(this, new LogEventArgs("Player 2 ate a food"));
                }
                else { if (!TurboRound || Players.Last().TurboEnabled) { Players.Last().Snake.RemoveAt(0); } }
                if (Players.First().Snake.Count < 1) { Fail[0] = true; }
                if (Players.Last().Snake.Count < 1) { Fail[1] = true; }
            }

            if (Fail[0] && Fail[1]) { FinishGame(null); return; }
            if (Fail[0]) { FinishGame(Players.Last()); return; }
            if (Fail[1]) { FinishGame(Players.First()); return; }

            AtTurbo[0] = IsAtTurbo(Players.First());
            AtTurbo[1] = IsAtTurbo(Players.Last());

            if (AtTurbo[0] && AtTurbo[1] && Players.First().Head.Equals(Players.Last().Head))
            {
                AteTurbo(Players.First().Head);
            }
            else
            {
                if (AtTurbo[0])
                {
                    Players.First().Turbo += TurboAmount;
                    AteTurbo(Players.First().Head);
                    MessageLogged(this, new LogEventArgs("Player 1 ate a turbo"));
                }
                if (AtTurbo[1])
                {
                    Players.Last().Turbo += TurboAmount;
                    AteTurbo(Players.Last().Head);
                    MessageLogged(this, new LogEventArgs("Player 2 ate a turbo"));
                }
            }

            foreach (SnakePlayer P in Players)
            {
                if (P.TurboEnabled)
                {
                    P.Turbo--;
                    if (P.Turbo <= 0) { P.Turbo = 0; P.TurboEnabled = false; }
                }
            }

            SendStatus();
        }

        protected void NextMove(SnakePlayer Player)
        {
            if (TurboRound && !Player.TurboEnabled) { return; }

            Point Head = Player.Head;
            Point NewHead = new Point(0, 0);

            if (Player.CurrentDirection != Player.NextDirection)
            {
                switch (Player.NextDirection)
                {
                    case Direction.Up: if (Player.CurrentDirection != Direction.Down) { Player.CurrentDirection = Direction.Up; } break;
                    case Direction.Down: if (Player.CurrentDirection != Direction.Up) { Player.CurrentDirection = Direction.Down; } break;
                    case Direction.Left: if (Player.CurrentDirection != Direction.Right) { Player.CurrentDirection = Direction.Left; } break;
                    case Direction.Right: if (Player.CurrentDirection != Direction.Left) { Player.CurrentDirection = Direction.Right; } break;
                }
            }
            switch (Player.CurrentDirection)
            {
                case Direction.Right: NewHead.X = Head.X; NewHead.Y = Head.Y + 1; break;
                case Direction.Left: NewHead.X = Head.X; NewHead.Y = Head.Y - 1; break;
                case Direction.Up: NewHead.X = Head.X - 1; NewHead.Y = Head.Y; break;
                case Direction.Down: NewHead.X = Head.X + 1; NewHead.Y = Head.Y; break;
            }

            bool Fail = false;
            for (int i = 0; i < Player.Snake.Count - 1; i++)
            {
                if ((Player.Snake[i].X == NewHead.X) && (Player.Snake[i].Y == NewHead.Y)) { Fail = true; }
            }
            if (NewHead.X < 1 || NewHead.X > BlockWidth || NewHead.Y < 1 || NewHead.Y > BlockHeight) { Fail = true; }
            if (Fail) { throw new InvalidOperationException(); }

            Player.Snake.Add(NewHead);
        }

        protected bool IsAtFood(SnakePlayer Player)
        {
            foreach (Point F in Food) { if (F.X == Player.Head.X && F.Y == Player.Head.Y) { return true; } }
            return false;
        }

        protected bool IsAtTurbo(SnakePlayer Player)
        {
            foreach (Point T in Turbo) { if (T.X == Player.Head.X && T.Y == Player.Head.Y) { return true; } }
            return false;
        }

        protected void AteFood(Point Which)
        {
            RemoveFromPointList(Food, Which);
            PlaceFood();
        }

        protected void AteTurbo(Point Which)
        {
            RemoveFromPointList(Turbo, Which);
            PlaceTurbo();
        }

        protected void RemoveFromPointList(List<Point> List, Point What)
        {
            Point TR = new Point(0, 0);
            bool found = false;
            foreach (Point F in List)
            {
                if (F.X == What.X && F.Y == What.Y) { TR = F; found = true; break; }
            }
            if (found) { List.Remove(TR); }
        }

        protected void SendStatus()
        {
            string Status = "#Status " + GetRepresentation(Food) + "\t" + GetRepresentation(Turbo) + "\t" + GetRepresentation(Players.First().Snake) + "\t" + GetRepresentation(Players.Last().Snake);
            Players.First().Send(Status + "\t" + (Players.First().TurboEnabled ? "E" : "D") + "\t" + Players.First().Turbo.ToString());
            Players.Last().Send(Status + "\t" + (Players.Last().TurboEnabled ? "E" : "D") + "\t" + Players.Last().Turbo.ToString());
        }

        protected string GetRepresentation(List<Point> PointList)
        {
            List<char> chars = new List<char>();
            for (int i = 0; i < PointList.Count; i++)
            {
                chars.Add((char)(PointList[i].X + 20));
                chars.Add((char)(PointList[i].Y + 20));
            }
            return new string(chars.ToArray());
        }

        protected void FinishGame(SnakePlayer Winner)
        {
            if (Winner == null)
            {
                Send("#Draw");
                MessageLogged(this, new LogEventArgs("Game over: Draw"));
            }
            else
            {
                int Won = Winner == Players.First() ? 1 : 2;
                Send("#Winner " + Won.ToString());
                MessageLogged(this, new LogEventArgs("Game over: Player " + Won.ToString() + " won"));
            }
            AbortGame();
        }

        public void AbortGame() { AbortGame(null, null); }
        protected void AbortGame(object sender, Client.CloseEventArgs e)
        {
            if (Aborting) { return; }
            Clock.Stop();
            this.Aborting = true;
            try
            {
                foreach (SnakePlayer P in Players)
                {
                    try { P.Disconnect(); }
                    catch { P.Abort(); }
                }
            }
            catch { }
            finally
            {
                GameOver(this, new EventArgs());
                MessageLogged(this, new LogEventArgs("Players disconnected."));
            }
        }

        public void Send(string Text)
        {
            foreach (SnakePlayer P in Players) { P.Send(Text); }
        }

        public void Send(byte[] Data)
        {
            foreach (SnakePlayer P in Players) { P.Send(Data); }
        }

        protected Point FreePoint()
        {
            int X = 0, Y = 0;
            do
            {
                X = Tools.Random.Next(2, BlockWidth);
                Y = Tools.Random.Next(2, BlockHeight);
                if (Food.Any(new Func<Point, bool>(delegate(Point c) { return c.X == X && c.Y == Y; }))) { continue; }
                if (Turbo.Any(new Func<Point, bool>(delegate(Point c) { return c.X == X && c.Y == Y; }))) { continue; }
                if (Players.First().Snake.Any(new Func<Point, bool>(delegate(Point c) { return c.X == X && c.Y == Y; }))) { continue; }
                if (Players.Last().Snake.Any(new Func<Point, bool>(delegate(Point c) { return c.X == X && c.Y == Y; }))) { continue; }
                return new Point(X, Y);
            } while (true);
        }

        public void PlaceFood()
        {
            Food.Add(FreePoint());
        }

        public void PlaceTurbo()
        {
            Turbo.Add(FreePoint());
        }
    }

    public class SnakePlayer : Client
    {
        public Direction CurrentDirection;
        public Direction NextDirection;

        public SnakeGame Game;
        public List<Point> Snake = new List<Point>();

        public int Turbo = 0;
        public bool TurboEnabled = false;

        public Point Head { get { return Snake.Last(); } }

        public SnakePlayer()
        {
            this.Received += new Client.ReceiveDelegate(Client_Received);
        }

        void Client_Received(object sender, Client.TransmitEventArgs e)
        {
            if (e.Text.StartsWith("#D "))
            {
                switch (e.Text.Substring(3))
                {
                    case "up": this.NextDirection = Direction.Up; return;
                    case "down": this.NextDirection = Direction.Down; return;
                    case "left": this.NextDirection = Direction.Left; return;
                    case "right": this.NextDirection = Direction.Right; return;
                }
            }

            if (e.Text == "#Turbo on")
            {
                if (this.Turbo > 0) { this.TurboEnabled = true; }
            }

            if (e.Text == "#Turbo off")
            {
                this.TurboEnabled = false;
            }

            if (e.Text == "#MaxTurbo")
            {
                this.Turbo = 10000;
            }
        }
    }

    public enum Direction
    {
        Up = 1,
        Down = 2,
        Left = 3,
        Right = 4
    }

    public enum GameStatus
    {
        WaitingForOpponent,
        CountDown,
        Playing,
        GameOver
    }

    public static class Tools
    {
        public static Random Random = new Random();
    }

    public class Point
    {
        public int X = 0;
        public int Y = 0;
        public Point(int X, int Y)
        {
            this.X = X;
            this.Y = Y;
        }
        public bool Equals(Point obj)
        {
            return obj.X == this.X && obj.Y == this.Y;
        }
        public override int GetHashCode()
        {
            return this.X * 1000 + this.Y;
        }
        public override string ToString()
        {
            return X.ToString() + "," + Y.ToString();
        }
    }
}