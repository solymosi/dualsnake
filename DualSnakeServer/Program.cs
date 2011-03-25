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
                if (Game != null) { Game.AbortAll(null, null); }
                Game = new SnakeGame(e.Client);
                return;
            }
            else
            {
                if (Game.Status == GameStatus.WaitingForOpponent)
                {
                    Game.Start(e.Client);
                }
                else
                {
                    e.Client.Abort();
                }
            }
        }
    }
}
