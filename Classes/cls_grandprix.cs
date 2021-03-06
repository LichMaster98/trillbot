using System;
using System.Collections.Generic;
using System.Linq;
using Discord.Commands;
using Discord.WebSocket;

namespace trillbot.Classes {
    public class GrandPrix {
        private List<racer> racers = new List<racer>();
        private Stack<Card> cards = new Stack<Card>();
        private bool runningGame = false;
        private int position = 0;
        private int round = 1;
        public string channelName { get; set; } = "";
        private static readonly Dictionary<int, Tuple<int,int,int>> remedy_to_hazards = new Dictionary<int, Tuple<int,int,int>> {
            {0,new Tuple<int,int,int>(5,6,-1)},
            {1,new Tuple<int,int,int>(8,9,17)},
            {2,new Tuple<int,int,int>(10,11,-1)}
        };
        private static readonly Dictionary<int, string> target_hazard_output = new Dictionary<int, string> {
            {0, ". They are unable to move until they remove this Hazard."},
            {1, ". They better not have any other Hazards applied!"},
            {2,". They have 3 turns to remove this Hazard."},
            {3, ". They are unable to move more than two spaces!"}
        };
        private static readonly Dictionary<long,int> ability_to_save = new Dictionary<long, int> {
            {2, 5},
            {3, 6},
            {4, 8},
            {5, 9},
            {6, 10}
        };
        private static int[] lengths = {15, 8, 7, 15}; // Center Leadboard Values
        private static int[] wealthyIDs = {3, 4, 15, 16}; // Card IDs for Wealthy Sponsor 3, 4, 15, 16
        private textVersion tV = textVersion.get_textVersion(0);

        public GrandPrix() {
            channelName = "Null";
        }

        public GrandPrix(string cn, int version) {
            var vers =  textVersion.get_textVersion(version);
            if (vers != null) tV = vers;
            channelName = cn;
        }

        //Private Helper Functions
        //Leaderboard Centering Math
        private void leaderCenter() {
            foreach (racer r in racers) {
                for(int i = 0; i < 4; i++) {
                    switch (i) {
                        case 0:
                        if(r.nameID().Length > lengths[i]) lengths[i] = r.nameID().Length;
                        break;
                        case 1:
                        if(tV.leaderBoardAlive(true).Length > tV.leaderBoardAlive(false).Length) lengths[i]  = tV.leaderBoardAlive(true).Length;
                        else lengths[i]  = tV.leaderBoardAlive(false).Length;
                        break;
                        case 2:
                        if(r.faction.Length > lengths[i]) lengths[i] = r.faction.Length;
                        break;
                        case 3:
                        if(r.ability.Title.Length > lengths[i]) lengths[i] = r.ability.Title.Length;
                        break;
                        default:
                        break;
                    }
                }
            }
        }

        //Hazard Output
        private static string targetHazard(racer racer, racer target, Card card) {
            target.addHazard(card);
            return (racer.name + " played a " + card.title + " against " + target.name + target_hazard_output[(int)card.value]);
        }

        //Show hand
        public void showHand(SocketCommandContext Context) {
            var r = racers.FirstOrDefault(e=> e.player_discord_id == Context.Message.Author.Id);
            if(r == null) {
                helpers.output(Context.Channel,"No racer found for you, " + Context.User.Mention + ", in this game");
            } else {
                helpers.output(Context.User,r.currentStatus(tV));
            }
        }

        //One Player Still Alive?
        private bool oneAlive() {
            foreach (racer r in racers) {
                if(r.stillIn) return true;
                if(!r.stillIn && r.ability.ID == 8) return true;
            }
            return false;
        }

        //Make New Deck of Shuffled Cards
        private Stack<Card> generateDeck() {
            return shuffleDeck(freshDeck(tV.cardStore));
        }

        //Make a fresh deck of cards
        private static List<Card> freshDeck(string cardStore) {
            List<Card> c = new List<Card>();
            List<Card> temp = trillbot.Classes.Card.get_card(cardStore);

            foreach (Card c1 in temp) {
                for(int i = 0; i < c1.count; i++) {
                    c.Add(c1);
                }
            }

            return c;
        }

        //Shuffle a deck of cards
        private static Stack<Card> shuffleDeck(List<Card> c) {
            Stack<Card> s = new Stack<Card>();

            while (c.Count > 0) {
                int num = trillbot.Program.rand.Next(0,c.Count);
                s.Push(c[num]);
                c.RemoveAt(num);
            }

            return s;
        }

        //Deal Cards
        private void dealCards(SocketCommandContext Context)
        {
            cards = generateDeck();
            foreach(racer r in racers) {
                if(r.ability.ID == 1) {
                    for(int i = 0; i < wealthyIDs.Length; i++) {
                        r.cards.Add(Card.get_card(tV.cardStore,wealthyIDs[i]));
                    }
                    for(int i = r.cards.Count; i < 8; i++) {
                        if(cards.Count == 0) { cards = generateDeck(); }
                        r.cards.Add(cards.Pop());
                    }
                } else {
                    for(int i = 0; i < 8; i++) {
                        if(cards.Count == 0) { cards = generateDeck(); }
                        r.cards.Add(cards.Pop());
                    }
                }
                helpers.output(Context.Guild.GetUser(r.player_discord_id),r.currentStatus(tV));
            }
        }

        //Increment Turn ( Full Logic, better than position++ everywhere with a block of code after it)
        private void incTurn(SocketCommandContext Context) {
            position++;
            if(position == racers.Count) {
                endOfRound(Context); //Handle Passive Movement
                position -= racers.Count;
                round++;
            }
        }

