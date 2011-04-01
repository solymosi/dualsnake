using System;
using System.Collections.Generic;
using System.Text;
using Solymosi.Networking.Sockets;
using System.Net.Sockets;
using System.IO;

namespace DualSnakeServer
{
    class Program
    {
        static public Server<SnakePlayer> Server = new Server<SnakePlayer>(1991);
        static public List<SnakeGame> Games = new List<SnakeGame>();
        static public int CurrentID = 0;
        static public SnakeMap Map;

        static void Main(string[] args)
        {
            Console.WriteLine("DualSnake Server v1.0\r\nCopyright (C) Solymosi Máté 2011\r\n");
            if (args.Length > 0)
            {
                try { LoadLevel(args[0]); }
                catch { return; }
            }
            else
            {
                try { LoadLevel("Default.level"); }
                catch
                {
                    Console.WriteLine("Using built-in default map.");
                    Map = Tools.CreateDefaultMap(SnakeGame.FieldWidth, SnakeGame.FieldHeight);
                }
            }
            
            Server<SnakePlayer> Server = new Server<SnakePlayer>(1991);
            Server.Connected += new Server<SnakePlayer>.ConnectedDelegate(Server_Connected);
            Server.Listen();
            Console.WriteLine("\r\nListening on port 1991. Press CTRL+C to exit.");
            while (true) { System.Threading.Thread.Sleep(10000); }
        }

        static void LoadLevel(string F)
        {
            string Input = "";
            SnakeMap LoadedMap;
            Console.WriteLine("Loading map " + F + "...");
            try { Input = File.ReadAllText(F); }
            catch
            {
                Console.WriteLine("Could not load " + F + ". Make sure the file exists and it is readable.");
                throw new Exception();
            }
            try { LoadedMap = SnakeMap.Parse(Input, SnakeGame.FieldWidth, SnakeGame.FieldHeight); }
            catch
            {
                Console.WriteLine("The map " + F + " contains errors. Fix them and try again.");
                throw new Exception();
            }
            Console.WriteLine("Map loaded successfully.");
            Map = LoadedMap;
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
                TargetGame = new SnakeGame(Map);
                TargetGame.AddPlayer(e.Client);
                TargetGame.ID = CurrentID;
                TargetGame.GameOver += new SnakeGame.GameOverDelegate(TargetGame_GameOver);
                TargetGame.MessageLogged += new SnakeGame.LogMessageDelegate(TargetGame_MessageLogged);
                Games.Add(TargetGame);
                TargetGame_MessageLogged(TargetGame, new SnakeGame.LogEventArgs("First player connected"));
            }
            else
            {
                TargetGame.AddPlayer(e.Client);
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
