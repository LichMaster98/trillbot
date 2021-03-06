using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using JsonFlatFileDataStore;
using Newtonsoft.Json;
using trillbot.Classes;

namespace trillbot.Classes {

    public class slotMachineRunner {
        public SocketTextChannel Channel { get; set;}
        public int ID { get; set; }
        public string name { get; set;}
        public string description { get; set;}
        public int maxBet { get; set; }
        public int minBet { get; set;}
        public List<string> reels { get; set; }
        public List<List<int>> Weights { get; set; }
        public List<int> Payouts { get; set; }
        public ulong ChannelID { get; set; }

        public slotMachineRunner(SocketTextChannel Chan, slotMachine sm) {
            /* var sm = slotMachine.get_slotMachine(id);
            if(sm == null) {
                await Context.Channel.sendMessageAsyn(Context.User.Mention + ", you didn't pass a valid slot machine ID.");
                return;
            }*/
            Channel = Chan;
            this.ID = sm.ID;
            this.name = sm.name;
            this.description = sm.description;
            this.maxBet = sm.maxBet;
            this.minBet = sm.minBet;
            this.reels = sm.reels;
            this.Weights = sm.Weights;
            this.Payouts = sm.Payouts;
            this.ChannelID = Channel.Id;

        }

        private string displayReel(int[] rolls ) {
            if (rolls[0] < 0 || rolls[0] > reels.Count || rolls[1] < 0 || rolls[1] > reels.Count || rolls[2] < 0 || rolls[2] > reels.Count) {
                return "";
            } else {
                return reels[rolls[0]] + " | " + reels[rolls[1]] + " | " + reels[rolls[2]];
            }
        }

