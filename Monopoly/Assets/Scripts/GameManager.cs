using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;

namespace Monopoly
{
    public class GameManager : MonoBehaviourPunCallbacks, IOnEventCallback
    {
        public static GameManager instance;

        public const byte SendNewActivityLineCode = 1;
        public const byte PlayerTurnCode = 2;
        public const byte ReceiveMoneyCode = 3;
        public const byte PropertyChangeCode = 4;
    
        public Dice dice;
        public Board board;

        public int currentPlayer;
        public Player[] players;

        public GameObject cannon;
        public GameObject car;
        public GameObject iron;
        public GameObject locomotive;
        public GameObject thimble; 
        public GameObject topHat;
        public GameObject wheelbarrow;
        public GameObject[] pieces;

        public GameObject house;
        public GameObject hotel;

        public GameObject PlayerUi;
        public GameObject popUpWindow;
        public GameObject noMoneyOptions;
        
        void Awake()
        {
            instance = this;
        }

        void Start ()
        {
            pieces = new GameObject[] {cannon, car, iron, locomotive, thimble, topHat, wheelbarrow};
            currentPlayer = 0;
            players = PhotonNetwork.PlayerList;

            DisableButton("buyPropertyButton");
            DisableButton("buildHouseButton");
            DisableButton("sellPropertyButton");
            DisableButton("endTurnButton");
            
            SetupBoard();

            if (PlayerManager.LocalPlayerInstance == null)
            {
                // we're in a room. spawn a character for the local player. it gets synced by using PhotonNetwork.Instantiate
                int pieceNum = (int)PhotonNetwork.LocalPlayer.CustomProperties["Gamepiece"];
                PhotonNetwork.Instantiate(pieces[pieceNum].name, board.locations[0].gridPoint, Quaternion.identity, 0);
            }

            if (PhotonNetwork.IsMasterClient) {
                NextPlayer();
            }
        }

        void Update()
        {
            if (HasProperties()) 
            {
                EnableButton("sellPropertyButton");
            }
            else {
                DisableButton("sellPropertyButton");
            }
        }

        private void SetupBoard()
        {
            board.SetUpLocations();
            board.SetUpCards();
        }

        public void Move(int steps)
        {
            int location;
            
            location = PlayerManager.location;
            location+=steps;
            if (location > 39)
            {
                location = location - 39;
            }
            MoveToLocation(location);
            
            LandedOn(steps);
        }

        public void MoveToLocation(int location)
        {
            PlayerManager.location = location;
        }

