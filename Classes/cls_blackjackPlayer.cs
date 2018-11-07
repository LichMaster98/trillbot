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

    public class blackjackPlayer {
        public ulong player_discord_id { get; set; }
        public string name { get; set; }
        public List<List<StandardCard>> hand { get; set; } = new List<List<StandardCard>>();
        public int bet { get; set; }
        public bool bust { get; set; }

        public blackjackPlayer(ulong ID, string n, int b) {
            player_discord_id = ID;
            name = n;
            bet = b;
        }

        public int handValue(int i = 1) {
            if (i < 0 || i >= hand.Count) return -1;
            else return handValue(hand[i]);
        }

        private int handValue(List<StandardCard> cards) {
            int value = 0;
            int numAces = 0;
            foreach(StandardCard c in cards) {
                if(c.value == 14) {
                    numAces++;
                    value += 11;
                } else if(c.value > 10) {
                    value += 10;
                } else {
                    value += c.value;
                }
            }
            while(numAces > 0 && value > 21) {
                value -= 10;
                numAces--;
            }
            return value;
        }

        public string handDisplay() {
            List<string> str = new List<string>();
            str.Add("**" + name + "'s Hands**");
            for(int i = 0; i < hand.Count; i++) {
                str.Add("*Hand " + i + "*");
                str.Add(handDisplay(hand[i]));
            }
            return String.Join(System.Environment.NewLine,str);
        }
        private string handDisplay(List<StandardCard> cards) {
            List<string> str = new List<string>();
            foreach(var c in cards) {
                str.Add(c.ToString());
            }
            str.Add(handValue(cards).ToString());
            return String.Join(" | ", str);
        }
    }
}