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
        public int Speed = 1;
        public int StartLength = 5;
        public int FoodEaten = 0;
        public int CurrentSpeed { get { return Math.Min(FoodEaten / 5 + 1, 10); } }
        public int CurrentInterval { get { return 80; } }
        public List<Point> Food = new List<Point>();
        public List<Point> Turbo = new List<Point>();
        public SnakePlayer PlayerOne;
        public SnakePlayer PlayerTwo;
        public Timer Clock = new Timer();
        public Timer CountDown = new Timer();
        public int BlockWidth = 50;
        public int BlockHeight = 50;
        public bool TurboOnly = false;
        public SnakePlayer Winner = null;

        public GameStatus Status
        {
            get
            {
                if (PlayerTwo == null) { return GameStatus.WaitingForOpponent; }
                if (!Clock.Enabled && CountDown.Enabled) { return GameStatus.CountDown; }
                return Clock.Enabled || (PlayerOne == null && PlayerTwo == null) ? GameStatus.Playing : GameStatus.GameOver;
            }
        }

        public SnakeGame(SnakePlayer FirstPlayer)
        {
            FirstPlayer.Game = this;
            this.PlayerOne = FirstPlayer;
            this.PlayerOne.Closed += new Client.CloseDelegate(AbortAll);
            this.PlayerOne.Send("YOU ARE FIRST");
            Console.WriteLine("First player connected.");
        }

        public void Start(SnakePlayer SecondPlayer)
        {
            SecondPlayer.Game = this;
            this.PlayerTwo = SecondPlayer;
            this.PlayerTwo.Closed += new Client.CloseDelegate(AbortAll);
            this.PlayerTwo.Send("YOU ARE SECOND");
            Console.WriteLine("Second player connected.");
        }

        public void AbortAll(object sender, Client.CloseEventArgs e)
        {
            try
            {
                PlayerOne.Closed -= AbortAll;
                PlayerTwo.Closed -= AbortAll;
                try { PlayerOne.Disconnect(); }
                catch { PlayerOne.Abort(); }
                try { PlayerTwo.Disconnect(); }
                catch { PlayerTwo.Abort(); }
            }
            catch { }
            finally
            {
                Console.WriteLine("Game aborted.");
                Program.Game = null;
            }
        }

        public void InitSnakes()
        {
            for (int i = 1; i <= StartLength; i++)
            {
                PlayerOne.Snake.Add(new Point(BlockHeight / 2, i + 2));
                PlayerTwo.Snake.Add(new Point(BlockHeight / 2, BlockWidth - i - 1));
            }
            PlayerOne.CurrentDirection = Direction.Right;
            PlayerTwo.CurrentDirection = Direction.Left;
            RandomFood();
            Console.WriteLine("Snakes initialized.");
        }

        void Clock_Elapsed(object sender, ElapsedEventArgs e)
        {
            TurboOnly = !TurboOnly;

            Point HeadOne = PlayerOne.Snake[PlayerOne.Snake.Count - 1];
            Point HeadTwo = PlayerTwo.Snake[PlayerTwo.Snake.Count - 1];

            Point NewPointOne = new Point(0, 0);
            Point NewPointTwo = new Point(0, 0);

            foreach (SnakePlayer Player in new SnakePlayer[] { PlayerOne, PlayerTwo })
            {
                if (!TurboOnly || Player.TurboEnabled)
                {
                    if (Player.CurrentDirection != Player.NextDirection)
                    {
                        switch (Player.NextDirection)
                        {
                            case Direction.Up:
                                if (Player.CurrentDirection != Direction.Down) { Player.CurrentDirection = Direction.Up; }
                                break;
                            case Direction.Down:
                                if (Player.CurrentDirection != Direction.Up) { Player.CurrentDirection = Direction.Down; }
                                break;
                            case Direction.Left:
                                if (Player.CurrentDirection != Direction.Right) { Player.CurrentDirection = Direction.Left; }
                                break;
                            case Direction.Right:
                                if (Player.CurrentDirection != Direction.Left) { Player.CurrentDirection = Direction.Right; }
                                break;
                        }
                    }
                }
            }

            if (!TurboOnly || PlayerOne.TurboEnabled)
            {
                switch (PlayerOne.CurrentDirection)
                {
                    case Direction.Right:
                        NewPointOne.X = HeadOne.X;
                        NewPointOne.Y = HeadOne.Y + 1;
                        break;
                    case Direction.Left:
                        NewPointOne.X = HeadOne.X;
                        NewPointOne.Y = HeadOne.Y - 1;
                        break;
                    case Direction.Up:
                        NewPointOne.X = HeadOne.X - 1;
                        NewPointOne.Y = HeadOne.Y;
                        break;
                    case Direction.Down:
                        NewPointOne.X = HeadOne.X + 1;
                        NewPointOne.Y = HeadOne.Y;
                        break;
                }
            }
            if (!TurboOnly || PlayerTwo.TurboEnabled)
            {
                switch (PlayerTwo.CurrentDirection)
                {
                    case Direction.Right:
                        NewPointTwo.X = HeadTwo.X;
                        NewPointTwo.Y = HeadTwo.Y + 1;
                        break;
                    case Direction.Left:
                        NewPointTwo.X = HeadTwo.X;
                        NewPointTwo.Y = HeadTwo.Y - 1;
                        break;
                    case Direction.Up:
                        NewPointTwo.X = HeadTwo.X - 1;
                        NewPointTwo.Y = HeadTwo.Y;
                        break;
                    case Direction.Down:
                        NewPointTwo.X = HeadTwo.X + 1;
                        NewPointTwo.Y = HeadTwo.Y;
                        break;
                }
            }

            bool FailOne = false;
            bool FailTwo = false;

            if (!TurboOnly || PlayerOne.TurboEnabled)
            {
                for (int i = 0; i < PlayerOne.Snake.Count - 1; i++)
                {
                    if ((PlayerOne.Snake[i].X == NewPointOne.X) && (PlayerOne.Snake[i].Y == NewPointOne.Y))
                    {
                        FailOne = true;
                    }
                }
            }
            if (!TurboOnly || PlayerTwo.TurboEnabled)
            {
                for (int i = 0; i < PlayerTwo.Snake.Count - 1; i++)
                {
                    if ((PlayerTwo.Snake[i].X == NewPointTwo.X) && (PlayerTwo.Snake[i].Y == NewPointTwo.Y))
                    {
                        FailTwo = true;
                    }
                }
            }
            if (!TurboOnly || PlayerOne.TurboEnabled)
            {
                if (NewPointOne.X < 1 || NewPointOne.X > this.BlockWidth || NewPointOne.Y < 1 || NewPointOne.Y > this.BlockHeight)
                {
                    FailOne = true;
                }
            }
            if (!TurboOnly || PlayerTwo.TurboEnabled)
            {
                if (NewPointTwo.X < 1 || NewPointTwo.X > this.BlockWidth || NewPointTwo.Y < 1 || NewPointTwo.Y > this.BlockHeight)
                {
                    FailTwo = true;
                }
            }

            bool AtFoodOne = false;
            bool AtFoodTwo = false;
            Point WhichFood = new Point(0, 0);

            for (int i = 0; i < Food.Count; i++)
            {
                if (!TurboOnly || PlayerOne.TurboEnabled)
                {
                    if (Food[i].X == HeadOne.X && Food[i].Y == HeadOne.Y)
                    {
                        AtFoodOne = true;
                        WhichFood = Food[i];
                    }
                }
                if (!TurboOnly || PlayerTwo.TurboEnabled)
                {
                    if (Food[i].X == HeadTwo.X && Food[i].Y == HeadTwo.Y)
                    {
                        AtFoodTwo = true;
                        WhichFood = Food[i];
                    }
                }
            }

            if (!AtFoodOne)
            {
                if (!TurboOnly || PlayerOne.TurboEnabled)
                {
                    PlayerOne.Snake.RemoveAt(0);
                }
                if (AtFoodTwo)
                {
                    if (PlayerOne.Snake.Count < 1) { FailOne = true; }
                    else { PlayerOne.Snake.RemoveAt(0); }
                }
            }
            if (!AtFoodTwo)
            {
                if (!TurboOnly || PlayerTwo.TurboEnabled)
                {
                    PlayerTwo.Snake.RemoveAt(0);
                }
                if (AtFoodOne)
                {
                    if (PlayerTwo.Snake.Count < 1) { FailTwo = true; }
                    else { PlayerTwo.Snake.RemoveAt(0); }
                }
            }

            if (AtFoodOne || AtFoodTwo)
            {
                Food.Remove(WhichFood);
                RandomFood();
                FoodEaten++;
                Console.WriteLine("Ate a food. Yam-yam :)");
            }

            if (!TurboOnly || PlayerOne.TurboEnabled)
            {
                PlayerOne.Snake.Add(NewPointOne);
            }
            if (!TurboOnly || PlayerTwo.TurboEnabled)
            {
                PlayerTwo.Snake.Add(NewPointTwo);
            }

            if (PlayerOne.TurboEnabled)
            {
                PlayerOne.Turbo--;
                if (PlayerOne.Turbo == 0) { PlayerOne.TurboEnabled = false; }
            }
            if (PlayerTwo.TurboEnabled)
            {
                PlayerTwo.Turbo--;
                if (PlayerTwo.Turbo == 0) { PlayerTwo.TurboEnabled = false; }
            }

            // TURBO START

            bool AtTurboOne = false;
            bool AtTurboTwo = false;
            Point WhichTurboOne = new Point(0, 0);
            Point WhichTurboTwo = new Point(0, 0);

            for (int i = 0; i < Turbo.Count; i++)
            {
                if (!TurboOnly || PlayerOne.TurboEnabled)
                {
                    if (Turbo[i].X == HeadOne.X && Turbo[i].Y == HeadOne.Y)
                    {
                        AtTurboOne = true;
                        WhichTurboOne = Turbo[i];
                    }
                }
                if (!TurboOnly || PlayerTwo.TurboEnabled)
                {
                    if (Turbo[i].X == HeadTwo.X && Turbo[i].Y == HeadTwo.Y)
                    {
                        AtTurboTwo = true;
                        WhichTurboTwo = Turbo[i];
                    }
                }
            }

            if(!(AtTurboOne && AtTurboTwo && WhichTurboOne.X == WhichTurboTwo.X && WhichTurboOne.Y == WhichTurboTwo.Y))
            {
                if (AtTurboOne)
                {
                    PlayerOne.Turbo += 20;
                    Turbo.Remove(WhichTurboOne);
                    RandomTurbo();
                }
                if (AtTurboTwo)
                {
                    PlayerTwo.Turbo += 20;
                    Turbo.Remove(WhichTurboTwo);
                    RandomTurbo();
                }
            }

            if (AtTurboOne || AtTurboTwo)
            {
                Console.WriteLine("Ate a turbo! Yeah!");
            }

            // TURBO END

            if (FailOne && FailTwo)
            {
                GameOver();
                return;
            }
            if (FailOne)
            {
                Winner = PlayerTwo;
                GameOver();
                return;
            }
            if (FailTwo)
            {
                Winner = PlayerOne;
                GameOver();
                return;
            }

            Clock.Interval = CurrentInterval;

            string DataFood = string.Join(";", Food.Select<Point, string>(new Func<Point, string>(delegate(Point p) { return p.ToString(); })).ToArray());
            string DataTurbo = string.Join(";", Turbo.Select<Point, string>(new Func<Point, string>(delegate(Point p) { return p.ToString(); })).ToArray());
            string DataSnakeOne = string.Join(";", PlayerOne.Snake.Select<Point, string>(new Func<Point, string>(delegate(Point p) { return p.ToString(); })).ToArray());
            string DataSnakeTwo = string.Join(";", PlayerTwo.Snake.Select<Point, string>(new Func<Point, string>(delegate(Point p) { return p.ToString(); })).ToArray());

            string st = "STATUS " + DataFood + "\t" + DataSnakeOne + "\t" + DataSnakeTwo + "\t" + DataTurbo;
            PlayerOne.Send(st + "\t" + (PlayerOne.TurboEnabled ? "E" : "D") + "\t" + PlayerOne.Turbo.ToString());
            PlayerTwo.Send(st + "\t" + (PlayerTwo.TurboEnabled ? "E" : "D") + "\t" + PlayerTwo.Turbo.ToString());
        }

        public void GameOver()
        {
            Clock.Stop();
            if (Winner == null)
            {
                PlayerOne.Send("DRAW");
                PlayerTwo.Send("DRAW");
                Console.WriteLine("Game over: draw");
            }
            else
            {
                PlayerOne.Send(Winner == PlayerOne ? "YOU WON" : "YOU LOST");
                PlayerTwo.Send(Winner == PlayerOne ? "YOU LOST" : "YOU WON");
                Console.WriteLine("Game over: " + (Winner == PlayerOne ? "Player one won" : "Player two won"));
            }
            /*PlayerOne.Disconnect();
            PlayerTwo.Disconnect();*/
        }

        public void SendAll(string Text)
        {
            PlayerOne.Send(Text);
            PlayerTwo.Send(Text);
        }

        public void RandomFood()
        {
            bool Problem = false;
            int RandomX = 0;
            int RandomY = 0;
            do
            {
                Problem = false;
                RandomX = Tools.Random.Next(1, BlockWidth);
                RandomY = Tools.Random.Next(1, BlockHeight);
                for (int i = 0; i < Food.Count; i++)
                {
                    if (Food[i].X == RandomX && Food[i].Y == RandomY)
                    {
                        Problem = true;
                    }
                }
                for (int i = 0; i < Turbo.Count; i++)
                {
                    if (Turbo[i].X == RandomX && Turbo[i].Y == RandomY)
                    {
                        Problem = true;
                    }
                }
            } while (Problem);
            Food.Add(new Point(RandomX, RandomY));
            RandomTurbo();
            Console.WriteLine("New food generated.");
        }

        public void RandomTurbo()
        {
            bool Problem = false;
            int RandomX = 0;
            int RandomY = 0;
            do
            {
                Problem = false;
                RandomX = Tools.Random.Next(1, BlockWidth);
                RandomY = Tools.Random.Next(1, BlockHeight);
                for (int i = 0; i < Food.Count; i++)
                {
                    if (Food[i].X == RandomX && Food[i].Y == RandomY)
                    {
                        Problem = true;
                    }
                }
                for (int i = 0; i < Turbo.Count; i++)
                {
                    if (Turbo[i].X == RandomX && Turbo[i].Y == RandomY)
                    {
                        Problem = true;
                    }
                }
            } while (Problem);
            Turbo.Add(new Point(RandomX, RandomY));
            Console.WriteLine("New turbo generated.");
        }

        public void InitCountdown()
        {
            SendAll("START COUNTDOWN");
            PlayerOne.Send("OTHER NAME " + PlayerTwo.Name);
            PlayerTwo.Send("OTHER NAME " + PlayerOne.Name);
            CountDown.Interval = 3000;
            CountDown.Elapsed += new ElapsedEventHandler(delegate
            {
                CountDown.Stop();
                Clock.Interval = CurrentInterval;
                Clock.Elapsed += new ElapsedEventHandler(Clock_Elapsed);
                InitSnakes();
                Clock.Start();
                Console.WriteLine("Starting game...");
            });
            CountDown.Start();
            Console.WriteLine("Staring countdown...");
        }
    }

    public class SnakePlayer : Client
    {
        public string Name = "";
        public Direction CurrentDirection;
        public Direction NextDirection;
        public List<Point> Snake = new List<Point>();
        public SnakeGame Game;
        public int Turbo = 0;
        public bool TurboEnabled = false;

        public SnakePlayer()
        {
            this.Received += new Client.ReceiveDelegate(Client_Received);
        }

        void Client_Received(object sender, Client.TransmitEventArgs e)
        {
            SnakePlayer OtherPlayer = Game.PlayerOne == this ? Game.PlayerTwo : Game.PlayerOne;
            if (this.Name == "")
            {
                try
                {
                    if (!e.Text.StartsWith("NAME ")) { throw new Exception(); }
                    string N = e.Text.Substring(5).Trim();
                    if (N == "") { throw new Exception(); }
                    this.Name = N;
                    Console.WriteLine("Name " + N + " received.");
                    if (OtherPlayer != null && OtherPlayer.Name != "")
                    {
                        Game.InitCountdown();
                    }
                }
                catch { this.Abort(); }
                return;
            }

            if (e.Text.StartsWith("DIRECTION "))
            {
                switch (e.Text.Substring(10))
                {
                    case "UP": this.NextDirection = Direction.Up; return;
                    case "DOWN": this.NextDirection = Direction.Down; return;
                    case "LEFT": this.NextDirection = Direction.Left; return;
                    case "RIGHT": this.NextDirection = Direction.Right; return;
                }
            }

            if (e.Text == "TURBO")
            {
                this.TurboEnabled = !this.TurboEnabled;
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
        public override bool Equals(object obj)
        {
            return (((Point)obj).X == this.X && ((Point)obj).Y == this.Y);
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
        public override string ToString()
        {
            return X.ToString() + "," + Y.ToString();
        }
    }
}