        //End of Turn Logic
        private void endOfTurnLogic(SocketCommandContext Context, racer r, int i) { //Handle All Logic for Transitioning an End of Turn
            if(r.cards.Count == 0) {
                if(cards.Count == 0 ) cards = generateDeck();
                r.cards.Add(cards.Pop());
            } else if(i > -1) {
                r.cards.RemoveAt(i);
                if(cards.Count == 0 ) cards = generateDeck();
                r.cards.Add(cards.Pop());
            }

            if(checkWinner(Context)) {
                return;
            }

            incTurn(Context);

            if(checkWinner(Context)) {
                return;
            }

            displayCurrentBoard(Context);
            nextTurn(Context);
        }

        private bool coreDeathSync(racer r, out string s) {
            s = null;
            if(r.coreSync != null) {
                r.coreSync.stillIn = false;
                s = tV.coreSyncFailure(r.coreSync.name,r.name);
                //s = r.coreSync.name + "'s core fails in tangent with " + r.name + " as their racer goes up in smoke!";
                return true;
            }
            return false;
        }

        //Survival Checks
        private List<string> SurvivalChecks(SocketCommandContext Context, racer r) { 
            var str = new List<string>();
            List<pair> remove = new List<pair>();
            r.hazards.ForEach(e=>{
                e.item2++;
                if(e.item2 > 2)
                {
                    r.stillIn = false;
                    str.Add(tV.deathByCondition(r.name,e.item1.title));
                    //str.Add(r.name + " succumbs to " + e.item1.title + " and their vehicle explodes! 💥");
                    if(coreDeathSync(r, out string s) ) str.Add(s);
                }

                if(e.item1.ID == 17 && e.item2 > 1) {
                    str.Add(tV.deathByCondition(r.name,e.item1.title));
                    //str.Add(r.name + " succumbs to " + e.item1.title + " and their vehicle explodes! 💥");
                    r.stillIn = false;
                    if(coreDeathSync(r, out string s) ) str.Add(s);
                }

                if(e.item1.ID == 16 && e.item2 > 0) {
                    remove.Add(e);
                }
                
                if (1 < r.ability.ID && r.ability.ID < 7) {
                    if(e.item1.ID == ability_to_save[r.ability.ID]) {
                        str.Add(tV.abilitySave(r.name,r.ability.Title,e.item1.title));
                        //str.Add(r.name + " uses their " + r.ability.Title + " to solve " + e.item1.title + ".");
                        remove.Add(e);
                    }
                }
            });
            foreach(pair p in remove) {
                r.hazards.Remove(p);
            }
            if(r.sab() && r.hazards.Count > 1) {
                r.stillIn = false;
                str.Add(tV.deathByCondition(r.name,"sabotage"));
                //str.Add(r.name + " succumbs to Sabotage and their vehicle explodes! 💥");
                if(coreDeathSync(r, out string s) ) str.Add(s);
            }
            if(!r.stillIn && r.ability.ID == 12 && r.abilityRemaining) {
                str.Add(tV.escapePod(r.name));
                //str.Add("An escape pod launches from " + r.name + "'s lightrunner, giving them another chance to struggle along!");
                r.stillIn = true;
                r.cards = new List<Card>();
            }
            if (r.distance < 0) {
                r.distance = 0;
            }
            return str;
        }

        //Increment Turn
        private void nextTurn(SocketCommandContext Context) { //DM the next turn person
            var usr = Context.Guild.GetUser(racers[position].player_discord_id);
            if (usr == null ) {
                helpers.output(Context.Channel,"Uhh boss, something went wrong.");
                return;
            }
            List<string> outOfRace = new List<string>();
            while(!racers[position].stillIn) {
                outOfRace.Add(racers[position].name + " is no longer in the race.");
                incTurn(Context);
                if(checkWinner(Context)) {
                    return;
                }
                usr = Context.Guild.GetUser(racers[position].player_discord_id);
            }
            if(outOfRace.Count != 0) {
                helpers.output(Context.Channel,outOfRace);
            }

            //Start of New Turn
            if(racers[position].crash) {
                List<string> str = new List<string>();
                //str.Add(racers[position].name + "'s crash card triggers. The following racers crash out of the race:");
                racers.Where(e=>e.distance == racers[position].distance).ToList().ForEach(e=> {
                    if (e != racers[position]) {
                        e.stillIn = false;
                        str.Add(e.nameID());
                    }
                });
                string crashed = String.Join(", ",str);
                helpers.output(Context.Channel,tV.crash(racers[position].name,crashed));
                racers[position].crash = false;
            }

            if(racers[position].ability.ID == 12 && racers[position].cards.Count == 1 && racers[position].abilityRemaining) {
                racers[position].hazards = new List<pair>();
                racers[position].abilityRemaining = false;
                //helpers.output(Context.Channel,racers[position].name + "'s escape pod gets out of the way of all the hazards!");
                helpers.output(Context.Channel,tV.escapePodEscape(racers[position].name));
            }

            if(racers[position].ability.ID == 17) {
                var ListRacer = racers.OrderByDescending(e=> e.distance).ToList();
                var target = ListRacer[0];
                if (target == racers[position]) target = ListRacer[1];
                //helpers.output(usr,"You use " + racers[position].ability.Title + " to see:" + System.Environment.NewLine + target.currentStatus());
                helpers.output(usr,tV.peek(racers[position].ability.Title, target.currentStatus(tV)));
            }

            //DM Current Hand & Status
            if (!(position == 0 && round == 1)) helpers.output(usr,racers[position].currentStatus(tV));

            //Start Next Turn
            helpers.output(Context.Channel,racers[position].name + " has the next turn.");
        }