        public void LandedOn(int? diceRoll = null)
        {
            string landedOnText = "landed on " + board.locations[PlayerManager.location].name;
            SendActivityMessage(landedOnText, currentPlayer);

            if (board.locations[PlayerManager.location] is Property) {
                Property currentLocation = (Property)board.locations[PlayerManager.location];
                // Unowned
                if (currentLocation.owner == null) {
                    if (PlayerManager.balance >= currentLocation.price) {
                        EnableButton("buyPropertyButton");
                    }
                } else { // Owned
                    if (currentLocation.owner != players[currentPlayer]) { // By other
                        bool paid = PayMoney(currentLocation.rent, currentLocation.owner);
                        if (paid) {
                            string text = "paid $" + currentLocation.rent + " in rent to " + currentLocation.owner.NickName;
                            SendActivityMessage(text, currentPlayer);
                        }
                    } else { // By self
                        if (PlayerManager.balance >= currentLocation.price) {
                            if (currentLocation.numHouses < 5) {
                                EnableButton("buildHouseButton");
                            }
                        }
                    }
                }
            } 
            else if (board.locations[PlayerManager.location] is Railroad) {
                Railroad currentLocation = (Railroad)board.locations[PlayerManager.location];
                if (currentLocation.owner == null) {
                    if (PlayerManager.balance >= currentLocation.price) {
                        EnableButton("buyPropertyButton");
                    }
                } else { // Owned
                    if (currentLocation.owner != players[currentPlayer]) { // By other
                        int rent = currentLocation.GetRent();
                        bool paid = PayMoney(rent, currentLocation.owner);
                        if (paid) {
                            string text = "paid $" + rent + " in rent to " + currentLocation.owner.NickName;
                            SendActivityMessage(text, currentPlayer);
                        }
                    }
                }
            }
            else if (board.locations[PlayerManager.location] is Utility) {
                Utility currentLocation = (Utility)board.locations[PlayerManager.location];
                if (currentLocation.owner == null) {
                    if (PlayerManager.balance >= currentLocation.price) {
                        EnableButton("buyPropertyButton");
                    }
                } else { // Owned
                    if (currentLocation.owner != players[currentPlayer]) { // By other
                        int rent = currentLocation.GetRent((int)diceRoll);
                        bool paid = PayMoney(rent, currentLocation.owner);
                        if (paid) {
                            string text = "paid $" + rent + " in rent to " + currentLocation.owner.NickName;
                            SendActivityMessage(text, currentPlayer);
                        }
                    }
                }
            }
            else if (board.locations[PlayerManager.location] is Tax) {
                Tax currentLocation = (Tax)board.locations[PlayerManager.location];
                bool paid = PayMoney(currentLocation.tax);
                if (paid) {
                    string text = "paid $" + currentLocation.tax + " in taxes";
                    SendActivityMessage(text, currentPlayer);
                }
            }
            else if (board.locations[PlayerManager.location] is Location) {
                switch(board.locations[PlayerManager.location].name) {
                    case "GO":
                        ReceiveMoney(200);
                        break;
                    case "Jail":
                        break;
                    case "Free Parking":
                        break;
                    case "Go To Jail":
                        MoveToLocation(10);
                        LandedOn();

                        bool paid = PayMoney(50);
                        if (paid) {
                            int amountPaid = 50;
                            string paidMessage = "paid $" + amountPaid.ToString();
                            SendActivityMessage(paidMessage, currentPlayer);
                        }
                        break;
                    case "Chance":
                        Card drawnCard = board.DrawChance();
                        string chanceCardText = "Chance card: " + drawnCard.description;
                        SendActivityMessage(chanceCardText);
                        
                        // If receive money
                        if (drawnCard.amountMoney > 0) {
                            ReceiveMoney(drawnCard.amountMoney);
                            string receivedMessage = "received $" + drawnCard.amountMoney.ToString();
                            SendActivityMessage(receivedMessage, currentPlayer);
                        } else if (drawnCard.amountMoney < 0) {
                            bool paid2 = PayMoney(Math.Abs(drawnCard.amountMoney));
                            if (paid2) {
                                string paidMessage = "paid $" + Math.Abs(drawnCard.amountMoney).ToString();
                                SendActivityMessage(paidMessage, currentPlayer);
                            }
                        }

                        if (drawnCard.moveTo != null) {
                            MoveToLocation((int)drawnCard.moveTo);
                            LandedOn();
                        }
                        break;
                    case "Community Chest":
                        Card drawnCard2 = board.DrawCommunityChest();
                        string communityChestCardText = "Community Chest card: " + drawnCard2.description;
                        SendActivityMessage(communityChestCardText);
                        
                        // If receive money
                        if (drawnCard2.amountMoney > 0) {
                            ReceiveMoney(drawnCard2.amountMoney);
                            string receivedMessage2 = "received $" + drawnCard2.amountMoney.ToString();
                            SendActivityMessage(receivedMessage2, currentPlayer);
                        } else if (drawnCard2.amountMoney < 0) {
                            bool paid3 = PayMoney(Math.Abs(drawnCard2.amountMoney));
                            if (paid3) {
                                string paidMessage2 = "paid $" + Math.Abs(drawnCard2.amountMoney).ToString();
                                SendActivityMessage(paidMessage2, currentPlayer);
                            }
                        }

                        if (drawnCard2.moveTo != null) {
                            MoveToLocation((int)drawnCard2.moveTo);
                            LandedOn();
                        }
                        break;
                }
            }
        }

        public void NextPlayer()
        {
            DisableButton("buyPropertyButton");
            DisableButton("buildHouseButton");
            DisableButton("endTurnButton");

            // Check how many players are bankrupt
            List<Player> notBankrupt = new List<Player>();
            foreach (Player player in players) {
                if ((bool)player.CustomProperties["Bankrupt"] == false) {
                    notBankrupt.Add(player);
                }
            }

            if (notBankrupt.Count == 0) {
                EndGame(null);
                return;
            } else if (notBankrupt.Count == 1) { // Only one person left, they are the winner
                EndGame(notBankrupt[0]);
                return;
            }

            if (currentPlayer == PhotonNetwork.CurrentRoom.PlayerCount-1) {
                currentPlayer = 0;
            } else {
                currentPlayer++;
            }

            if ((bool)players[currentPlayer].CustomProperties["Bankrupt"]) {
                NextPlayer();
                return;
            }

            SendEvent(PlayerTurnCode, currentPlayer);
        }

