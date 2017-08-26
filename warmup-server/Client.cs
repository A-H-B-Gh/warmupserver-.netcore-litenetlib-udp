using System.Reflection.Metadata.Ecma335;
using LiteNetLib;

namespace warmup_server
{
    public enum ClientStatus {Idle , Searching , InGame, GameEnded}
    
    public class Client
    {
        public NetPeer peer;
        public ClientStatus status;
        public string name;
        public Game currentGame;

        public Client(NetPeer peer, ClientStatus status, Game currentGame)
        {
            this.peer = peer;
            this.status = status;
            this.currentGame = currentGame;
        }

        public void setName(string name)
        {
            this.name = name;
        }
    }
}