        //Leaderboard Output
        private void displayCurrentBoard(SocketCommandContext Context) {
            List<string> str = new List<string>();
            str.Add("**Leaderboard!** Turn " + round + "." + (position+1));
            str.Add("```md");

            // Build Header Board
            List<string> str2 = new List<string>();
            str2.Add("< Distance");
            str2.Add(helpers.center("Racer Name (ID)", lengths[0]));
            str2.Add(helpers.center("Still In", lengths[1]));
            str2.Add(helpers.center("Sponsor",lengths[2]));
            str2.Add(helpers.center("Special Ability", lengths[3]));
            str2.Add("Hazards >");
            string title = String.Join(" | ",str2);
            str.Add(title);

            var listRacer = racers.OrderByDescending(e=> e.distance).ToList();
            string crash = "";


            for(int i = 0; i < listRacer.Count; i++) {
                str.Add(listRacer[i].leader(lengths,(i%2)==1,i==position,tV));
                if(listRacer[i].crash) {
                    //crash += listRacer[i].name + " will cause a crash on space " + listRacer[i].distance + " at the start of their next turn!" + System.Environment.NewLine;
                    crash += tV.causeCrash(listRacer[i].name,listRacer[i].distance.ToString());
                }
            }
            str.Add("```");
            if (crash.Length > 0) str.Add(crash);
            helpers.output(Context.Channel,str);
        }

        //Passive Movement
        private void endOfRound(SocketCommandContext Context) {
            foreach (racer r in racers ) {
                if(r.ability.ID == 9 && r.hazards.ToList().FirstOrDefault(e=> e.item1.ID == 5) != null && r.abilityRemaining) {
                    r.distance+=3;
                    //helpers.output(Context.Channel,r.name + "'s " + r.ability.Title + " activates causing them to passively move an extra two spaces!");
                    helpers.output(Context.Channel,tV.passiveTwo(r.name,r.ability.Title));
                    r.abilityRemaining = false;
                } else if (r.ability.ID == 10 && r.hazards.ToList().FirstOrDefault(e=> e.item1.ID == 6) != null && r.abilityRemaining) {
                    r.distance+=3;
                    //helpers.output(Context.Channel,r.name + "'s " + r.ability.Title + " activates causing them to passively move an extra two spaces!");
                    helpers.output(Context.Channel,tV.passiveTwo(r.name,r.ability.Title));
                    r.abilityRemaining = false;
                } else if (r.ability.ID == 11 && r.hazards.ToList().FirstOrDefault(e=> e.item1.ID == 9) != null && r.abilityRemaining) {
                    r.distance+=3;
                    //helpers.output(Context.Channel,r.name + "'s " + r.ability.Title + " activates causing them to passively move an extra two spaces!");
                    helpers.output(Context.Channel,tV.passiveTwo(r.name,r.ability.Title));
                    r.abilityRemaining = false;
                } else  if (r.ability.ID == 7 && r.hazards.Count == 0) {
                    int x = 1 + Program.rand.Next(2);
                    if (x == 2) r.distance+=3;
                    //helpers.output(Context.Channel,r.name + " test's their luck and rolls a " + x + ".");
                    helpers.output(Context.Channel,tV.luckPassive(r.name,x.ToString()));
                } else {
                    r.distance++;
                }
                if (r.distance > 24) {
                    r.distance = 24;
                }
            }
        }

        //Check for Winner - If there is one, handle winning logic
        private bool checkWinner(SocketCommandContext Context) {
            foreach(racer r in racers) {
                if (r.distance == 24 && r.stillIn) {
                    SocketGuildUser usr = Context.Guild.Users.FirstOrDefault(e=>e.Id == r.player_discord_id);
                    helpers.output(Context.Channel,usr.Mention + ", you have won the race!");
                    displayCurrentBoard(Context);
                    doReset(Context);
                    return true;
                }
                if(r.distance == 24 && !r.stillIn && r.ability.ID == 8) {
                    SocketGuildUser usr = Context.Guild.Users.FirstOrDefault(e=>e.Id == r.player_discord_id);
                    helpers.output(Context.Channel,usr.Mention + ", you have won the race!");
                    displayCurrentBoard(Context);
                    doReset(Context);
                    return true;
                }
            }
            return false;
        }

        //Shuffle Racers
        private void shuffleRacers(SocketCommandContext Context) {
            List<racer> temp = new List<racer>();
            var abilities = Ability.get_ability(tV.abilityStore);

            while (racers.Count > 0) {
                int num = trillbot.Program.rand.Next(0,racers.Count);
                racers[num].ability = abilities.First(e => e.ID == racers[num].ability.ID );
                if(racers[num].ability.ID == 0) {
                    racers[num].distance=4;
                    //helpers.output(Context.Channel,racers[num].name + " takes a " + racers[num].ability.Title + " and starts 4 distance units ahead!");
                    helpers.output(Context.Channel,tV.fourUnitStart(racers[num].name,racers[num].ability.Title));
                }
                temp.Add(racers[num]);
                racers.RemoveAt(num);
            }

            racers = temp;
        }

