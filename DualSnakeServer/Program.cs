using System;
using System.Collections.Generic;
using System.Text;
using Solymosi.Networking.Sockets;
using System.Net.Sockets;

namespace DualSnakeServer
{
    class Program
    {
        static public Server<SnakePlayer> Server = new Server<SnakePlayer>(1991);
        static public List<SnakeGame> Games = new List<SnakeGame>();
        static public int CurrentID = 0;

        static void Main(string[] args)
        {
            Server<SnakePlayer> Server = new Server<SnakePlayer>(1991);
            Server.Connected += new Server<SnakePlayer>.ConnectedDelegate(Server_Connected);
            Server.Listen();
            Console.WriteLine("DualSnake Server v1.0\r\nCopyright (C) Solymosi Máté 2011\r\nPress CTRL+C to exit...");
            while (true) { System.Threading.Thread.Sleep(10000); }
        }

        static void Server_Connected(object sender, Server<SnakePlayer>.ConnectionEventArgs e)
        {
            SnakeGame TargetGame = null;
            e.Client.Socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);
            foreach (SnakeGame G in Games)
            {
                if (G.Status == GameStatus.WaitingForOpponent) { TargetGame = G; break; }
            }
            if (TargetGame == null)
            {
                CurrentID++;
                TargetGame = new SnakeGame(e.Client);
                TargetGame.ID = CurrentID;
                TargetGame.GameOver += new SnakeGame.GameOverDelegate(TargetGame_GameOver);
                TargetGame.MessageLogged += new SnakeGame.LogMessageDelegate(TargetGame_MessageLogged);
                Games.Add(TargetGame);
                TargetGame_MessageLogged(TargetGame, new SnakeGame.LogEventArgs("First player connected"));
            }
            else
            {
                TargetGame.AddSecondPlayer(e.Client);
            }
        }

        static void TargetGame_MessageLogged(object sender, SnakeGame.LogEventArgs e)
        {
            SnakeGame G = (SnakeGame)sender;
            Console.WriteLine("#" + G.ID + ": " + e.Message);
        }

        static void TargetGame_GameOver(object sender, EventArgs e)
        {
            Games.Remove((SnakeGame)sender);
        }
    }
}
