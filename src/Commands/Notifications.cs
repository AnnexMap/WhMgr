﻿namespace WhMgr.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using DSharpPlus;
    using DSharpPlus.CommandsNext;
    using DSharpPlus.CommandsNext.Attributes;
    using DSharpPlus.Entities;

    using WhMgr.Data;
    using WhMgr.Data.Models;
    using WhMgr.Diagnostics;
    using WhMgr.Extensions;

    public class Notifications
    {
        public const int MaxPokemonDisplayed = 70;
        public const int MaxPokemonSubscriptions = 25;
        public const int MaxRaidSubscriptions = 5;
        public const int CommonTypeMinimumIV = 97;

        private const string All = "All";

        private static readonly IEventLogger _logger = EventLogger.GetLogger();

        private readonly Dependencies _dep;

        public Notifications(Dependencies dep)
        {
            _dep = dep;
        }

        [
            Command("info"),
            Description("Shows your current Pokemon and Raid boss notification subscriptions.")
        ]
        public async Task InfoAsync(CommandContext ctx,
            [Description("Discord user mention string.")] string mention = "")
        {
            if (string.IsNullOrEmpty(mention))
            {
                await SendUserSubscriptionSettings(ctx.Client, ctx.User, ctx.User.Id);
                return;
            }
            if (!ctx.User.Id.IsModeratorOrHigher(_dep.WhConfig))
            {
                await ctx.TriggerTypingAsync();
                await ctx.RespondAsync($"{ctx.User.Mention} is not a moderator or higher thus you may not see other's subscription settings.");
                return;
            }

            var userId = ConvertMentionToUserId(mention);
            if (userId <= 0)
            {
                await ctx.TriggerTypingAsync();
                await ctx.RespondAsync($"{ctx.User.Mention} Failed to retrieve user with mention tag {mention}.");
                return;
            }

            await SendUserSubscriptionSettings(ctx.Client, ctx.User, userId);
        }

        [
            Command("enable"),
            Aliases("disable"),
            Description("Enables or disables all of your Pokemon and Raid notification subscriptions at once.")
        ]
        public async Task EnableDisableAsync(CommandContext ctx)
        {
            await ctx.Message.IsDirectMessageSupported();

            if (!_dep.SubscriptionManager.UserExists(ctx.User.Id))
            {
                await ctx.TriggerTypingAsync();
                await ctx.RespondAsync($"{ctx.User.Mention} is not currently subscribed to any Pokemon or Raid notifications.");
                return;
            }

            var cmd = ctx.Message.Content.TrimStart('.', ' ');
            if (_dep.SubscriptionManager.Set(ctx.User.Id, cmd.ToLower().Contains("enable")))
            {
                await ctx.TriggerTypingAsync();
                await ctx.RespondAsync($"{ctx.User.Mention} has **{cmd}d** Pokemon and Raid notifications.");
            }
        }

        [
            Command("pokeme"),
            Description("Subscribe to Pokemon notifications based on the pokedex number or name, minimum IV stats, or minimum level.")
        ]
        public async Task PokeMeAsync(CommandContext ctx,
            [Description("Pokemon name or id to subscribe to Pokemon spawn notifications.")] string poke,
            [Description("Minimum IV to receive notifications for, use 0 to disregard IV.")] int iv = 0,
            [Description("Minimum level to receive notifications for, use 0 to disregard level.")] int lvl = 0,
            [Description("Specific gender the Pokemon must be, use * to disregard gender.")] string gender = "*")
        {
            var isSupporter = await ctx.Client.IsSupporterOrHigher(ctx.User.Id, _dep.WhConfig);
            var isModOrHigher = ctx.User.Id.IsModeratorOrHigher(_dep.WhConfig);
            if (!isSupporter && !isModOrHigher)
            {
                await ctx.DonateUnlockFeaturesMessage();
                return;
            }

            //if (command.Args.Count >= 3)
            if (lvl > 0 || gender != "*")
            {
                if (!await ctx.Client.IsSupporterOrHigher(ctx.User.Id, _dep.WhConfig))
                {
                    await ctx.TriggerTypingAsync();
                    await ctx.RespondAsync($"{ctx.User.Mention} The minimum level and gender type parameters are only available to Supporter members, please consider donating to unlock this feature.");
                    return;
                }
            }

            //await message.IsDirectMessageSupported();

            //if (!int.TryParse(cpArg, out int cp))
            //{
            //    await message.RespondAsync($"'{cpArg}' is not a valid value for CP.");
            //    return;
            //}

            if (iv == 0)
            {
                //await message.RespondAsync($"{message.Author.Mention} you entered 0% for a minimum IV, are you s you want to do this?");
                //return;
            }

            if (iv < 0 || iv > 100)
            {
                await ctx.TriggerTypingAsync();
                await ctx.RespondAsync($"{ctx.User.Mention} {iv} must be within the range of 0-100.");
                return;
            }

            if (gender != "*" && gender != "m" && gender != "f")
            {
                await ctx.TriggerTypingAsync();
                await ctx.RespondAsync($"{ctx.User.Mention} {gender} is not a valid gender.");
                return;
            }

            if (lvl < 0 || lvl > 35)
            {
                await ctx.TriggerTypingAsync();
                await ctx.RespondAsync($"{ctx.User.Mention} {lvl} must be within the range of 0-35.");
                return;
            }

            if (string.Compare(poke, All, true) == 0)
            {
                if (!isSupporter)
                {
                    await ctx.TriggerTypingAsync();
                    await ctx.RespondAsync($"{ctx.User.Mention} non-supporter members have a limited Pokemon notification amount of {MaxPokemonSubscriptions}, thus you may not use the 'all' parameter. Please narrow down your Pokemon notification subscriptions to be more specific and try again.");
                    return;
                }

                if (iv < 80)
                {
                    await ctx.TriggerTypingAsync();
                    await ctx.RespondAsync($"{ctx.User.Mention} may not subscribe to **all** Pokemon with a minimum IV less than 80, please set something higher.");
                    return;
                }

                var subscription = _dep.SubscriptionManager.GetUserSubscriptions(ctx.User.Id);

                for (int i = 1; i < 386; i++)
                {
                    if (i == 132 && !isSupporter)
                    {
                        await ctx.TriggerTypingAsync();
                        await ctx.RespondAsync($"{ctx.User.Mention} Ditto has been skipped since he is only available to Supporters. Please consider donating to lift this restriction.");
                        continue;
                    }

                    //var pokemon = Database.Instance.Pokemon[i];
                    if (!_dep.SubscriptionManager.UserExists(ctx.User.Id))
                    {
                        _dep.SubscriptionManager.AddPokemon(ctx.User.Id, i, (i == 201 ? 0 : iv), lvl, gender);
                        continue;
                    }

                    //User has already subscribed before, check if their new requested sub already exists.
                    if (!subscription.Pokemon.Exists(x => x.PokemonId == i))
                    {
                        //Always ignore the user's input for Unown and set it to 0 by default.
                        subscription.Pokemon.Add(new PokemonSubscription { PokemonId = i, MinimumIV = (i == 201 ? 0 : iv), MinimumLevel = lvl, Gender = gender });
                        continue;
                    }

                    //Check if minimum IV value is different from value in database, if not add it to the already subscribed list.
                    var subscribedPokemon = subscription.Pokemon.Find(x => x.PokemonId == i);
                    if (iv != subscribedPokemon.MinimumIV ||
                        lvl != subscribedPokemon.MinimumLevel ||
                        gender != subscribedPokemon.Gender)
                    {
                        subscribedPokemon.MinimumIV = (i == 201 ? 0 : iv);
                        subscribedPokemon.MinimumLevel = lvl;
                        subscribedPokemon.Gender = gender;
                    }
                }

                _dep.SubscriptionManager.Save(subscription);

                await ctx.TriggerTypingAsync();
                await ctx.RespondAsync($"{ctx.User.Mention} subscribed to **all** Pokemon notifications with a minimum IV of {iv}%.");
                return;
            }

            var alreadySubscribed = new List<string>();
            var subscribed = new List<string>();
            foreach (var arg in poke.Replace(" ", "").Split(','))
            {
                if (!int.TryParse(arg, out int pokeId))
                {
                    pokeId = arg.PokemonIdFromName();
                    if (pokeId == 0)
                    {
                        await ctx.TriggerTypingAsync();
                        await ctx.RespondAsync($"{ctx.User.Mention} failed to lookup Pokemon by name and pokedex id {arg}.");
                        continue;
                    }
                }

                if (pokeId == 132 && !isSupporter)
                {
                    await ctx.TriggerTypingAsync();
                    await ctx.RespondAsync($"{ctx.User.Mention} Ditto is only available to Supporters, please consider donating to unlock this feature.");
                    continue;
                }

                //TODO: Check if common type pokemon e.g. Pidgey, Ratatta, Spinarak 'they are beneath him and he refuses to discuss them further'
                if (IsCommonPokemon(pokeId) && iv < CommonTypeMinimumIV && !isModOrHigher)
                {
                    await ctx.TriggerTypingAsync();
                    await ctx.RespondAsync($"{ctx.User.Mention} {Database.Instance.Pokemon[pokeId].Name} is a common type Pokemon and cannot be subscribed to for notifications unless the IV is set to at least {CommonTypeMinimumIV}% or higher.");
                    continue;
                }

                if (!Database.Instance.Pokemon.ContainsKey(pokeId))
                {
                    await ctx.TriggerTypingAsync();
                    await ctx.RespondAsync($"{ctx.User.Mention} {pokeId} is not a valid Pokemon id.");
                    continue;
                }

                var pokemon = Database.Instance.Pokemon[pokeId];

                if (!_dep.SubscriptionManager.UserExists(ctx.User.Id))
                {
                    _dep.SubscriptionManager.AddPokemon(ctx.User.Id, pokeId, (pokeId == 201 ? 0 : iv), lvl, gender);
                    subscribed.Add(pokemon.Name);
                    continue;
                }

                var subscription = _dep.SubscriptionManager.GetUserSubscriptions(ctx.User.Id);

                //User has already subscribed before, check if their new requested sub already exists.
                if (!subscription.Pokemon.Exists(x => x.PokemonId == pokeId))
                {
                    if (!isSupporter && subscription.Pokemon.Count >= MaxPokemonSubscriptions)
                    {
                        await ctx.TriggerTypingAsync();
                        await ctx.RespondAsync($"{ctx.User.Mention} non-supporter members have a limited notification amount of {MaxPokemonSubscriptions} different Pokemon, please consider donating to lift this to every Pokemon. Otherwise you will need to remove some subscriptions in order to subscribe to new Pokemon.");
                        return;
                    }

                    _dep.SubscriptionManager.AddPokemon(ctx.User.Id, pokeId, (pokeId == 201 ? 0 : iv), lvl, gender);
                    subscribed.Add(pokemon.Name);
                    continue;
                }
                else
                {
                    //Check if minimum IV value is different from value in database, if not add it to the already subscribed list.
                    var subscribedPokemon = subscription.Pokemon.Find(x => x.PokemonId == pokeId);
                    if (iv != subscribedPokemon.MinimumIV ||
                        lvl != subscribedPokemon.MinimumLevel ||
                        gender != subscribedPokemon.Gender)
                    {
                        subscribedPokemon.MinimumIV = iv;
                        subscribedPokemon.MinimumLevel = lvl;
                        subscribedPokemon.Gender = gender;
                        subscribed.Add(pokemon.Name);

                        _dep.SubscriptionManager.Save(subscription);
                    }
                    else
                    {
                        alreadySubscribed.Add(pokemon.Name);
                    }
                }
            }

            await ctx.TriggerTypingAsync();
            await ctx.RespondAsync
            (
                (subscribed.Count > 0
                    ? $"{ctx.User.Mention} has subscribed to **{string.Join("**, **", subscribed)}** notifications with a minimum IV of {iv}%{(lvl > 0 ? $" and a minimum level of {lvl}" : null)}{(gender == "*" ? null : $" and only '{gender}' gender types")}."
                    : string.Empty) +
                (alreadySubscribed.Count > 0
                    ? $"\r\n{ctx.User.Mention} is already subscribed to **{string.Join("**, **", alreadySubscribed)}** notifications with a minimum IV of {iv}%{(lvl > 0 ? $" and a minimum level of {lvl}" : null)}{(gender == "*" ? null : $" and only '{gender}' gender types")}."
                    : string.Empty)
            );
        }

        [
            Command("pokemenot"),
            Description("Unsubscribe from one or more or even all subscribed Pokemon notifications by pokedex number or name.")
        ]
        public async Task PokeMeNotAsync(CommandContext ctx,
            [Description("Pokemon name or id to unsubscribe from Pokemon spawn notifications.")] string poke)
        {
            if (!_dep.SubscriptionManager.UserExists(ctx.User.Id))
            {
                await ctx.TriggerTypingAsync();
                await ctx.RespondAsync($"{ctx.User.Mention} is not subscribed to any Pokemon notifications.");
                return;
            }

            var subscription = _dep.SubscriptionManager.GetUserSubscriptions(ctx.User.Id);

            if (string.Compare(poke, All, true) == 0)
            {
                var confirm = await Confirm(ctx, $"{ctx.User.Mention} are you sure you want to remove **all** {subscription.Pokemon.Count.ToString("N0")} of your Pokemon subscriptions? If so, please reply back with `{_dep.WhConfig.CommandPrefix}{ctx.Command.QualifiedName} all yes` to confirm.");
                if (!confirm) return;

                if (!_dep.SubscriptionManager.RemoveAllPokemon(ctx.User.Id))
                {
                    await ctx.TriggerTypingAsync();
                    await ctx.RespondAsync($"Failed to remove all Pokemon subscriptions for {ctx.User.Mention}.");
                    return;
                }

                await ctx.TriggerTypingAsync();
                await ctx.RespondAsync($"{ctx.User.Mention} has unsubscribed from **all** Pokemon notifications.");
                return;
            }

            var notSubscribed = new List<string>();
            var unsubscribed = new List<string>();
            foreach (var arg in poke.Replace(" ", "").Split(','))
            {
                var pokeId = arg.PokemonIdFromName();
                if (pokeId == 0)
                {
                    if (!int.TryParse(arg, out pokeId))
                    {
                        await ctx.TriggerTypingAsync();
                        await ctx.RespondAsync($"{ctx.User.Mention}, failed to lookup Pokemon by name and pokedex id using {arg}.");
                        return;
                    }
                }

                if (!Database.Instance.Pokemon.ContainsKey(pokeId))
                {
                    await ctx.TriggerTypingAsync();
                    await ctx.RespondAsync($"{ctx.User.Mention}, pokedex number {pokeId} is not a valid Pokemon id.");
                    continue;
                }

                var pokemon = Database.Instance.Pokemon[pokeId];
                var unsubscribePokemon = subscription.Pokemon.Find(x => x.PokemonId == pokeId);
                if (unsubscribePokemon != null)
                {
                    if (subscription.Pokemon.Remove(unsubscribePokemon))
                    {
                        unsubscribed.Add(pokemon.Name);
                    }
                }
                else
                {
                    notSubscribed.Add(pokemon.Name);
                }
            }

            _dep.SubscriptionManager.Save(subscription);

            await ctx.TriggerTypingAsync();
            await ctx.RespondAsync
            (
                (unsubscribed.Count > 0
                    ? $"{ctx.User.Mention} has unsubscribed from **{string.Join("**, **", unsubscribed)}** notifications."
                    : string.Empty) +
                (notSubscribed.Count > 0
                    ? $" {ctx.User.Mention} is not subscribed to **{string.Join("**, **", notSubscribed)}** notifications."
                    : string.Empty)
            );
        }

        [
            Command("raidme"),
            Description("Subscribe to raid boss notifications based on the pokedex number or name.")
        ]
        public async Task RaidMeAsync(CommandContext ctx,
            [Description("Pokemon name or id to subscribe to raid notifications.")] string poke,
            [Description("City to send the notification if the raid appears in otherwise if null all will be sent.")] string city = null)
        {
            var isSupporter = await ctx.Client.IsSupporterOrHigher(ctx.User.Id, _dep.WhConfig);
            var isModOrHigher = ctx.User.Id.IsModeratorOrHigher(_dep.WhConfig);
            if (!isSupporter && !isModOrHigher)
            {
                await ctx.DonateUnlockFeaturesMessage();
                return;
            }

            if (string.Compare(city, All, true) != 0 && !string.IsNullOrEmpty(city))
            {
                if (_dep.WhConfig.CityRoles.Find(x => string.Compare(x.ToLower(), city.ToLower(), true) == 0) == null)
                {
                    await ctx.RespondAsync($"{ctx.User.Mention} Failed to find city role {city}. To see a list of valid city roles type the command `.cities` or `.feeds`.");
                    return;
                }
            }
            else
            {
                //Assign to all cities.
                city = string.Empty;
            }

            if (string.Compare(poke, All, true) == 0)
            {
                //var isSupporter = await ctx.Client.IsSupporterOrHigher(ctx.User.Id, _dep.Config);
                if (!isSupporter)
                {
                    await ctx.TriggerTypingAsync();
                    await ctx.RespondAsync($"{ctx.User.Mention} Non-supporter members have a limited raid boss notification amount of {MaxRaidSubscriptions}, thus you may not use the 'all' parameter. Please narrow down your raid boss notification subscriptions to be more specific and try again.");
                    return;
                }

                for (var i = 1; i < 386; i++)
                {
                    //if (!i.IsValidRaidBoss(_dep.Config.RaidBosses)) continue;
                    //if (!_dep.Db.IsValidRaidBoss(i))
                    //    continue;

                    var pokemon = Database.Instance.Pokemon[i];
                    if (string.IsNullOrEmpty(city))
                    {
                        for (var cty = 0; cty < _dep.WhConfig.CityRoles.Count; cty++)
                        {
                            if (!_dep.SubscriptionManager.AddRaid(ctx.User.Id, i, _dep.WhConfig.CityRoles[cty]))
                            {
                                _logger.Error($"Failed to add raid boss {i} in city {_dep.WhConfig.CityRoles[cty]} added to {ctx.User.Id} subscription list.");
                                continue;
                            }

                            _logger.Info($"Raid boss {i} in city {_dep.WhConfig.CityRoles[cty]} added to {ctx.User.Id} subscription list.");
                            //AddRaidBoss(ctx.User.Id, i, _dep.WhConfig.CityRoles[cty]);
                        }
                    }
                    else
                    {
                        //AddRaidBoss(ctx.User.Id, i, city);
                        if (!_dep.SubscriptionManager.AddRaid(ctx.User.Id, i, city))
                        {
                            _logger.Error($"Failed to add raid boss {i} in city {city} added to {ctx.User.Id} subscription list.");
                            continue;
                        }

                        _logger.Info($"Raid boss {i} in city {city} added to {ctx.User.Id} subscription list.");
                    }
                }

                await ctx.TriggerTypingAsync();
                await ctx.RespondAsync($"{ctx.User.Mention} Subscribed to **all** raid boss notifications{(string.IsNullOrEmpty(city) ? " from **all** areas" : $" from city **{city}**")}.");
                return;
            }

            var alreadySubscribed = new List<string>();
            var subscribed = new List<string>();

            var subscription = _dep.SubscriptionManager.GetUserSubscriptions(ctx.User.Id);

            foreach (var arg in poke.Replace(" ", "").Split(','))
            {
                if (!isSupporter && subscription.Raids.Count >= MaxRaidSubscriptions)
                {
                    await ctx.TriggerTypingAsync();
                    await ctx.RespondAsync($"{ctx.User.Mention} Non-supporter members have a limited notification amount of {MaxRaidSubscriptions} different raid bosses, please consider donating to lift this to every raid Pokemon. Otherwise you will need to remove some subscriptions in order to subscribe to new raid Pokemon.");
                    return;
                }

                var pokeId = arg.PokemonIdFromName();
                if (pokeId == 0)
                {
                    await ctx.TriggerTypingAsync();
                    await ctx.RespondAsync($"{ctx.User.Mention} Failed to find raid Pokemon {arg}.");
                    continue;
                }

                var pokemon = Database.Instance.Pokemon[pokeId];
                var result = false;
                if (string.IsNullOrEmpty(city))
                {
                    for (var cty = 0; cty < _dep.WhConfig.CityRoles.Count; cty++)
                    {
                        result |= _dep.SubscriptionManager.AddRaid(ctx.User.Id, pokeId, _dep.WhConfig.CityRoles[cty]);
                    }
                }
                else
                {
                    result |= _dep.SubscriptionManager.AddRaid(ctx.User.Id, pokeId, city);
                }

                if (result)
                {
                    subscribed.Add(pokemon.Name);
                }
                else
                {
                    alreadySubscribed.Add(pokemon.Name);
                }

                //if (_dep.Db.Exists(ctx.User.Id))
                //{
                //    //User has already subscribed before, check if their new requested sub already exists.
                //    if (_dep.Db[ctx.User.Id].Raids.Exists(x => x.PokemonId == pokeId && (string.IsNullOrEmpty(city) || string.Compare(city, x.City, true) == 0)))
                //    {
                //        alreadySubscribed.Add(pokemon.Name);
                //    }
                //    else
                //    {
                //        _dep.Db[ctx.User.Id].Raids.Add(new Pokemon { PokemonId = pokeId, City = city });
                //        subscribed.Add(pokemon.Name);
                //    }
                //}
                //else
                //{
                //    _dep.Db.Subscriptions.Add(new Subscription<Pokemon>(ctx.User.Id, new List<Pokemon>(), new List<Pokemon> { new Pokemon { PokemonId = pokeId, City = city } }));
                //    subscribed.Add(pokemon.Name);
                //}
            }

            _dep.SubscriptionManager.Save(subscription);

            await ctx.TriggerTypingAsync();
            await ctx.RespondAsync
            (
                (subscribed.Count > 0
                    ? $"{ctx.User.Mention} has subscribed to **{string.Join("**, **", subscribed)}** raid notifications{(string.IsNullOrEmpty(city) ? " from **all** areas" : $" from city **{city}**")}."
                    : string.Empty) +
                (alreadySubscribed.Count > 0
                    ? $" {ctx.User.Mention} is already subscribed to {string.Join(",", alreadySubscribed)} raid notifications{(string.IsNullOrEmpty(city) ? " from **all** areas" : $" from city **{city}**")}."
                    : string.Empty)
            );
        }

        [
            Command("raidmenot"),
            Description("Unsubscribe from one or more or even all subscribed raid boss notifications by pokedex number or name.")
        ]
        public async Task RaidMeNotAsync(CommandContext ctx,
            [Description("Pokemon name or id to unsubscribe from raid notifications.")] string poke,
            [Description("City to send the notification if the raid appears in otherwise if null all will be sent.")] string city = null)
        {
            if (string.Compare(city, All, true) != 0 && !string.IsNullOrEmpty(city))
            {
                if (_dep.WhConfig.CityRoles.Find(x => string.Compare(x.ToLower(), city.ToLower(), true) == 0) == null)
                {
                    await ctx.RespondAsync($"{ctx.User.Mention} Failed to find city role {city}. To see a list of valid city roles type the command `.cities` or `.feeds`.");
                    return;
                }
            }
            else
            {
                //Assign to all cities.
                city = string.Empty;
            }

            if (!_dep.SubscriptionManager.UserExists(ctx.User.Id))
            {
                await ctx.TriggerTypingAsync();
                await ctx.RespondAsync($"{ctx.User.Mention} is not subscribed to any raid notifications{(string.IsNullOrEmpty(city) ? " from **all** areas" : $" from city **{city}**")}.");
                return;
            }

            var notSubscribed = new List<string>();
            var unsubscribed = new List<string>();

            var subscription = _dep.SubscriptionManager.GetUserSubscriptions(ctx.User.Id);

            if (string.Compare(poke, All, true) == 0)
            {
                var result = await Confirm(ctx, $"{ctx.User.Mention} are you sure you want to remove **all** {subscription.Pokemon.Count.ToString("N0")} of your raid boss subscriptions? Please reply back with `y` or `yes` to confirm.");
                if (!result) return;

                if (!_dep.SubscriptionManager.RemoveAllRaids(ctx.User.Id))
                {
                    await ctx.TriggerTypingAsync();
                    await ctx.RespondAsync($"{ctx.User.Mention} Failed to remove all raid boss subscriptions.");
                    return;
                }

                await ctx.TriggerTypingAsync();
                await ctx.RespondAsync($"{ctx.User.Mention} has unsubscribed from **all** raid boss notifications!");
                return;
            }

            foreach (var arg in poke.Replace(" ", "").Split(','))
            {
                var pokeId = arg.PokemonIdFromName();
                if (pokeId == 0)
                {
                    await ctx.TriggerTypingAsync();
                    await ctx.RespondAsync($"{ctx.User.Mention} Failed to find raid boss Pokemon {arg}.");
                    continue;
                }

                var pokemon = Database.Instance.Pokemon[pokeId];
                var result = false;
                if (string.IsNullOrEmpty(city))
                {
                    for (var cty = 0; cty < _dep.WhConfig.CityRoles.Count; cty++)
                    {
                        result |= _dep.SubscriptionManager.RemoveRaid(ctx.User.Id, pokeId, _dep.WhConfig.CityRoles[cty]);
                    }
                }
                else
                {
                    result |= _dep.SubscriptionManager.RemoveRaid(ctx.User.Id, pokeId, city);
                }

                if (result)
                {
                    unsubscribed.Add(pokemon.Name);
                }
                else
                {
                    notSubscribed.Add(pokemon.Name);
                }
            }

            _dep.SubscriptionManager.Save(subscription);

            await ctx.TriggerTypingAsync();
            await ctx.RespondAsync
            (
                (unsubscribed.Count > 0
                    ? $"{ctx.User.Mention} has unsubscribed from **{string.Join("**, **", unsubscribed)}** raid notifications{(string.IsNullOrEmpty(city) ? " from **all** cities" : $" from city **{city}**")}."
                    : string.Empty) +
                (notSubscribed.Count > 0
                    ? $" {ctx.User.Mention} is not subscribed to {string.Join(",", notSubscribed)} raid notifications{(string.IsNullOrEmpty(city) ? " from **all** cities" : $" from city **{city}**")}."
                    : string.Empty)
            );
        }

        #region Private Methods

        private async Task SendUserSubscriptionSettings(DiscordClient client, DiscordUser receiver, ulong userId)
        {
            var discordUser = await client.GetUserAsync(userId);
            if (discordUser == null)
            {
                _logger.Error($"Failed to retreive user with id {userId}.");
                return;
            }

            var userSettings = await BuildUserSubscriptionSettings(client, discordUser);
            if (userSettings.Length > 2000)
                await client.SendDirectMessage(receiver, $"**{discordUser.Mention}**'s subscription list is longer than the allowed Discord message character count, here is a partial list:\r\n{userSettings.Substring(0, Math.Min(userSettings.Length, 1500))}```", null);
            else
                await client.SendDirectMessage(receiver, userSettings, null);
        }

        private async Task<string> BuildUserSubscriptionSettings(DiscordClient client, DiscordUser user)
        {
            var author = user.Id;
            var isSubbed = _dep.SubscriptionManager.UserExists(author);
            var subscription = _dep.SubscriptionManager.GetUserSubscriptions(user.Id);
            var hasPokemon = isSubbed && subscription?.Pokemon.Count > 0;
            var hasRaids = isSubbed && subscription?.Raids.Count > 0;
            var msg = string.Empty;
            var isSupporter = await client.IsSupporterOrHigher(author, _dep.WhConfig);

            if (hasPokemon)
            {
                var member = await client.GetMemberById(_dep.WhConfig.GuildId, author);
                if (member == null)
                {
                    return $"Failed to get discord member from id {author}.";
                }

                var feeds = new List<string>();
                foreach (var role in member.Roles)
                {
                    if (_dep.WhConfig.CityRoles.Contains(role.Name))
                    {
                        feeds.Add(role.Name);
                    }
                }

                var pokemon = subscription.Pokemon;
                pokemon.Sort((x, y) => x.PokemonId.CompareTo(y.PokemonId));

                var exceedsLimits = pokemon.Count > MaxPokemonDisplayed;
                var defaultIV = 0;
                var defaultCount = 0;
                var results = pokemon.GroupBy(p => p.MinimumIV, (key, g) => new { IV = key, Pokes = g.ToList() });
                foreach (var result in results)
                {
                    if (result.Pokes.Count > defaultIV)
                    {
                        defaultIV = result.IV;
                        defaultCount = result.Pokes.Count;
                    }
                }

                msg = $"**{user.Mention} Notification Settings:**\r\n";
                msg += $"Enabled: **{(subscription.Enabled ? "Yes" : "No")}**\r\n";
                msg += $"Feed Zones: **{string.Join("**, **", feeds)}**\r\n";
                msg += $"Pokemon Subscriptions: ({pokemon.Count}/{(isSupporter ? "∞" : MaxPokemonSubscriptions.ToString())} used)\r\n";
                msg += "```";

                if (exceedsLimits)
                {
                    msg += $"Default: {defaultIV}% ({defaultCount.ToString("N0")} unlisted)\r\n";
                }

                foreach (var sub in results)
                {
                    if (sub.IV == defaultIV && exceedsLimits) continue;

                    foreach (var poke in sub.Pokes)
                    {
                        var pkmn = Database.Instance.Pokemon[poke.PokemonId];
                        msg += $"{poke.PokemonId}: {pkmn.Name} {poke.MinimumIV}%+{(poke.MinimumLevel > 0 ? $", L{poke.MinimumLevel}+" : null)}\r\n";
                    }
                }
                msg += "```" + Environment.NewLine + Environment.NewLine;
            }

            if (hasRaids)
            {
                msg += $"Raid Subscriptions: ({subscription.Raids.Count.ToString("N0")}/{(isSupporter ? "∞" : MaxRaidSubscriptions.ToString())} used)\r\n";
                msg += "```";
                msg += string.Join(Environment.NewLine, GetRaidSubscriptionNames(author));
                msg += "```";
            }

            if (string.IsNullOrEmpty(msg))
            {
                msg = $"**{user.Mention}** is not subscribed to any Pokemon or Raid notifications.";
            }

            return msg;
        }

        private List<string> GetPokemonSubscriptionNames(ulong userId)
        {
            var list = new List<string>();
            if (!_dep.SubscriptionManager.UserExists(userId))
                return list;

            var subscription = _dep.SubscriptionManager.GetUserSubscriptions(userId);
            var subscribedPokemon = subscription.Pokemon;
            subscribedPokemon.Sort((x, y) => x.PokemonId.CompareTo(y.PokemonId));

            foreach (var poke in subscribedPokemon)
            {
                if (!Database.Instance.Pokemon.ContainsKey(poke.PokemonId))
                    continue;

                var pokemon = Database.Instance.Pokemon[poke.PokemonId];
                if (pokemon == null)
                    continue;

                list.Add(pokemon.Name);
            }

            return list;
        }

        private List<string> GetRaidSubscriptionNames(ulong userId)
        {
            var list = new List<string>();
            if (!_dep.SubscriptionManager.UserExists(userId))
                return list;

            var subscription = _dep.SubscriptionManager.GetUserSubscriptions(userId);
            var subscribedRaids = subscription.Raids;
            subscribedRaids.Sort((x, y) => x.PokemonId.CompareTo(y.PokemonId));

            foreach (var poke in subscribedRaids)
            {
                if (!Database.Instance.Pokemon.ContainsKey(poke.PokemonId))
                    continue;

                var pokemon = Database.Instance.Pokemon[poke.PokemonId];
                if (pokemon == null)
                    continue;

                list.Add($"{pokemon.Name} (From: {(string.IsNullOrEmpty(poke.City) ? "All Areas" : poke.City)})");
            }

            return list;
        }

        private bool IsCommonPokemon(int pokeId)
        {
            var commonPokemon = new List<int>
            {
                1, //Bulbasaur
                4, //Charmander
                //7, //Squirtle
                10, //Caterpie
                13, //Weedle
                16, //Pidgey
                17, //Pidgeotto
                19, //Rattata
                20, //Raticate
                21, //Spearow
                23, //Ekans
                25, //Pikachu
                27, //Sandshrew
                29, //Nidoran Female
                32, //Nidoran Male
                41, //Zubat
                46, //Paras
                48, //Venonat
                50, //Diglett
                52, //Meowth
                104, //Cubone
                133, //Eevee
                152, //Chikorita
                155, //Cyndaquil
                161, //Sentret
                163, //Noothoot
                165, //Ledyba
                167, //Spinarak
                177, //Natu
                187, //Hoppip
                191, //Sunkern
                193, //Yanma
                194, //Wooper
                198, //Murkrow
                209, //Snubbull
                228, //Houndour
                252, //Treecko
                255, //Torchic
                261, //Poochyena
                263, //Zigzagoon
                265, //Wurmple
                273, //Seedot
                276, //Taillow
                293, //Whismur
                300, //Skitty
                307, //Meditite
                309, //Electrike
                315, //Roselia
                316, //Gulpin
                322, //Numel
                325, //Spoink
                331, //Cacnea
                333, //Swablu
                363, //Spheal

            };
            return commonPokemon.Contains(pokeId);
        }

        #endregion

        private static ulong ConvertMentionToUserId(string mention)
        {
            //<@201909896357216256>
            //mention = Utils.GetBetween(mention, "<", ">");
            mention = mention.Replace("<", null);
            mention = mention.Replace(">", null);
            mention = mention.Replace("@", null);
            mention = mention.Replace("!", null);

            return ulong.TryParse(mention, out ulong result) ? result : 0;
        }

        private const string ConfirmRegex = "\\b[Yy][Ee]?[Ss]?\\b|\\b[Nn][Oo]?\\b";
        private const string YesRegex = "[Yy][Ee]?[Ss]?";
        private const string NoRegex = "[Nn][Oo]?";

        private static async Task<bool> Confirm(CommandContext ctx, string message)
        {
            //await ctx.RespondAsync(message);
            //var interactivity = ctx.Client.GetModule<InteractivityModule>();
            //var m = await interactivity.WaitForMessageAsync(
            //    x => x.Channel.Id == ctx.Channel.Id
            //    && x.Author.Id == ctx.Member.Id
            //    && Regex.IsMatch(x.Content, ConfirmRegex));

            //return Regex.IsMatch(m.Message.Content, YesRegex);
            ////if (Regex.IsMatch(m.Message.Content, YesRegex))
            ////    await ctx.RespondAsync("Confirmation Received");
            ////else
            ////    await ctx.RespondAsync("Confirmation Denied");
            //TODO: InteractivityModule
            return await Task.FromResult(true);
        }
    }
}