        public void exitGame(SocketCommandContext Context) {
            if(runningGame) {
                helpers.output(Context.Channel,"You can't exit the game while it is running. Use `ta!leave`");
                return;
            }
            racer racer = racer.get_racer(Context.Message.Author.Id, Context.Guild.Id);
            if(racer == null) {
                helpers.output(Context.Channel,"You aren't in this game. You can't exit it!");
                return;
            }
            racers.Remove(racer);
            racer.inGame = false;
            trillbot.Classes.racer.update_racer(racer);
            helpers.output(Context.Channel,Context.User.Mention + ", you have left this game");
        }

        //Start Game
        public void startGame(SocketCommandContext Context) {
            dealCards(Context); //Deal cards to all racers
            shuffleRacers(Context); //Randomize Turn Order
            leaderCenter();
            runningGame = true;
            displayCurrentBoard(Context);
            inGameAsync(Context);
            nextTurn(Context);
        }

        public void toggleDeath(SocketCommandContext Context, int ID = -1) {
            var r = racers.FirstOrDefault(e=> e.player_discord_id == Context.User.Id);
            if(r == null) {
                helpers.output(Context.Channel,Context.User.Mention + " you don't have a racer in this game.");
                return;
            }
            r.stillIn = false;
            helpers.output(Context.Channel,Context.User.Mention + " you have left this game. You are unable to join a new game until this game is finished.");
        }
        
        public void adminDeath(SocketCommandContext Context, int ID) {
            var r = racers.FirstOrDefault(e=> e.ID == ID);
            if(r == null ) {
                helpers.output(Context.Channel,Context.User.Mention + ", that racer ID doesn't exist in this race");
                return;
            }
            r.stillIn = false;
            helpers.output(Context.Channel,r.nameID() + " has been removed from this game.");
        }

        public void skipTurn(SocketCommandContext Context) {
            //Handle Survival Checks
            helpers.output(Context.Channel,SurvivalChecks(Context, racers[position]));
            //IF Entire Turn Completed Successfully
            if(oneAlive()) {
                endOfTurnLogic(Context, racers[position], -1);
            } else {
                //helpers.output(Context.Channel,"All racers are dead. This ends the game.");
                helpers.output(Context.Channel,tV.gameOver());
                doReset(Context);
                return;
            }
        }

        //Join Game
        public void joinGame(SocketCommandContext Context) {
            racer racer = racer.get_racer(Context.Message.Author.Id, Context.Guild.Id);
            if (runningGame) {
                helpers.output(Context.Channel,"The game has already started");
                return;
            }

            if(racer == null) {
                helpers.output(Context.Channel,"No racer found for you, " + Context.User.Mention);
                return;
            } else {
                if(racer.inGame) {
                    helpers.output(Context.Channel,"Hold up," +  Context.User.Mention + ", you can only play in one game at a time!");
                    return;
                }
                foreach (racer r in racers) {
                    if(r.ID == racer.ID) {
                        helpers.output(Context.Channel,Context.User.Mention + ", you have already joined the game!");
                        return;
                    }
                    if(r.ability.ID == racer.ability.ID) {
                        helpers.output(Context.Channel,Context.User.Mention + ", I'm sorry, " + r.name + " already has " + r.ability.Title + " in this game.");
                        return;
                    }
                }
                racer.inGame = true;
                racer.replace_racer(racer);
                racers.Add(racer);
                
            }

            helpers.output(Context.Channel,Context.User.Mention + ", you have joined the game");
        }

