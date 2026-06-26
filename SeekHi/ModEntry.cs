using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Characters;

namespace SeekHi
{
    public class ModEntry : Mod
    {
        private ModConfig Config;
        private Random Randomizer = new Random();
        private DateTime _lastGreetingTime = DateTime.MinValue;

        private class DelayedResponse
        {
            public NPC Npc { get; set; }
            public string ResponseText { get; set; }
            public int TicksElapsed { get; set; } = 0;
            public bool Turned { get; set; } = false;
            public bool Bubbled { get; set; } = false;
        }
        private List<DelayedResponse> _activeResponses = new List<DelayedResponse>();

        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.Input.ButtonPressed += this.OnButtonPressed;
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
        }

        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu == null) return;

            configMenu.Register(
                mod: this.ModManifest,
                reset: () => this.Config = new ModConfig(),
                save: () => this.Helper.WriteConfig(this.Config)
            );

            configMenu.AddKeybind(
                mod: this.ModManifest,
                getValue: () => this.Config.GreetingKey,
                setValue: value => this.Config.GreetingKey = value,
                name: () => this.Helper.Translation.Get("config.greeting-key.name"),
                tooltip: () => this.Helper.Translation.Get("config.greeting-key.desc")
            );
            configMenu.AddNumberOption(
                mod: this.ModManifest,
                getValue: () => this.Config.SearchRadius,
                setValue: value => this.Config.SearchRadius = value,
                name: () => this.Helper.Translation.Get("config.search-radius.name"),
                tooltip: () => this.Helper.Translation.Get("config.search-radius.desc"),
                min: 1, max: 20
            );
            configMenu.AddBoolOption(
                mod: this.ModManifest,
                getValue: () => this.Config.AllowGreetingIfAlreadyTalked,
                setValue: value => this.Config.AllowGreetingIfAlreadyTalked = value,
                name: () => this.Helper.Translation.Get("config.allow-greeting-talked.name"),
                tooltip: () => this.Helper.Translation.Get("config.allow-greeting-talked.desc")
            );
            configMenu.AddNumberOption(
                mod: this.ModManifest,
                getValue: () => this.Config.TurnDelaySeconds,
                setValue: value => this.Config.TurnDelaySeconds = value,
                name: () => this.Helper.Translation.Get("config.turn-delay.name"),
                tooltip: () => this.Helper.Translation.Get("config.turn-delay.desc"),
                min: 0f, max: 2.0f, interval: 0.05f,
                formatVal: value => $"{value:F2} " + this.Helper.Translation.Get("config.seconds.unit")
            );
            configMenu.AddNumberOption(
                mod: this.ModManifest,
                getValue: () => this.Config.BubbleDelaySeconds,
                setValue: value => this.Config.BubbleDelaySeconds = value,
                name: () => this.Helper.Translation.Get("config.bubble-delay.name"),
                tooltip: () => this.Helper.Translation.Get("config.bubble-delay.desc"),
                min: 0f, max: 3.0f, interval: 0.05f,
                formatVal: value => $"{value:F2} " + this.Helper.Translation.Get("config.seconds.unit")
            );
            configMenu.AddNumberOption(
                mod: this.ModManifest,
                getValue: () => this.Config.GreetingCooldownSeconds,
                setValue: value => this.Config.GreetingCooldownSeconds = value,
                name: () => this.Helper.Translation.Get("config.cooldown.name"),
                tooltip: () => this.Helper.Translation.Get("config.cooldown.desc"),
                min: 1f, max: 60f, interval: 1f,
                formatVal: value => $"{value:F0} " + this.Helper.Translation.Get("config.seconds.unit")
            );
            configMenu.AddBoolOption(
                mod: this.ModManifest,
                getValue: () => this.Config.AlwaysRespond,
                setValue: value => this.Config.AlwaysRespond = value,
                name: () => this.Helper.Translation.Get("config.always-respond.name"),
                tooltip: () => this.Helper.Translation.Get("config.always-respond.desc")
            );
        }

        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsPlayerFree) return;
            if (e.Button == this.Config.GreetingKey)
            {
                if ((DateTime.Now - this._lastGreetingTime).TotalSeconds < this.Config.GreetingCooldownSeconds) return;
                this._lastGreetingTime = DateTime.Now;
                this.TriggerGreeting();
            }
        }

        private void TriggerGreeting()
        {
            Farmer player = Game1.player;
            if (player == null || player.currentLocation == null) return;

            player.doEmote(32);

            Vector2 playerTile = player.Tile;

            foreach (var npc in player.currentLocation.characters)
            {
                if (npc == null || !npc.IsVillager) continue;
                if (npc is Child || npc is Pet || npc is Horse) continue;

                if (Vector2.Distance(playerTile, npc.Tile) > this.Config.SearchRadius) continue;

                bool hasTalkedToday = player.friendshipData.TryGetValue(npc.Name, out var f) && f.TalkedToToday;
                if (hasTalkedToday && !this.Config.AllowGreetingIfAlreadyTalked) continue;

                if (this.DetermineIfNPCResponds(player, npc))
                {
                    string responseText = this.GetNPCResponseText(npc, player);
                    this._activeResponses.Add(new DelayedResponse { Npc = npc, ResponseText = responseText });

                    if (!hasTalkedToday)
                    {
                        player.changeFriendship(20, npc);
                        if (player.friendshipData.TryGetValue(npc.Name, out var f2)) f2.TalkedToToday = true;
                    }
                }
            }
        }

        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady) return;

            for (int i = this._activeResponses.Count - 1; i >= 0; i--)
            {
                var response = this._activeResponses[i];
                response.TicksElapsed++;

                if (response.TicksElapsed >= (this.Config.TurnDelaySeconds * 60f) && !response.Turned)
                {
                    response.Npc.faceGeneralDirection(Game1.player.getStandingPosition());
                    response.Turned = true;
                }
                if (response.TicksElapsed >= (this.Config.BubbleDelaySeconds * 60f) && !response.Bubbled)
                {
                    response.Npc.showTextAboveHead(response.ResponseText);
                    response.Bubbled = true;
                }
                if (response.Bubbled) this._activeResponses.RemoveAt(i);
            }
        }

        private bool DetermineIfNPCResponds(Farmer player, NPC npc)
        {
            if (this.Config.AlwaysRespond)
                return true;

            int chance = 30 + (int)((player.getFriendshipLevelForNPC(npc.Name) / 2500f) * 50);
            if (npc.Manners == 1) chance += 20;
            else if (npc.Manners == 2) chance -= 20;
            return this.Randomizer.Next(0, 100) < chance;
        }

        private string GetNPCResponseText(NPC npc, Farmer player)
        {
            string season = Game1.currentSeason.ToLower();
            string weather = Game1.isGreenRain ? "greenrain" : (Game1.isLightning ? "storm" : (Game1.isRaining ? "rain" : (Game1.isSnowing ? "snow" : "sun")));

            string time = "afternoon";
            if (Game1.timeOfDay < 1200) time = "morning";
            else if (Game1.timeOfDay >= 1800) time = "night";

            string env = (npc.currentLocation != null && npc.currentLocation.IsOutdoors) ? "outdoor" : "indoor";

            int friendshipLevel = player.getFriendshipLevelForNPC(npc.Name);
            string friendshipStage = "low";
            if (friendshipLevel >= 2000) friendshipStage = "high";
            else if (friendshipLevel >= 500) friendshipStage = "mid";

            if (player.friendshipData.TryGetValue(npc.Name, out var friendship))
            {
                if (npc.Name == player.spouse || friendship.Status == FriendshipStatus.Married)
                {
                    friendshipStage = "spouse";
                }
                else if (friendship.Status == FriendshipStatus.Dating || friendship.Status == FriendshipStatus.Engaged)
                {
                    friendshipStage = "love";
                }
            }

            List<string> keys = new List<string>
            {
                $"npc.{npc.Name}.{friendshipStage}.{season}.{weather}.{time}.{env}",
                $"npc.{npc.Name}.{friendshipStage}.{season}.{weather}.{time}",
                $"npc.{npc.Name}.{friendshipStage}.{weather}.{time}",
                $"npc.{npc.Name}.{friendshipStage}.{season}.{time}",
                $"npc.{npc.Name}.{friendshipStage}",
                $"npc.{npc.Name}.generic",

                $"generic.{friendshipStage}.{season}.{weather}.{time}.{env}",
                $"generic.{friendshipStage}.{season}.{weather}.{time}",
                $"generic.{friendshipStage}.{weather}.{time}",
                $"generic.{friendshipStage}.{season}.{time}",
                $"generic.{friendshipStage}.{season}",
                $"generic.{friendshipStage}.{weather}",
                $"generic.{friendshipStage}.{time}",
                $"generic.{friendshipStage}.base",
                "generic.base"
            };

            foreach (string baseKey in keys)
            {
                List<int> availableIndices = new List<int>();
                int index = 1;

                // 【核心修复】动态探查：不断自增检测后缀，突破 5 行的硬编码限制
                while (this.Helper.Translation.Get($"{baseKey}.{index}").HasValue())
                {
                    availableIndices.Add(index);
                    index++;
                }

                if (availableIndices.Count > 0)
                {
                    int randomIndex = availableIndices[this.Randomizer.Next(availableIndices.Count)];
                    var translation = this.Helper.Translation.Get($"{baseKey}.{randomIndex}");

                    return translation.Tokens(new { playerName = player.Name, npcName = npc.displayName }).ToString();
                }

                var directTranslation = this.Helper.Translation.Get(baseKey);
                if (directTranslation.HasValue())
                {
                    return directTranslation.Tokens(new { playerName = player.Name, npcName = npc.displayName }).ToString();
                }
            }

            return "...";
        }
    }

    public interface IGenericModConfigMenuApi
    {
        void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);
        void AddBoolOption(IManifest mod, Func<bool> getValue, Action<bool> setValue, Func<string> name, Func<string> tooltip = null, string fieldId = null);
        void AddNumberOption(IManifest mod, Func<int> getValue, Action<int> setValue, Func<string> name, Func<string> tooltip = null, int? min = null, int? max = null, int? interval = null, Func<int, string> formatVal = null, string fieldId = null);
        void AddNumberOption(IManifest mod, Func<float> getValue, Action<float> setValue, Func<string> name, Func<string> tooltip = null, float? min = null, float? max = null, float? interval = null, Func<float, string> formatVal = null, string fieldId = null);
        void AddKeybind(IManifest mod, Func<SButton> getValue, Action<SButton> setValue, Func<string> name, Func<string> tooltip = null, string fieldId = null);
    }
}