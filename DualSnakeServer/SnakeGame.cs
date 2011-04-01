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
        public const int ClockInterval = 40;
        public const int GetReadyDuration = 3;
        public const int InitialFood = 1;
        public const int InitialTurbo = 3;
        public const int TurboAmount = 20;
        public const int MaxTurbo = 100;
        public const int FieldWidth = 70;
        public const int FieldHeight = 40;

        public SnakeMap Map;
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

        public SnakeGame() : this(Tools.CreateDefaultMap(FieldWidth, FieldHeight)) { }
        public SnakeGame(SnakeMap Map)
        {
            this.Map = Map;
        }

        private Point ModPoint(Point p)
        {
            return Tools.ModPoint(p, FieldWidth, FieldHeight);
        }

        public void AddPlayer(SnakePlayer Player)
        {
            if (Players.Count > 1) { throw new InvalidOperationException(); }
            int Which = Players.Count + 1;
            Players.Add(Player);
            Player.Game = this;
            Player.Closed += new Client.CloseDelegate(AbortGame);
            Player.Send("#Player " + Which.ToString());
            MessageLogged(this, new LogEventArgs((Which == 1 ? "First" : "Second") + " player connected"));
            if (Players.Count == 2)
            {
                StartGame();
            }
        }

        protected void StartGame()
        {
            Send("#Countdown " + GetReadyDuration.ToString());
            CountDown.Interval = GetReadyDuration * 1000;
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
            MessageLogged(this, new LogEventArgs("Starting game in " + GetReadyDuration.ToString() + " seconds"));
        }

        public void CreateSnakes()
        {
            for (int Index = 0; Index < 2; Index++)
            {
                Point P = new Point(Map.StartLocation[Index].X, Map.StartLocation[Index].Y);
                for (int i = 0; i < Map.StartLength; i++)
                {
                    (Index == 0 ? Players.First() : Players.Last()).Snake.Add(new Point(P.X, P.Y));
                    switch (Map.StartDirection[Index])
                    {
                        case Direction.Up: P.Y++; break;
                        case Direction.Down: P.Y--; break;
                        case Direction.Left: P.X++; break;
                        case Direction.Right: P.X--; break;
                    }
                    ModPoint(P);
                }
            }
            Players.First().Snake.Reverse();
            Players.Last().Snake.Reverse();
            Players.First().CurrentDirection = Map.StartDirection[0];
            Players.Last().CurrentDirection = Map.StartDirection[1];
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
                    if (Players.First().Turbo > MaxTurbo) { Players.First().Turbo = MaxTurbo; }
                    AteTurbo(Players.First().Head);
                    MessageLogged(this, new LogEventArgs("Player 1 ate a turbo"));
                }
                if (AtTurbo[1])
                {
                    Players.Last().Turbo += TurboAmount;
                    if (Players.Last().Turbo > MaxTurbo) { Players.Last().Turbo = MaxTurbo; }
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

            Direction NextDirection = Player.CurrentDirection;
            if (Player.DirectionQueue.Count > 0)
            {
                NextDirection = Player.DirectionQueue.First();
                Player.DirectionQueue.RemoveAt(0);
            }

            if (Player.CurrentDirection != NextDirection)
            {
                switch (NextDirection)
                {
                    case Direction.Up: if (Player.CurrentDirection != Direction.Down) { Player.CurrentDirection = Direction.Up; } break;
                    case Direction.Down: if (Player.CurrentDirection != Direction.Up) { Player.CurrentDirection = Direction.Down; } break;
                    case Direction.Left: if (Player.CurrentDirection != Direction.Right) { Player.CurrentDirection = Direction.Left; } break;
                    case Direction.Right: if (Player.CurrentDirection != Direction.Left) { Player.CurrentDirection = Direction.Right; } break;
                }
            }
            switch (Player.CurrentDirection)
            {
                case Direction.Right: NewHead.X = Head.X + 1; NewHead.Y = Head.Y; break;
                case Direction.Left: NewHead.X = Head.X - 1; NewHead.Y = Head.Y; break;
                case Direction.Up: NewHead.X = Head.X; NewHead.Y = Head.Y - 1; break;
                case Direction.Down: NewHead.X = Head.X; NewHead.Y = Head.Y + 1; break;
            }

            NewHead = ModPoint(NewHead);

            bool Fail = false;
            for (int i = 0; i < Player.Snake.Count - 1; i++)
            {
                if ((Player.Snake[i].X == NewHead.X) && (Player.Snake[i].Y == NewHead.Y)) { Fail = true; }
            }
            for (int i = 0; i < Map.Walls.Count; i++)
            {
                if ((Map.Walls[i].X == NewHead.X) && (Map.Walls[i].Y == NewHead.Y)) { Fail = true; }
            }
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
            string Status = "#Status " + GetRepresentation(Map.Walls) + "\t" + GetRepresentation(Food) + "\t" + GetRepresentation(Turbo) + "\t" + GetRepresentation(Players.First().Snake) + "\t" + GetRepresentation(Players.Last().Snake);
            try
            {
                Players.First().Send(Status + "\t" + (Players.First().TurboEnabled ? "E" : "D") + "\t" + Players.First().Turbo.ToString());
                Players.Last().Send(Status + "\t" + (Players.Last().TurboEnabled ? "E" : "D") + "\t" + Players.Last().Turbo.ToString());
            }
            catch { }
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
            CountDown.Stop();
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
                X = Tools.Random.Next(1, FieldWidth + 1);
                Y = Tools.Random.Next(1, FieldHeight + 1);
                if (Food.Any(new Func<Point, bool>(delegate(Point c) { return c.X == X && c.Y == Y; }))) { continue; }
                if (Turbo.Any(new Func<Point, bool>(delegate(Point c) { return c.X == X && c.Y == Y; }))) { continue; }
                if (Map.Walls.Any(new Func<Point, bool>(delegate(Point c) { return c.X == X && c.Y == Y; }))) { continue; }
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
        public List<Direction> DirectionQueue = new List<Direction>();

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
                    case "U": DirectionQueue.Add(Direction.Up); break;
                    case "D": DirectionQueue.Add(Direction.Down); break;
                    case "L": DirectionQueue.Add(Direction.Left); break;
                    case "R": DirectionQueue.Add(Direction.Right); break;
                }
                if (DirectionQueue.Count > 2) { DirectionQueue = DirectionQueue.Skip(DirectionQueue.Count - 2).Take(2).ToList(); }
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
                this.Turbo = 100;
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

        public static Direction ParseDirection(string Input)
        {
            switch (Input.ToLower())
            {
                case "up": return Direction.Up;
                case "down": return Direction.Down;
                case "left": return Direction.Left;
                case "right": return Direction.Right;
            }
            throw new ArgumentException();
        }

        public static Point ModPoint(Point p, int FieldWidth, int FieldHeight)
        {
            Point q = new Point(0, 0);
            q.X = p.X; q.Y = p.Y;
            if (q.X < 1) { q.X = q.X % FieldWidth + FieldWidth; }
            if (q.X > FieldWidth) { q.X %= FieldWidth; }
            if (q.Y < 1) { q.Y = q.Y % FieldHeight + FieldHeight; }
            if (q.Y > FieldHeight) { q.Y %= FieldHeight; }
            return q;
        }

        public static SnakeMap CreateDefaultMap(int Width, int Height)
        {
            string BlankLevel = "Blank level | 8 | Right | Left\n";
            for (int i = 0; i < Height; i++)
            {
                for (int j = 0; j < Width; j++)
                {
                    if (new int[] { 0, Height - 1 }.Contains(i) || new int[] { 0, Width - 1 }.Contains(j)) { BlankLevel += "%"; continue; }
                    BlankLevel += (i == Height / 2 ? (j == 10 ? "1" : (j == Width - 11 ? "2" : " ")) : " ");
                }
                BlankLevel += "\n";
            }
            return SnakeMap.Parse(BlankLevel, Width, Height);
        }
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

    public class SnakeMap
    {
        public int StartLength;
        public List<Point> Walls = new List<Point>();
        public Point[] StartLocation = new Point[2];
        public Direction[] StartDirection = new Direction[2];

        public SnakeMap() { throw new InvalidOperationException("Use SnakeMap.Parse instead."); }
        private SnakeMap(bool PrivateAccess) { }

        static public SnakeMap Parse(string Level, int FieldWidth, int FieldHeight)
        {
            SnakeMap Map = new SnakeMap(true);
            string[] Rows = Level.Split('\n').Select(new Func<string, string>(delegate(string start) { return start.TrimEnd(new char[] { '\r' }); })).Where(new Func<string, bool>(delegate(string w) { return w != ""; })).ToArray();

            string[] Info = Rows[0].Split('|').Select(new Func<string, string>(delegate(string start) { return start.Trim(); })).ToArray();
            Map.StartLength = int.Parse(Info[1]);
            Map.StartDirection[0] = Tools.ParseDirection(Info[2]);
            Map.StartDirection[1] = Tools.ParseDirection(Info[3]);

            if (Rows.Length != FieldHeight + 1) { throw new ArgumentException(); }

            for (int Row = 1; Row < Rows.Length; Row++)
            {
                for (int j = 0; j < FieldWidth; j++)
                {
                    int Column = j + 1;
                    if (Rows[Row][j] == '%') { Map.Walls.Add(new Point(Column, Row)); }
                }
            }

            int Count = 0;
            for (int Row = 1; Row < Rows.Length; Row++)
            {
                for (int j = 0; j < FieldWidth; j++)
                {
                    int Column = j + 1;
                    if (new char[] { '1', '2' }.Contains(Rows[Row][j]))
                    {
                        Count++;
                        if (Count > 2) { throw new ArgumentException(); }
                        int Index = Rows[Row][j] == '1' ? 0 : 1;
                        if (Map.StartLocation[Index] != null) { throw new ArgumentException(); }
                        for (int i = 0; i < Map.StartLength; i++)
                        {
                            Point MP;
                            switch (Map.StartDirection[Index])
                            {
                                case Direction.Up: MP = new Point(Column, Row + i); break;
                                case Direction.Down: MP = new Point(Column, Row - i); break;
                                case Direction.Left: MP = new Point(Column + i, Row); break;
                                case Direction.Right: MP = new Point(Column - i, Row); break;
                                default: throw new ArgumentException();
                            }
                            MP = Tools.ModPoint(MP, FieldWidth, FieldHeight);
                            if (Map.Walls.Any(new Func<Point, bool>(delegate(Point p) { return p.X == MP.X && p.Y == MP.Y; })) || (Count > 1 ? Map.StartLocation[1 - Index].Equals(MP) : false))
                            {
                                throw new ArgumentException();
                            }
                        }
                        Map.StartLocation[Index] = new Point(Column, Row);
                    }
                }
            }

            if (Map.StartLocation[0] == null || Map.StartLocation[1] == null) { throw new ArgumentException(); }

            return Map;
        }
    }
}