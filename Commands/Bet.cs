using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Newtonsoft.Json;
using RestSharp;
using trillbot.Classes;

namespace trillbot.Commands
{
    public class Bet : ModuleBase<SocketCommandContext>
    {
        [Command("bet")]
        public async Task BetAsync(int ID, int amount)
        {
            SocketGuildUser usr = Context.Guild.GetUser(Context.Message.Author.Id);
            trillbot.Classes.Character char = character.get_character(Context.Message.Author.Id);
            racer racer = racer.get_racer(ID);
            string name = usr.Nickname != null ? usr.Nickname : usr.Username;

            if (char == null)
            {
                await ReplyAsync("Account not found. Please create one before proceeding via `tb!registeraccount`");
                return;
            }

            if(racer == null) {
                await ReplyAsync("The racer you selected doesn't exist.");
                return;
            }
            
            if (amount <= 0) {
                await ReplyAsync("You can't make a negative bet!");
                return;
            }

            trillbot.Classes.Bet b = new trillbot.Classes.Bet(racer.name,(long)amount);

            char.bets.Add(b);
            character.update_character(char);
            await ReplyAsync(name + ", you have placed the following bet: " + System.Environment.NewLine + b.ToString());

        }

        [Command("displaybets")]
        public async Task DisplaybetsAsync()
        {
            //Display bets to the User in a DM?
            SocketGuildUser usr = Context.Guild.GetUser(Context.Message.Author.Id);
            character character = character.get_character(Context.Message.Author.Id);

            if (character == null)
            {
                await ReplyAsync("Account not found. Please create one before proceeding via `tb!registeraccount`");
                return;
            }

            string output = character.name + "\n";

            foreach (trillbot.Classes.Bet bet in character.bets) {
                output+= bet.ToString() + "\n";
            }

            await ReplyAsync(output);
        }

        [Command("cancelbet")]
        public async Task CancelbetAsync(int ID)
        {
            //Allow a user to cancel a bet
            SocketGuildUser usr = Context.Guild.GetUser(Context.Message.Author.Id);
            character character = character.get_character(Context.Message.Author.Id);

            if (character == null)
            {
                await ReplyAsync("Account not found. Please create one before proceeding via `tb!registeraccount`");
                return;
            }

            foreach (trillbot.Classes.Bet bet in character.bets) {
                if (bet.Id == ID) {
                    character.bets.Remove(bet);
                    await ReplyAsync("Bet with ID: " + ID + "has been cancled.");
                    return;
                }
            }

            await ReplyAsync("Bet not found. You can see the list of bets you've made with `tb!displaybets`");
            return;
        }

        
    }
}