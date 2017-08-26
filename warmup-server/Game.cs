using System;
using System.Reflection.Metadata.Ecma335;

namespace warmup_server
{
    public class Game
    {
        public delegate void EndGame(Client client1, int score1, Client client2, int senderScore);

        public delegate void UpdateGame(Client sender , Client receiver, int score);

        private EndGame endGame;
        private UpdateGame updateGame;

        private Client client1;
        private Client client2;
        private int score1;
        private int score2;
        private bool isEnded1;
        private bool isEnded2;
        private DateTime startTime;

        public Game(EndGame endGame, UpdateGame updateGame, Client client1, Client client2)
        {
            this.endGame = endGame;
            this.updateGame = updateGame;
            this.client1 = client1;
            this.client2 = client2;
            startTime = DateTime.Now;
        }

        public string getName(Client client)
        {
            if (client.Equals(client1)) return client1.name;
            if (client.Equals(client2)) return client2.name;
            Console.WriteLine("incorrect client");
            return null;
        }

        public void updateScoreServer(Client client, int score)
        {
            if (client.Equals(client1))
            {
                score1 = score;
                updateGame(client1 , client2, score1);
                return;
            }
            if (client.Equals(client2))
            {
                score2 = score;
                updateGame(client2 , client1 , score2);
                return;
            }
            Console.WriteLine("incorrect client");
        }

        public void endGameServer(Client client, int score)
        {
            if (client.Equals(client1))
            {
                score1 = score;
                isEnded1 = true;
            }
            else if (client.Equals(client2))
            {
                score2 = score;
                isEnded2 = true;
            }
            else
            {
                Console.WriteLine("incorrect client");
                return;
            }
            if (isEnded1 & isEnded2)
            {
                endGame(client1, score1, client2, score2);
            }
        }

        public void forceEndGame()
        {
            endGame(client1, score1, client2, score2);
        }
    }
}