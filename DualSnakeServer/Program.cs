using System;
using System.Collections.Generic;
using System.Text;
using Solymosi.Networking.Sockets;

namespace DualSnakeServer
{
    class Program
    {
        static public Server<SnakePlayer> Server = new Server<SnakePlayer>(1991);
        static public SnakeGame Game;

        static void Main(string[] args)
        {
            Server<SnakePlayer> Server = new Server<SnakePlayer>(1991);
            Server.Connected += new Server<SnakePlayer>.ConnectedDelegate(Server_Connected);
            Server.Listen();
            Console.WriteLine("DualSnake Server running :)\r\nPress CTRL+C to exit.");
            while (true)
            {
                System.Threading.Thread.Sleep(1000);
            }
        }

        static void Server_Connected(object sender, Server<SnakePlayer>.ConnectionEventArgs e)
        {
            if (Game == null || Game.Status == GameStatus.GameOver)
            {
                if (Game != null) { Game.AbortGame(); }
                Game = new SnakeGame(e.Client);
                Console.WriteLine("First player connected");
                Game.MessageLogged += new SnakeGame.LogMessageDelegate(delegate(object o, SnakeGame.LogEventArgs ea) { Console.WriteLine(ea.Message); });
                return;
            }
            else
            {
                if (Game.Status == GameStatus.WaitingForOpponent)
                {
                    Game.AddSecondPlayer(e.Client);
                }
                else
                {
                    e.Client.Abort();
                }
            }
        }
    }
}