        public void playAbility(SocketCommandContext Context, int i = -1, List<int> j = null) {
            racer r = racers[position];
            if(r.player_discord_id != Context.Message.Author.Id) {
                helpers.output(Context.Channel,"It's not your turn!");
                return;
            }
            if(!r.abilityRemaining) {
                helpers.output(Context.Channel,"You've already used your special ability");
                return;
            }
            if(!r.ability.Active) {
                helpers.output(Context.Channel,"You're ability isn't an active ability!");
                return;
            }
            Card c = new Card();
            racer t = new racer();
            List<racer> listRacer = new List<racer>();
            switch(r.ability.ID) {
                case 24:
                    t = racers.FirstOrDefault(e=> e.ID == i);
                    if (t == null) {
                        helpers.output(Context.Channel,"You didn't target a valid racer");
                        return;
                    }
                    t.coreSync = r;
                    r.coreSync = t;
                    r.abilityRemaining = false;
                    //helpers.output(Context.Channel,r.name + " synchronized their core with " + t.name + ". If either of them die, the other will explode!");
                    helpers.output(Context.Channel,tV.coreSync(r.name,t.name));
                break;
                case 23:
                    if(i < 0 || j == null || j.Count > 1 || j[0] < 0) {
                        helpers.output(Context.Channel,"You didn't provide two valid targets");
                        return;
                    }
                    racer t1 = racers.FirstOrDefault(e=> e.ID == i);
                    racer t2 = racers.FirstOrDefault(e=> e.ID == j[0]);
                    if (t1 == null || t2 == null) {
                        helpers.output(Context.Channel,"You didn't provide two valid targets");
                        return;
                    }
                    if(t1 == r || t2 == r) {
                        helpers.output(Context.Channel,"You can't target yourself");
                        return;
                    }
                    var c1 = Card.get_card(tV.cardStore,Program.rand.Next(7)+5);
                    while (c1.ID == 7) c1 = Card.get_card(tV.cardStore,Program.rand.Next(7)+5);
                    var c2 = Card.get_card(tV.cardStore,Program.rand.Next(7)+5);
                    while (c2.ID == 7) c2 = Card.get_card(tV.cardStore,Program.rand.Next(7)+5);
                    t1.addHazard(c1);
                    t2.addHazard(c2);
                    r.abilityRemaining = false;
                    helpers.output(Context.Channel, r.name + " used " + r.ability.Title + " and played " + c1.title + " against " + t1.name + " and " + c2.title + " against " + t2.name);
                break;
                case 22:
                    int size = r.cards.Count;
                    r.cards = new List<Card>();
                    for(int k = 0; k < size; k++) {
                        if(cards.Count == 0) cards = generateDeck();
                        r.cards.Add(cards.Pop());
                    }
                    helpers.output(Context.User,r.currentStatus(tV));
                    r.abilityRemaining = false;
                    helpers.output(Context.Channel,r.name + " used " + r.ability.Title + " and discarded their hand to draw a new one.");
                break;
                case 21:
                listRacer = racers.OrderByDescending(e=> e.distance).ToList();
                    for(int k = 0; k < listRacer.Count; k++) {
                        if(listRacer[k] == r) {
                            if (k == 0) {
                                helpers.output(Context.Channel,"You can't use this, you are in first!");
                                return;
                            }
                            t = listRacer[k-1];
                        }
                    }
                    int x = 1 + Program.rand.Next(10);
                    if (x < 4) {
                        r.stillIn = false;
                        r.abilityRemaining = false;
                        //helpers.output(Context.Channel,r.name + " actives their Grav-Sling and it explodes! Roll: " + x);
                        helpers.output(Context.Channel,tV.switchPositionFail(r.name,r.ability.Title,x.ToString()));
                    } else {         
                        int dist = (int)t.distance;
                        t.distance = r.distance;
                        r.distance = dist;
                        r.abilityRemaining = false;
                        helpers.output(Context.Channel,r.name + " actives their " + r.ability.Title + " (Roll: " + x + ") and moves to distance " + r.distance + " by switching locations with " + t.name + " who is now at a distance of " + t.distance);
                    }
                break;
                case 20:
                    if(i<0 || i > r.hazards.Count ) {
                        helpers.output(Context.Channel,"You didn't target a valid Hazard");
                        return;
                    }
                    helpers.output(Context.Channel,r.name + " had a " + r.ability.Title + " and fixed " + r.hazards[--i].item1.title);
                    r.hazards.RemoveAt(i);
                    r.abilityRemaining = false;
                break;
                case 19:
                    if(i<0) { 
                        helpers.output(Context.Channel,"You didn't target a valid racer");
                        return;
                    }
                    t = racers.FirstOrDefault(e=> e.ID == i);
                    if (t == null) {
                        helpers.output(Context.Channel,"You didn't target a valid racer");
                        return;
                    }
                    c = Card.get_card(tV.cardStore,17);
                    t.addHazard(c);
                    r.abilityRemaining = false;
                    helpers.output(Context.Channel,r.name + " used " + r.ability.Title + " and applied a " + c.title + " hazard to " + t.name);
                break;
                case 18:
                    listRacer = racers.OrderByDescending(e=> e.distance).ToList();
                    for(int k = 0; k < listRacer.Count; k++) {
                        if(listRacer[k] == r) {
                            if (k == 0) {
                                helpers.output(Context.Channel,"You can't use this, you are in first!");
                                return;
                            }
                            t = listRacer[k-1];
                        }
                    }
                    r.distance = t.distance;
                    r.abilityRemaining = false;
                    helpers.output(Context.Channel,r.name + " uses " + r.ability.Title + " and matches pace with " + t.name + " on space " + r.distance);
                break;
                case 16:
                    if(i<0) {
                        helpers.output(Context.Channel,"You didn't target a valid racer");
                        return;
                    }
                    t = racers.FirstOrDefault(e=> e.ID == i);
                    if (t == null) {
                        helpers.output(Context.Channel,"You didn't target a valid racer");
                        return;
                    }
                    if(t.cards.Count < 4) {
                        helpers.output(Context.Channel,"You can't target this racers as they have less than 4 cards.");
                        return;
                    }
                    j.Distinct();
                    if(j == null || j.Count != 4) {
                        helpers.output(Context.Channel,"You didn't select 4 cards!");
                        return;
                    }
                    foreach(int k in j) {
                        if (k < 1 || k > 8) {
                            helpers.output(Context.Channel,"You didn't select 4 valid cards!");
                            return;
                        }
                    }
                    var nums = new List<int>();
                    while(nums.Count != 4) {
                        int temp = Program.rand.Next(8);
                        if(!nums.Contains(temp)) nums.Add(temp);
                    }
                    for(int k = 0; k < 4; k++) {
                        var swap = t.cards[nums[k]];
                        t.cards[nums[k]] = r.cards[j[k]-1];
                        r.cards[j[k]-1] = swap;
                    }
                    r.abilityRemaining = false;
                    helpers.output(Context.User,r.currentStatus(tV));
                    helpers.output(Context.Guild.GetUser(t.player_discord_id),t.currentStatus(tV));
                    helpers.output(Context.Channel, r.name + " used " + r.ability.Title + " against " + t.name + " to switch hands with them!");
                break;
                case 15:
                    if(i<0) {
                        helpers.output(Context.Channel,"You didn't target a valid racer");
                        return;
                    }
                    t = racers.FirstOrDefault(e=> e.ID == i);
                    if (t == null) {
                        helpers.output(Context.Channel,"You didn't target a valid racer");
                        return;
                    }
                    if (t == r) {
                        helpers.output(Context.Channel,"You can't use " + r.ability.Title + " on yourself!");
                        return;
                    }
                    if(t.cards.Count == 1) { 
                        helpers.output(Context.Channel,t.name + " losses the following card: " + t.cards[0].title);
                        t.cards.RemoveAt(0);
                        if(cards.Count == 0) cards = generateDeck();
                        t.cards.Add(cards.Pop());
                        r.abilityRemaining = false;
                        helpers.output(Context.Guild.GetUser(t.player_discord_id),t.currentStatus(tV));
                        helpers.output(Context.Channel,r.name + " used " + r.ability.Title + " against " + t.name + " causing them to redraw their only card!");
                    } else {
                        var str = new List<string>();
                        str.Add(t.name + " losses the following cards: ");
                        for(int k = 0; k < 4; k++) {
                            var remove = t.cards[Program.rand.Next(t.cards.Count)];
                            str.Add(remove.title);
                            t.cards.Remove(remove);
                        }
                        helpers.output(Context.Channel,String.Join(", ",str));
                        for(int k = 0; k < 4; k++) {
                            if(cards.Count == 0) cards = generateDeck();
                            t.cards.Add(cards.Pop());
                        }
                        r.abilityRemaining = false;
                        helpers.output(Context.Guild.GetUser(t.player_discord_id),t.currentStatus(tV));
                        helpers.output(Context.Channel,r.name + " used " + r.ability.Title + " against " + t.name + " causing them to redraw four random cards!");
                    }
                break;
                case 14:
                    if(i < 0 || i > 8 || j == null || j.Count > 1 || j[0] < 0 || j[0] > 8) {
                        helpers.output(Context.Channel,"You haven't selected valid cards");
                        return;
                    }
                    if(r.cards[--i].type != "Movement" || r.cards[--j[0]].type != "Movement") {
                        helpers.output(Context.Channel,"You haven't selected valid cards");
                        return;
                    }
                    int d = (int) (r.cards[i].value + r.cards[j[0]].value);
                    r.distance += d;
                    if(r.distance > 24) {
                        helpers.output(Context.Channel,"You can't move past space 24!");
                        r.distance -= d;
                    }
                    r.abilityRemaining = false;
                    helpers.output(Context.Channel,r.name + " moved to space " + r.distance + " using " + r.ability.Title + " to play both a " + r.cards[i].title + " and " + r.cards[j[0]].title);
                    if(j[0] < i) {
                        r.cards.RemoveAt(i);
                        r.cards.RemoveAt(j[0]);
                        if(cards.Count == 0) cards = generateDeck();
                        r.cards.Add(cards.Pop());
                    } else {
                        r.cards.RemoveAt(j[0]);
                        r.cards.RemoveAt(i);
                        if(cards.Count == 0) cards = generateDeck();
                        r.cards.Add(cards.Pop());
                    }
                break;
                default:
                    helpers.output(Context.Channel,"Something went wrong. Try again");
                    return;
            }

            //Handle Survival Checks
            helpers.output(Context.Channel,SurvivalChecks(Context, r));
            //IF Entire Turn Completed Successfully
            if(oneAlive()) {
                endOfTurnLogic(Context, r, -1);
            } else {
                //helpers.output(Context.Channel,"All racers are dead. This ends the game.");
                helpers.output(Context.Channel,tV.gameOver());
                doReset(Context);
                return;
            }
        }