        public void rollSlot(int bet, ICommandContext Context) {
            
            var c = Character.get_character(Context.User.Id,Context.Guild.Id);
            if(c == null) {
                helpers.output(Channel,Context.User.Mention + ", You don't have an account. Create one with `ta!registeraccount`");
                return;
            }
            if(c.balance-bet < 0) {
                helpers.output(Channel,Context.User.Mention + ",You can't make a bet that brings you to a negative balance.");
                return;
            }
            if(bet < minBet || bet > maxBet) {
                helpers.output(Channel,Context.User.Mention + ", You can't make a bet larger than this machine's max bet OR a negative bet.");
                return;
            }
            c.balance -= bet;
            
            //Roll a Slot Roll
            List<string> str = new List<string>();
            str.Add("**" + this.name + "**");
            int[] rolls = new int[3];

            for(int i = 0; i < rolls.Length; i++) {
                var roll = Program.rand.Next(1,65); //Roll a number between 1 and 64
                int sum = 0;
                for(int j = 0; j < Weights[i].Count; j++) { //Find which reel to display from the weighted machine
                    sum += Weights[i][j];
                    if (roll <= sum) {
                        rolls[i] = j;
                        break;
                    }
                }
            }
            
            str.Add(displayReel(rolls)); //Comments Assume Default Emotes
            if(rolls[0] == 0 && rolls[1] == 0 && rolls[2] == 0) { //3 Moneybags
                str.Add("JACKPOT! You've Won " + Payouts[0]*bet + " Credits!");
                c.balance+=Payouts[0]*bet;
            } else if (rolls[0] == 1 && rolls[1] == 2 && rolls[2] == 3 ) { //One of each Fruit Variant 1
                str.Add("You've Won " + Payouts[1]*bet + " Credits!");
                c.balance+=Payouts[1]*bet;
            } else if (rolls[0] == 1 && rolls[1] == 3 && rolls[2] == 2) { //One of each Fruit Variant 2
                str.Add("You've Won " + Payouts[1]*bet + " Credits!");
                c.balance+=Payouts[1]*bet;
            } else if (rolls[0] == 2 && rolls[1] == 1 && rolls[2] == 3) { //One of each Fruit Variant 3
                str.Add("You've Won " + Payouts[1]*bet + " Credits!");
                c.balance+=Payouts[1]*bet;
            } else if (rolls[0] == 2 && rolls[1] == 3 && rolls[3] == 1) { //One of each Fruit Variant 4
                str.Add("You've Won " + Payouts[1]*bet + " Credits!");
                c.balance+=Payouts[1]*bet;
            } else if (rolls[0] == 3 && rolls[1] == 1 && rolls[2] == 2) { //One of each Fruit Variant 5
                str.Add("You've Won " + Payouts[1]*bet + " Credits!");
                c.balance+=Payouts[1]*bet;
            } else if (rolls[0] == 3 && rolls[1] == 2 && rolls[2] == 1) { //One of each Fruit Variant 6
                str.Add("You've Won " + Payouts[1]*bet + " Credits!");
                c.balance+=Payouts[1]*bet;
            } else if (rolls[0] == 1 && rolls[1] == 1 && rolls[2] == 1) { //Three Cherries
                str.Add("You've Won " + Payouts[2]*bet + " Credits!");
                c.balance+=Payouts[2]*bet;
            } else if (rolls[0] == 2 && rolls[1] == 2 && rolls[2] == 2) { //Three Grapes
                str.Add("You've Won " + Payouts[3]*bet + " Credits!");
                c.balance+=Payouts[3]*bet;
            } else if (rolls[0] == 3 && rolls[1] == 3 && rolls[2] == 3) { //Three Lemons
                str.Add("You've Won " + Payouts[4]*bet + " Credits!");
                c.balance+=Payouts[4]*bet;
            } else if ((rolls[0] == 1 || rolls[0] == 2 || rolls[0] == 3) && (rolls[1] == 1 || rolls[1] == 2 || rolls[1] == 3) && (rolls[2] == 1 || rolls[2] == 2 || rolls[2] == 3)) { //Three Fruits
                str.Add("You've Won " + Payouts[5]*bet + " Credits!");
                c.balance+=Payouts[5]*bet;
            } else if (rolls[0] == 4 && rolls[1] == 4 && rolls[2] == 4) { //Three Rose
                str.Add("You've Won " + Payouts[6]*bet + " Credits!");
                c.balance+=Payouts[6]*bet;
            } else if (rolls[0] == 5 && rolls[1] == 5 && rolls[2] == 5) {//Three Sunfowers
                str.Add("You've Won " + Payouts[7]*bet + " Credits!");
                c.balance+=Payouts[7]*bet;
            } else if(rolls[0] == 6 && rolls[1] == 6 && rolls[2] == 6) { //Three Hibiscous 
                str.Add("You've Won " + Payouts[8]*bet + " Credits!");
                c.balance+=Payouts[8]*bet;
            } else if ((rolls[0] == 4 || rolls[0] == 5 || rolls[0] == 6) && (rolls[1] == 4 || rolls[1] == 5 || rolls[1] == 6) && (rolls[2] == 4 || rolls[2] == 5 || rolls[2] == 6)) {//Three Flowers
                str.Add("You've Won " + Payouts[9]*bet + " Credits!");
                c.balance+=Payouts[9]*bet;
            } else if (rolls[0] != 7 && rolls[1] != 7 && rolls[2] != 7) { //Three non-pinapple symbols
                str.Add("You've Won " + Payouts[10]*bet + " Credits!");
                c.balance+=Payouts[10]*bet;
            } else {
                str.Add("You lost " + bet + " credits");
                Server s = Server.get_Server(Context.Guild.Id);
                if (s != null) { 
                    s.houseBal += bet;
                    Server.replace_Server(s);
                }
            }
            //Update Character JSON & Output results
            Character.update_character(c);
            helpers.output(Channel,String.Join(System.Environment.NewLine,str));
        }
        public string payouts() {
            List<string> str = new List<string>();
            str.Add("**" + this.name + " Payouts**");
            str.Add(this.description);
            str.Add("*Payouts assume a bet of 1 Imperial Credit.* This machines's max bet is: " + this.maxBet + ". This machine's minimum bet is: " + this.minBet);
            str.Add(reels[0] + " | " + reels[0] + " | " + reels[0] + ": " + Payouts[0]);
            str.Add(reels[1] + " | " + reels[2] + " | " + reels[3] + ": " + Payouts[1]);
            str.Add(reels[1] + " | " + reels[1] + " | " + reels[1] + ": " + Payouts[2]);
            str.Add(reels[2] + " | " + reels[2] + " | " + reels[2] + ": " + Payouts[3]);
            str.Add(reels[3] + " | " + reels[3] + " | " + reels[3] + ": " + Payouts[4]);
            str.Add(reels[1] + reels[2] + reels[3] + " | " + reels[1] + reels[2] + reels[3] + " | " + reels[1] + reels[2] + reels[3] + ": " + Payouts[5]);
            str.Add(reels[4] + " | " + reels[4] + " | " + reels[4] + ": " + Payouts[6]);
            str.Add(reels[5] + " | " + reels[5] + " | " + reels[5] + ": " + Payouts[7]);
            str.Add(reels[6] + " | " + reels[6] + " | " + reels[6] + ": " + Payouts[8]);
            str.Add(reels[4] + reels[5] + reels[6] + " | " + reels[4] + reels[5] + reels[6] + " | " + reels[4] + reels[5] + reels[6] + ": " + Payouts[9]);
            str.Add("Any three symbols which aren't " + reels[7] + ": " + Payouts[10]);
            return(String.Join(System.Environment.NewLine,str));
        }
    }