        public void StartTurn()
        {
            if (players[currentPlayer] == PhotonNetwork.LocalPlayer)
            {
                popUpWindow.SetActive(true);
                popUpWindow.SendMessage("DisplayText", "It's your turn! Press space to roll the dice", SendMessageOptions.RequireReceiver);
                StartCoroutine(WaitForDiceRoll(diceNum => {
                    EnableButton("endTurnButton");
                    Move(diceNum);
                }));
            }
            else {
                string text = "It's " + players[currentPlayer].NickName + "'s turn!";
                popUpWindow.SetActive(true);
                popUpWindow.SendMessage("DisplayText", text, SendMessageOptions.RequireReceiver);
            }
        }

        public void ReceiveMoney(int amount)
        {
            PlayerManager.balance += amount;
        }

        public bool PayMoney(int amount, Player recipient = null)
        {

            if (PlayerManager.balance < amount) {
                NoMoney(amount);
                return false;
            }

            PlayerManager.balance -= amount;
            if (recipient != null) {
                int[] targetActors = {recipient.ActorNumber};
                SendEvent(ReceiveMoneyCode, amount, targetActors);
            }
            return true;
        }

        public void NoMoney(int amount)
        {
            DisableButton("buyPropertyButton");
            DisableButton("buildHouseButton");
            DisableButton("sellPropertyButton");
            DisableButton("endTurnButton");

            int amountFromProperties = 0;
            bool[] ownedProperties = (bool[])PhotonNetwork.LocalPlayer.CustomProperties["OwnedProperties"];
            for (int i = 0; i < 40; i++) {
                if (ownedProperties[i] == true) {
                    if (board.locations[i] is Property) {
                        Property property = (Property)board.locations[i];
                        amountFromProperties += (int)(0.5*property.price);
                        amountFromProperties += (int)(0.5*property.housePrice*property.numHouses);
                    }
                    else if (board.locations[i] is Utility) {
                        Utility property = (Utility)board.locations[i];
                        amountFromProperties += (int)(0.5*property.price);
                    }
                    else {
                        Railroad property = (Railroad)board.locations[i];
                        amountFromProperties += (int)(0.5*property.price);
                    }
                }
            }

            if (amountFromProperties >= amount) {
                PlayerManager.noMoneyAmount = amount;
                noMoneyOptions.SetActive(true);
            } else {
                Bankrupt();
            }
        }

        public bool HasProperties()
        {
            bool[] ownedProperties = (bool[])PhotonNetwork.LocalPlayer.CustomProperties["OwnedProperties"];
            for (int i = 0; i < 40; i++) {
                if (ownedProperties[i] == true) {
                    return true;
                }
            }
            return false;
        }

        public void SoldPropertyNoMoney()
        {
            if (PlayerManager.balance >= PlayerManager.noMoneyAmount) {
                string paidMessage = "paid $" + PlayerManager.noMoneyAmount.ToString();
                SendActivityMessage(paidMessage, currentPlayer);
                PlayerManager.balance -= PlayerManager.noMoneyAmount;
                PlayerManager.noMoneyAmount = 0;
                
                EnableButton("sellPropertyButton");
                EnableButton("endTurnButton");
            }
            else {
                NoMoney(PlayerManager.noMoneyAmount);
            }
        }

        public void Bankrupt()
        {
            // Set custom player property as bankrupt
            ExitGames.Client.Photon.Hashtable hash = new ExitGames.Client.Photon.Hashtable();
            hash.Add("Bankrupt", true);
            PhotonNetwork.LocalPlayer.SetCustomProperties(hash);
            SendActivityMessage("went bankrupt!", currentPlayer);
            NextPlayer();
        }

        public void EndGame(Player winner)
        {
            if (winner == null) {
                WinnerScene.winner = "Game over - no winners";
            }
            else if (winner == PhotonNetwork.LocalPlayer) {
                WinnerScene.winner = "You are the winner!";
            } else {
                WinnerScene.winner = winner.NickName + " is the winner!";
            }
            PhotonNetwork.LeaveRoom();
            SceneManager.LoadScene(2);
        }
        
