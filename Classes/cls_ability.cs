// Generated by https://quicktype.io
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

namespace trillbot.Classes
{

    public partial class Ability
    {
        [JsonProperty("id")]
        public long ID { get; set; }
        [JsonProperty("title")]
        public string Title { get; set; }
        [JsonProperty("description")]
        public string Description { get; set; }
        [JsonProperty("active")]
        public bool Active { get; set; }
    }

    public partial class Ability
    {
        public static Ability[] FromJson(string json) => JsonConvert.DeserializeObject<Ability[]>(json, Converter.Settings);
        public static List<Ability> get_ability () {
            var store = new DataStore ("ability.json");

            // Get employee collection
            return store.GetCollection<Ability> ().AsQueryable ().ToList();
        }

        public static Ability get_ability (int id) {
            var store = new DataStore ("ability.json");

            // Get employee collection
            return store.GetCollection<Ability> ().AsQueryable ().FirstOrDefault (e => e.ID == id);
        }

        public static Ability get_ability (string name) {
            var store = new DataStore ("ability.json");

            // Get employee collection
            return store.GetCollection<Ability> ().AsQueryable ().FirstOrDefault (e => e.Title == name);
        }

        public static void insert_ability (Ability ability) {
            var store = new DataStore ("ability.json");

            // Get employee collection
            store.GetCollection<Ability> ().InsertOneAsync (ability);

            store.Dispose();
        }

        public static void replace_ability (Ability ability) {
            var store = new DataStore ("ability.json");

            store.GetCollection<Ability> ().ReplaceOneAsync (e => e.ID == ability.ID, ability);
            store.Dispose();
        }

        public static void delete_card (Ability ability) {
            var store = new DataStore ("ability.json");

            store.GetCollection<Card> ().DeleteOne (e => e.ID == ability.ID);
            store.Dispose();
        }
    }

}