    public partial class slotMachine {

        [JsonProperty("Id")]
        public int ID {get; set; }
        [JsonProperty("Name")]
        public string name { get; set; }
        [JsonProperty("Description")]
        public string description { get; set; }
        [JsonProperty("Max Bet")]
        public int maxBet {get; set; }
        [JsonProperty("Min Bet")]
        public int minBet {get; set; }
        [JsonProperty("Reels")]
        public List<string> reels { get; set; } = new List<string>();
        [JsonProperty("Weights")]
        public List<List<int>> Weights { get; set; } = new List<List<int>>();
        [JsonProperty("Payouts")]
        public List<int> Payouts { get; set; } = new List<int>();
        [JsonProperty("Channel")]
        public ulong ChannelID {get; set;}

        /*public slotMachine(slotMachineRunner sm) {
            ID = sm.ID;
            name = sm.name;
            description = sm.description;
            maxBet = sm.maxBet;
            minBet = sm.minBet;
            reels = sm.reels;
            Weights = sm.Weights;
            Payouts = sm.Payouts;
            ChannelID = sm.ChannelID;
        }*/

        public string display(SocketGuild Guild) {
            var chan = Guild.GetTextChannel(ChannelID);
            return name + " slots. Min Bet: " + minBet + ". Max Bet: " + maxBet + ". Located in " + chan;
        }
    }

    public partial class slotMachine {
        public static slotMachine[] FromJson(string json) => JsonConvert.DeserializeObject<slotMachine[]>(json, Converter.Settings);

        public static List<slotMachine> get_slotMachine () {
            var store = new DataStore ("slotMachine.json");

            // Get employee collection
            var rtrner = store.GetCollection<slotMachine> ().AsQueryable ().ToList();
            store.Dispose();
            return rtrner;
        }

        public static slotMachine get_slotMachine (int id) {
            var store = new DataStore ("slotMachine.json");

            // Get employee collection
            var rtrner = store.GetCollection<slotMachine> ().AsQueryable ().FirstOrDefault (e => e.ID == id);
            store.Dispose();
            return rtrner;
        }

        public static slotMachine get_slotMachine (string name) {
            var store = new DataStore ("slotMachine.json");

            // Get employee collection
            var rtrner = store.GetCollection<slotMachine> ().AsQueryable ().FirstOrDefault (e => e.name == name);
            store.Dispose();
            return rtrner;
        }

        public static void insert_slotMachine (slotMachine slotMachine) {
            var store = new DataStore ("slotMachine.json");

            // Get employee collection
            store.GetCollection<slotMachine> ().InsertOneAsync (slotMachine);

            store.Dispose();
        }

        public static void update_slotMachine (slotMachine slotMachine) {
            var store = new DataStore ("slotMachine.json");

            store.GetCollection<slotMachine> ().ReplaceOneAsync (e => e.ID == slotMachine.ID, slotMachine);
            store.Dispose();
        }

        public static void delete_slotMachine (slotMachine slotMachine) {
            var store = new DataStore ("slotMachine.json");

            store.GetCollection<slotMachine> ().DeleteOne (e => e.ID == slotMachine.ID);
            store.Dispose();
        }
    }

}