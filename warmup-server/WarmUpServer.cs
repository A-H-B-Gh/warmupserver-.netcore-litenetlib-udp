using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using LiteNetLib;
using LitJson;

namespace warmup_server
{
    public class WarmUpServer
    {
        private int timeOfGame = 60; //todo move this.
        private int maxDelayAfterGame = 5;
        private Server server;
//        private Dictionary<string, Game> games;
        private Dictionary<string, Client> clients;
        private Dictionary<string, Client> pendingUsers;
        
        public WarmUpServer()
        {
            server = new Server( "warmup" , 3002 , 1, 100000 , 5000 );
//            games = new Dictionary<string, Game>();
            clients = new Dictionary<string, Client>();
            pendingUsers = new Dictionary<string, Client>();
            Initialize();
        }

        private void Initialize()
        {
            server.OnConnected(onConnectedEvent);
            server.OnDisconnected(onDisconnectedEvent);
            server.On("request_game",onRequestGame);
            server.On("unrequest_game",onUnrequestGame);
            server.On("update_game",onUpdateGame);
            server.On("end_game",onEndGame);
        }

        private void onRequestGame(NetPeer peer, JsonData data)
        {
            Client client = clients[peer.ConnectId.ToString()];
            if (client.status != ClientStatus.Idle) return;
            if (checkForSameInPending(peer.ConnectId.ToString())) return; //this can be removed
            Console.WriteLine("request game recieved");
            string name = data["name"].ToString();
            Client temp = clients[peer.ConnectId.ToString()];
            temp.setName(name);
            addToPendingList(peer.ConnectId.ToString(), temp);
            server.Emit( peer , createJson("request_game_accepted"));
            client.status = ClientStatus.Searching;
            checkForConnecting();
        }

        private void onUnrequestGame(NetPeer peer, JsonData data)
        {
            Client client = clients[peer.ConnectId.ToString()];
            if (client.status != ClientStatus.Searching) return;
            Console.WriteLine("unrequest game received");
            removeFromPendingList(peer.ConnectId.ToString());
            server.Emit( peer , createJson("unrequest_game_accepted"));
            client.status = ClientStatus.Idle;   
        }

        private void onUpdateGame(NetPeer peer, JsonData data)
        {
            Client client = clients[peer.ConnectId.ToString()];
            if(client.status != ClientStatus.InGame) return ;
            int score = Convert.ToInt32(data["score"].ToString());
            client.currentGame.updateScoreServer(client , score);
        }

        private void onEndGame(NetPeer peer, JsonData data)
        {
            Client client = clients[peer.ConnectId.ToString()];
            if(client.status != ClientStatus.InGame) return ;
            int score = Convert.ToInt32(data["score"].ToString());
            client.currentGame.endGameServer(client , score);
        }
        
        private void updateScoreCallBack (Client sender, Client receiver, int senderScore ) {
            if (receiver.peer.ConnectionState == ConnectionState.Connected)
            {
                Dictionary<string, string> data = new Dictionary<string, string> {["opscore"] = senderScore.ToString()};
                server.Emit(receiver.peer,createJson("update_game", data));
            }
            if (sender.peer.ConnectionState == ConnectionState.Connected)
                server.Emit(sender.peer,createJson("updated"));
        }
        
        private void endGameCallBack (Client client1 , int score1 , Client client2, int score2) {
            if (client1.peer.ConnectionState == ConnectionState.Connected)
            {
                Dictionary<string, string> data = new Dictionary<string, string> {["opscore"] = score2.ToString()};
                server.Emit(client1.peer,createJson("end_game" , data));
            }
            if (client2.peer.ConnectionState == ConnectionState.Connected)
            {
                Dictionary<string, string> data = new Dictionary<string, string> {["opscore"] = score1.ToString()};
                server.Emit(client2.peer,createJson("end_game" , data));
            }
            client1.currentGame = null;
            client2.currentGame = null;
            if (client1.peer.ConnectionState == ConnectionState.Connected) client1.status = ClientStatus.Idle;
            if (client2.peer.ConnectionState == ConnectionState.Connected) client2.status = ClientStatus.Idle;
        }
        
        private bool checkForSameInPending (string clientId)
        {
            return pendingUsers.ContainsKey(clientId);
        }
        
        private void addToPendingList (string clientId , Client client) {
            pendingUsers.Add(clientId, client);
        }
        
        private void checkForConnecting (){
            if (pendingUsers.Count < 2)
                return;
            Dictionary<string,Client>.Enumerator pendingEnumerator = pendingUsers.GetEnumerator();
            pendingEnumerator.MoveNext();
            KeyValuePair<string,Client> client1 = pendingEnumerator.Current;
            pendingEnumerator.MoveNext();
            KeyValuePair<string,Client> client2 = pendingEnumerator.Current;
            startGame(client1.Value, client2.Value);
            pendingUsers.Remove(client1.Key);
            pendingUsers.Remove(client2.Key);
            pendingEnumerator.Dispose();
        }
        
        private void startGame (Client user1 , Client user2 ){
            Console.WriteLine(user1.peer);
            Game game = new Game(endGameCallBack, updateScoreCallBack, user1, user2);
            Console.WriteLine(user1.peer.ConnectionState);
            if (user1.peer.ConnectionState == ConnectionState.Connected)
            {
                Dictionary<string, string> data = new Dictionary<string, string> 
                    {["opname"] = user2.name , ["timeofgame"] = timeOfGame.ToString()};
                server.Emit(user1.peer,createJson("accept_game" , data));
                user1.status = ClientStatus.InGame;
                user1.currentGame = game;
            }
            if (user2.peer.ConnectionState == ConnectionState.Connected)
            {
                Dictionary<string, string> data = new Dictionary<string, string> 
                    {["opname"] = user1.name , ["timeofgame"] = timeOfGame.ToString()};
                server.Emit(user2.peer,createJson("accept_game" , data));
                user2.status = ClientStatus.InGame;
                user2.currentGame = game;
            }
            Execute(forceEndGame, (maxDelayAfterGame + timeOfGame)*1000 , game);
            Console.WriteLine("game started");
        }
        
        private void removeFromPendingList (string clientId)
        {
            if (pendingUsers.ContainsKey(clientId))
                pendingUsers.Remove(clientId);
        }

        public delegate void ForceEndGame(Game game);

        private void forceEndGame(Game game)
        {
            game.forceEndGame();
        }
        
        public async Task Execute(ForceEndGame forceEndGame, int timeoutInMilliseconds , Game game)
        {
            await Task.Delay(timeoutInMilliseconds);
            forceEndGame(game);
        }

        private void onConnectedEvent(NetPeer netPeer)
        {
            clients.Add(netPeer.ConnectId.ToString() ,new Client(netPeer , ClientStatus.Idle, null));
        }

        private void onDisconnectedEvent(NetPeer netPeer)
        {
            if (pendingUsers.ContainsKey(netPeer.ConnectId.ToString()))
                pendingUsers.Remove(netPeer.ConnectId.ToString());
            clients.Remove(netPeer.ConnectId.ToString());
        }

        private string createJson(string eventNum, Dictionary<string, string> data)
        {   
            if (data == null || data.Count == 0)
            {
                return createJson(eventNum);
            }
            return string.Format("[\"{0}\",{1}]", eventNum, new JSONObject(data));
        }
        
        private string createJson(string eventNum)
        {
            return string.Format("[\"{0}\"]", eventNum);
        }
        
    }
}