        private IEnumerator WaitForDiceRoll(System.Action<int> callback) {
        
            // wait for player to press space
            yield return WaitForKeyPress(KeyCode.Space); // wait for this function to return
        
            // Roll dice after player presses space
            dice.RollDice();
            while (dice.currentNum == -1) {
                yield return new WaitForSeconds(3);
            }

            callback(dice.currentNum); // Use callback to do something with result
        }
        
        private IEnumerator WaitForKeyPress(KeyCode key)
        {
            bool done = false;
            while(!done) // essentially a "while true", but with a bool to break out naturally
            {
                if(Input.GetKeyDown(key))
                {
                    done = true; // breaks the loop
                }
                yield return null; // wait until next frame, then continue execution from here (loop continues)
            }
        
            // now this function returns
        }

        public override void OnPlayerLeftRoom(Player other)
        {
            string text = other.NickName + " has left the game.";
            PlayerUi.SendMessage ("AddActivityText", text, SendMessageOptions.RequireReceiver);

            if (PhotonNetwork.IsMasterClient) {
                if (players[currentPlayer] == other) {
                    NextPlayer();
                }
            }
            players = PhotonNetwork.PlayerList;
        }

        public void SendActivityMessage(string content, int? player = null)
        {
            object[] data = new object[] {player, content};
            SendEvent(SendNewActivityLineCode, data);
        }

        public void ReceiveActivityMessage(object[] arr)
        {
            string content = (string)arr[1];
            if (arr[0] == null) {
                PlayerUi.SendMessage ("AddActivityText", content, SendMessageOptions.RequireReceiver);
            } else {
                Player player = players[(int)arr[0]];
                string text;
                if (player == PhotonNetwork.LocalPlayer) {
                    text = "You " + content;
                } else {
                    text = player.NickName + " " + content;
                }
                PlayerUi.SendMessage ("AddActivityText", text, SendMessageOptions.RequireReceiver);
            }
        }

        public void SendEvent(byte eventCode, object content, int[] targetActors = null)
        {
            RaiseEventOptions raiseEventOptions = raiseEventOptions = new RaiseEventOptions { Receivers = ReceiverGroup.All }; // Set Receivers to All to receive this event on the local client as well
            if (targetActors != null && targetActors.Length != 0) {
                raiseEventOptions = new RaiseEventOptions { TargetActors = targetActors }; // Send to specific clients
            }
            PhotonNetwork.RaiseEvent(eventCode, content, raiseEventOptions, SendOptions.SendReliable);
        }
        public void OnEvent(EventData photonEvent)
        {
            byte eventCode = photonEvent.Code;
            switch (eventCode) {
                case SendNewActivityLineCode:
                    object[] arr = (object[])photonEvent.CustomData;
                    ReceiveActivityMessage(arr);
                    break;
                case PlayerTurnCode:
                    currentPlayer = (int)photonEvent.CustomData;
                    StartTurn();
                    break;
                case ReceiveMoneyCode:
                    int amount = (int)photonEvent.CustomData;
                    PlayerManager.balance += amount;
                    break;
                case PropertyChangeCode:
                    object[] data = (object[])photonEvent.CustomData;
                    int location = (int)data[0];
                    string property = (string)data[1];

                    if (board.locations[location] is Utility) {
                        Utility temp = (Utility)board.locations[location];
                        if (property == "owner") {
                            if (data[2] == null) {
                                temp.owner = null;
                            } else temp.owner = players[(int)data[2]];
                        }
                        board.locations[location]= temp;
                    } else if (board.locations[location] is Railroad) {
                        Railroad temp = (Railroad)board.locations[location];
                        if (property == "owner") {
                            if (data[2] == null) {
                                temp.owner = null;
                            } else temp.owner = players[(int)data[2]];
                        }
                        board.locations[location]= temp;
                    } else if (board.locations[location] is Property) {
                        Property temp = (Property)board.locations[location];
                        if (property == "owner") {
                            if (data[2] == null) {
                                temp.owner = null;
                            } else temp.owner = players[(int)data[2]];
                            board.locations[location]= temp;
                        } else if (property == "buildHouse") {
                            temp.BuildHouse();
                        } else if (property == "reset") {
                            temp.Reset();
                        }
                        board.locations[location]= temp;
                    }
                    break;
            }
        }