        //Discard a Card
        public void discardAsync(SocketCommandContext Context, int i) {
            racer r = racers[position];
            if(r.player_discord_id != Context.Message.Author.Id) {
                helpers.output(Context.Channel,"It's not your turn!");
                return;
            }
            if (i < 1 && i > r.cards.Count) {
                helpers.output(Context.Channel,"You only have " + r.cards.Count + " cards in your hand! Provide a number 1-" +r.cards.Count + ".");
                return;
            }
            Card c = r.cards[--i];
            helpers.output(Context.Channel,r.name + " discarded " + c.title);
            //Handle Survival Checks
            helpers.output(Context.Channel,SurvivalChecks(Context, r));
            //IF Entire Turn Completed Successfully
            if(oneAlive()) {
                endOfTurnLogic(Context, r, i);
            } else {
                //helpers.output(Context.Channel,"All racers are dead. This ends the game.");
                helpers.output(Context.Channel,tV.gameOver());
                doReset(Context);
                return;
            }
        }

        //Play a Card
        public void playCardAsync(SocketCommandContext Context, int i, int racerID = -1, int hazardID = -1) {
            racer r = racers[position];
            if(r.player_discord_id != Context.Message.Author.Id) {
                helpers.output(Context.Channel,"It's not your turn!");
                return;
            }
            if (i < 1 && i > r.cards.Count) {
                helpers.output(Context.Channel,"You only have " + r.cards.Count + " cards in your hand! Provide a number 1-" + r.cards.Count + ".");
                return;
            }
            Card c = r.cards[--i];
            switch(c.type) { 
                case "Movement":
                    if(!r.canMove() && racerID != 1) {
                        helpers.output(Context.Channel,"You currently can't move. Try solving a hazard!");
                        return;
                    }
                    if(c.value > 2 && !r.maxMove2() && racerID != 1) {
                        helpers.output(Context.Channel,"You currently can't move more than 2 spaces. Try solving a hazard!");
                        return;
                    }
                    switch(c.value) {
                        case 6:
                            if (racerID <= -1 || racerID > 1) {
                                helpers.output(Context.Channel,"You didn't indicate which version of this card to use.");
                                return;
                            }
                            else if (racerID == 1) {
                                c = Card.get_card(tV.cardStore,12);
                                var h2 = r.hazards.Where(e=> e.item1.ID == remedy_to_hazards[(int)c.value].Item1 || e.item1.ID == remedy_to_hazards[(int)c.value].Item2 ).ToList();
                                if (h2 == null) {
                                    helpers.output(Context.Channel,"You can't play this card. Try another.");
                                    return;
                                }
                                string s2 = h2[0].item1.title;
                                if(h2.Count > 1 ) {
                                    for(int j = 1; i < h2.Count; j++) {
                                        s2 += ", " + h2[j].item1.title;
                                    }
                                }
                                h2.ForEach(e=> {
                                    r.hazards.Remove(e);
                                });
                                helpers.output(Context.Channel,r.name + " played " + c.title + " solving " + s2);
                                break;
                            } else if (racerID == 0) {
                                r.distance += c.value;
                                if (r.distance > 24) {
                                    r.distance -= c.value;
                                    helpers.output(Context.Channel,"Woah there, you can't move past space 24! Try a different card.");
                                    return;
                                }
                                helpers.output(Context.Channel,r.name + " played a " + c.title + " spaces. They are now at a distance of " + r.distance + " units." );
                            }
                            break;
                        case 1:
                        case 2:
                        case 3:
                        case 4:
                            r.distance += c.value;
                            if (r.distance > 24) {
                                r.distance -= c.value;
                                helpers.output(Context.Channel,"Woah there, you can't move past space 24! Try a different card.");
                                return;
                            }
                            helpers.output(Context.Channel,r.name + " played a " + c.title + " spaces. They are now at a distance of " + r.distance + " units." );
                            break;
                        default:
                            helpers.output(Context.Channel,"Um boss, something went wrong. Let's try again!");
                            return;
                    }
                    break;
                case "Hazard":
                    switch(c.value) {
                        case 0:
                            var listRacer = racers.OrderByDescending(e=> e.distance).ToList();
                            List<string> str = new List<string>();
                            str.Add(r.name + " causes debris to hit " + listRacer[0].name);
                            if(listRacer[0] == r) listRacer[0].addHazard(c, -1);
                            else listRacer[0].addHazard(c);
                            for(int j = 1; j < 5 && j < listRacer.Count; j++) {
                                str.Add(listRacer[j].name);
                                if (listRacer[j] == r) listRacer[j].addHazard(c, -1);
                                else listRacer[j].addHazard(c);
                            }
                            helpers.output(Context.Channel,str,", ");
                        break;
                        case 1:
                            string ram = "";
                            if(!r.canMove()) {
                                helpers.output(Context.Channel,"Sorry, you can't move. Try a different card.");
                                return;
                            } 
                            if(!r.maxMove2()) {
                                helpers.output(Context.Channel,"Sorry, you can't move this far! Try a different card");
                                return;
                            }
                            r.distance += 3;
                            List<racer> targets = racers.Where(e=>e.distance == r.distance+1).ToList();
                            if (r.distance > 24) {
                                r.distance -= 3;
                                helpers.output(Context.Channel,"Woah there, you can't move past space 24! Try a different card.");
                                return;
                            }
                            if(targets.Count == 0) {
                                ram = r.name + " plays " + c.title + " and doesn't hit any other racers! They move forward 3 spaces to a distance of " + r.distance;
                            } else {
                                List<string> tar = new List<string>();
                                tar.Add(r.name + " plays " + c.title + " against " + targets[0].name);
                                targets[0].addHazard(c);
                                for(int j = 1; j < targets.Count; j++ ) {
                                    targets[j].addHazard(c);
                                    tar.Add(targets[j].name);
                                };
                                ram = String.Join(", ",tar);
                                ram += ". " + r.name + " moves forward 3 spaces to a distance of " + r.distance;
                            }
                            helpers.output(Context.Channel,ram);
                        break;
                        case 2:
                            if(!r.canMove()) {
                                helpers.output(Context.Channel,"Sorry, you can't move. Try a different card.");
                                return;
                            }
                            r.distance++;
                            if (r.distance > 24) {
                                r.distance--;
                                helpers.output(Context.Channel,"Woah there, you can't move past space 24! Try a different card.");
                                return;
                            }
                            r.crash = true;
                            helpers.output(Context.Channel,r.name + " plays a **" + c.title + "** card. You don't want to be on their space at the start of their next turn!");
                        break;
                        case 3:
                            if(!r.canMove()) {
                                helpers.output(Context.Channel,"You currently can't move. Try solving a hazard!");
                                return;
                            }
                            var listRacer2 = racers.OrderByDescending(e=> e.distance).ToList();
                            List<string> str3 = new List<string>();
                            int z = 3;
                            bool foundR = false;
                            string extra = "";
                            for(int j = 0; j < listRacer2.Count && j < z; j++) {
                                if(listRacer2[j] == r) {
                                    foundR = true;
                                    z++;
                                    continue;
                                } else if(!foundR) {
                                    z++;
                                }
                                if(foundR) {
                                    if (z-j == 3) {
                                        str3.Add(r.name + " dazzles  " + listRacer2[j].name);
                                        listRacer2[j].addHazard(c);
                                    } else {
                                        str3.Add(listRacer2[j].name);
                                        listRacer2[j].addHazard(c);
                                    }
                                    if(listRacer2[j].ability.ID == 13) {
                                        //extra += ". " + listRacer2[j].name + " uses " + listRacer2[j].ability.Title + " back at " + r.name + " causes them to get hit by Dazzle instead!";
                                        extra += "." + tV.stunCounter(listRacer2[j].name,listRacer2[j].ability.Title,r.name);
                                        r.addHazard(c, -1);
                                        listRacer2[j].hazards.RemoveAt(listRacer2.Count-1); //Remove Dazzle
                                    }
                                }
                            }
                            r.distance++;
                            string debris = String.Join(", ",str3) + extra;
                            helpers.output(Context.Channel,debris + ". " + r.name + " also moves one space.");
                        break;
                    }
                    break;
                case "THazard":
                    var target = racers.FirstOrDefault(e=>e.ID == racerID);
                    if(target == null) {
                        helpers.output(Context.Channel,"You didn't target a valid racer. Try again.");
                        return;
                    }
                    if (target == r) {
                        helpers.output(Context.Channel,"You can't target yourself...");
                        return;
                    }
                    helpers.output(Context.Channel,targetHazard(r,target,c));
                    break;
                case "Remedy":
                    switch(c.value) {
                        case 3:
                        if(racerID < 0 || hazardID < 0) {
                            helpers.output(Context.Channel,"You need to provide two hazards to target. If you only have one provide a `0` as the other input.");
                            return;
                        }
                        if(--racerID > r.hazards.Count || --hazardID > r.hazards.Count) {
                            helpers.output(Context.Channel,"One of your inputs is outside of acceptable limits. Please try again");
                            return;
                        }
                        if(racerID == -1) {
                            helpers.output(Context.Channel,r.name + " played " + c.title.ToLower() + " solving " + r.hazards[hazardID].item1.title.ToLower());
                            r.hazards.RemoveAt(hazardID);
                        } else if (hazardID == -1) {
                            helpers.output(Context.Channel,r.name + " played " + c.title.ToLower() + " solving " + r.hazards[racerID].item1.title.ToLower());
                            r.hazards.RemoveAt(racerID);
                        } else {
                            helpers.output(Context.Channel,r.name + " played " + c.title.ToLower() + " solving " + r.hazards[racerID].item1.title.ToLower() + " and " + r.hazards[hazardID].item1.title.ToLower());
                            if(racerID > hazardID) {   
                                r.hazards.RemoveAt(racerID);
                                r.hazards.RemoveAt(hazardID);
                            } else {
                                r.hazards.RemoveAt(hazardID);
                                r.hazards.RemoveAt(racerID);
                            }
                        }
                        r.distance-=2;
                        break;
                        default:
                        var h = r.hazards.Where(e=> e.item1.ID == remedy_to_hazards[(int)c.value].Item1 || e.item1.ID == remedy_to_hazards[(int)c.value].Item2 || e.item1.ID == remedy_to_hazards[(int)c.value].Item3 ).ToList();
                        if (h.Count == 0) {
                            helpers.output(Context.Channel,"You can't play this card. Try another.");
                            return;
                        }
                        List<string> str = new List<string>();
                        h.ForEach(e=> {
                            r.hazards.Remove(e);
                            str.Add(e.item1.title.ToLower());
                        });
                        string solved = String.Join(", ",str);
                        helpers.output(Context.Channel,r.name + " played " + c.title + " solving " + solved);
                        break;
                    }
                    break;
                default:
                    helpers.output(Context.Channel,"Um boss, something went wrong. Let's try again!");
                    return;
            }
            //Handle Survival Checks
            helpers.output(Context.Channel,SurvivalChecks(Context, r));
            //IF Entire Turn Completed Successfully
            if(oneAlive()) {
                endOfTurnLogic(Context, r, i);
            } else {
                //helpers.output(Context.Channel,"All racers are dead. This ends the game.");
                helpers.output(Context.Channel,tV.gameOver());
                doReset(Context);
                return;
            }
        }

