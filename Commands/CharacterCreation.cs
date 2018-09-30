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
    public class CharacterCreation : ModuleBase<SocketCommandContext>
    {
        [Command("registeraccount")]
        public async Task NewcharacterAsync(params string[] args)
        {
            SocketGuildUser usr = Context.Guild.GetUser(Context.Message.Author.Id);

            string name = usr.Nickname != null ? usr.Nickname : usr.Username;
            
            Classes.character character = new character
            {
                name = name
            };

            character.player_discord_id = Context.Message.Author.Id;

            character.insert_character(character);

            /*string serialized = Newtonsoft.Json.JsonConvert.SerializeObject(character);

            await System.IO.File.WriteAllTextAsync(name + ".json", serialized);

            RequestOptions opt = new RequestOptions
            {
                RetryMode = RetryMode.RetryRatelimit
            };*/

            await ReplyAsync(name + ", you have created an account. You can now use tb!bet <racer> <amount>");

        }

        [Command("deleteaccount")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task DeletecharacterAsync(params string[] args)
        {
            Classes.character character = character.get_character(Context.Message.Author.Id);

            if(character == null) {
                await ReplyAsync("No character found for you");
            } else {

                Classes.character.delete_character(character);
                await ReplyAsync("Account Deleted.");
            }
        }

        [Command("listaccounts")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task ListcharactersAsync()
        {
            List<Classes.character> characters = character.get_character();

            await ReplyAsync("**Racers for the Grand Prix**" +System.Environment.NewLine+string.Join(System.Environment.NewLine,characters.Select(e=>e.name).ToList()));
        }
    }
}