        public void buyProperty()
        {
            if (board.locations[PlayerManager.location] is Property) {
                Property boughtProperty = (Property)board.locations[PlayerManager.location];
                PlayerManager.balance -= boughtProperty.price; // Subtract price from balance
            }
            else if (board.locations[PlayerManager.location] is Utility) {
                Utility boughtProperty = (Utility)board.locations[PlayerManager.location];
                PlayerManager.balance -= boughtProperty.price; // Subtract price from balance
            }
            else { //Railroad
                Railroad boughtProperty = (Railroad)board.locations[PlayerManager.location];
                PlayerManager.balance -= boughtProperty.price; // Subtract price from balance
            }

            // Change our ownedProperties CustomProperty
            bool[] ownedProperties = (bool[])PhotonNetwork.LocalPlayer.CustomProperties["OwnedProperties"];
            ownedProperties[PlayerManager.location] = true;
            ExitGames.Client.Photon.Hashtable hash = new ExitGames.Client.Photon.Hashtable();
            hash.Add("OwnedProperties", ownedProperties);
            PhotonNetwork.LocalPlayer.SetCustomProperties(hash);

            // This will change the property on our local player since the message is also sent to us
            object[] data = new object[] {PlayerManager.location, "owner", currentPlayer};
            SendEvent(PropertyChangeCode, data);
            // Send activity message as well
            string text = "bought " + board.locations[PlayerManager.location].name;
            SendActivityMessage(text, currentPlayer);

            DisableButton("buyPropertyButton");
        }

        public void buildHouse()
        {
            Property property = (Property)board.locations[PlayerManager.location];
            PlayerManager.balance -= property.housePrice; // Subtract price from balance
            int numHouses = property.numHouses + 1;

            // This will change the property on our local player since the message is also sent to us
            object[] data = new object[] {PlayerManager.location, "buildHouse"};
            SendEvent(PropertyChangeCode, data);

            // Send activity message as well
            string text = "built a house on " + property.name;
            SendActivityMessage(text, currentPlayer);
            DisableButton("buildHouseButton");
        }

        public void SellProperty(int property)
        {
            if (board.locations[PlayerManager.location] is Property) {
                Property boughtProperty = (Property)board.locations[PlayerManager.location];
                // Sells for half price - plus houses at half price
                PlayerManager.balance += (int)(0.5*boughtProperty.price);
                PlayerManager.balance += (int)(0.5*boughtProperty.housePrice*boughtProperty.numHouses);

                // Send message to reset property
                // This will change the property on our local player since the message is also sent to us
                object[] data = new object[] {PlayerManager.location, "reset"};
                SendEvent(PropertyChangeCode, data);
            }
            else if (board.locations[PlayerManager.location] is Utility) {
                Utility boughtProperty = (Utility)board.locations[PlayerManager.location];
                PlayerManager.balance += (int)(0.5*boughtProperty.price);
            }
            else { //Railroad
                Railroad boughtProperty = (Railroad)board.locations[PlayerManager.location];
                PlayerManager.balance += (int)(0.5*boughtProperty.price);
            }

            // Change our ownedProperties CustomProperty
            bool[] ownedProperties = (bool[])PhotonNetwork.LocalPlayer.CustomProperties["OwnedProperties"];
            ownedProperties[PlayerManager.location] = false;
            ExitGames.Client.Photon.Hashtable hash = new ExitGames.Client.Photon.Hashtable();
            hash.Add("OwnedProperties", ownedProperties);
            PhotonNetwork.LocalPlayer.SetCustomProperties(hash);

            // This will change the property on our local player since the message is also sent to us
            object[] data2 = new object[] {PlayerManager.location, "owner", null};
            SendEvent(PropertyChangeCode, data2);
            
            // Send activity message as well
            string text = "sold " + board.locations[PlayerManager.location].name;
            int localPlayer = 0;
            for (int i = 0; i < PhotonNetwork.CurrentRoom.PlayerCount; i++) {
                if (players[i] == PhotonNetwork.LocalPlayer) {
                    localPlayer = i;
                }
            }
            SendActivityMessage(text, localPlayer);
        }

        private void DisableButton(string button)
        {
            PlayerUi.SendMessage ("DisableButton", button, SendMessageOptions.RequireReceiver);
        }

        private void EnableButton(string button)
        {
            PlayerUi.SendMessage ("EnableButton", button, SendMessageOptions.RequireReceiver);
        }

        private void OnEnable()
        {
            PhotonNetwork.AddCallbackTarget(this);
        }

        private void OnDisable()
        {
            PhotonNetwork.RemoveCallbackTarget(this);
        }
    }
}