        public bool inGame(SocketUser User) {
            foreach (var r in racers) {
                if (r.player_discord_id == User.Id) return true;
            }
            return false;
        }
        
        //Display Current players in the game
        public void inGameAsync(SocketCommandContext Context) {
            if (racers.Count == 0) {
                helpers.output(Context.Channel,"No racers in game.");
                return;
            }
            List<string> str = new List<string>();
            str.Add("**Current Racers**");
            for(int i = 0; i < racers.Count; i++) {
                if(i == position) {
                    str.Add("**#" + (i+1) + ": " + racers[i].nameID() + " | " + racers[i].ability.Title + "**");
                } else {
                    str.Add("#" + (i+1) + ": " + racers[i].nameID() + " | " + racers[i].ability.Title);
                }
            }
            helpers.output(Context.Channel,str);
        }

        //Mannually Reset a Game
        public void doReset(SocketCommandContext Context) {
            racers.ForEach(e=> {
                e.reset();
                racer.update_racer(e);
            });
            helpers.output(Context.Channel,"Game Reset");
            Program.games.Remove(Context.Channel.Id);
            if(Program.games.Count == 0) Context.Client.SetGameAsync(null, null, Discord.ActivityType.Playing).GetAwaiter().GetResult();
        }

        //Ping the current players turn
        public void whosTurnAsync(SocketCommandContext Context) { //Need to remind a person its there turn?
            var usr = Context.Guild.GetUser(racers[position].player_discord_id);
            if (usr == null ) {
                helpers.output(Context.Channel,"Uhh boss, something went wrong.");
            }
            helpers.output(Context.Channel,"Hey " + usr.Mention + " It's your turn now!");
        }

    }

}