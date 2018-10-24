﻿namespace WhMgr.Extensions
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    using DSharpPlus;
    using DSharpPlus.CommandsNext;
    using DSharpPlus.Entities;

    using WhMgr.Configuration;
    using WhMgr.Diagnostics;

    public static class DiscordExtensions
    {
        private static readonly IEventLogger _logger = EventLogger.GetLogger();

        public static async Task<DiscordMessage> SendDirectMessage(this DiscordClient client, DiscordUser user, DiscordEmbed embed)
        {
            if (embed == null)
                return null;

            return await client.SendDirectMessage(user, string.Empty, embed);
        }

        public static async Task<DiscordMessage> SendDirectMessage(this DiscordClient client, DiscordUser user, string message, DiscordEmbed embed)
        {
            try
            {
                var dm = await client.CreateDmAsync(user);
                if (dm != null)
                {
                    var msg = await dm.SendMessageAsync(message, false, embed);
                    return msg;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
            }

            return null;
        }

        public static async Task<DiscordMember> GetMemberById(this DiscordClient client, ulong guildId, ulong id)
        {
            var guild = await client.GetGuildAsync(guildId);
            if (guild == null)
            {
                _logger.Error($"Failed to get guild from id {guildId}.");
                return null;
            }

            var member = guild?.Members?.FirstOrDefault(x => x.Id == id);
            if (member == null)
            {
                _logger.Error($"Failed to get member from id {id}.");
                return null;
            }

            return member;
        }

        public static async Task<DiscordMessage> DonateUnlockFeaturesMessage(this CommandContext ctx, bool triggerTyping = true)
        {
            if (triggerTyping)
            {
                await ctx.TriggerTypingAsync();
            }
            return await ctx.RespondAsync($"{ctx.User.Mention} This feature is only available to supporters, please donate to unlock this feature and more. Donation information can be found by typing the `.donate` command.");
        }

        internal static async Task<bool> IsDirectMessageSupported(this DiscordMessage message)
        {
            if (message.Channel.Guild == null)
            {
                await message.RespondAsync("DM is not supported for this command yet.");
                return false;
            }

            return true;
        }

        public static async Task<bool> IsSupporterOrHigher(this DiscordClient client, ulong userId, WhConfig config)
        {
            try
            {
                var isAdmin = userId == config.OwnerId;
                if (isAdmin)
                    return true;

                var isModerator = config.Moderators.Contains(userId);
                if (isModerator)
                    return true;

                var isSupporter = await client.HasSupporterRole(userId, config.GuildId, config.SupporterRoleId);
                if (isSupporter)
                    return true;

                //TODO: Check TeamElite role.
                //var isElite = await client.HasSupporterRole(userId, config.TeamEliteRoleId);
                //if (isElite)
                    //return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
            }

            return false;
        }

        public static bool IsModeratorOrHigher(this ulong userId, WhConfig config)
        {
            var isAdmin = IsAdmin(userId, config.OwnerId);
            if (isAdmin)
                return true;

            var isModerator = config.Moderators.Contains(userId);
            if (isModerator)
                return true;

            return false;
        }

        public static bool IsModerator(this ulong userId, WhConfig config)
        {
            return config.Moderators.Contains(userId);
        }

        public static bool IsAdmin(this ulong userId, ulong ownerId)
        {
            return userId == ownerId;
        }

        public static async Task<bool> HasSupporterRole(this DiscordClient client, ulong guildId, ulong userId, ulong supporterRoleId)
        {
            var member = await client.GetMemberById(guildId, userId);
            if (member == null)
            {
                _logger.Error($"Failed to get user with id {userId}.");
                return false;
            }

            return member.HasSupporterRole(supporterRoleId);
        }

        public static bool HasSupporterRole(this DiscordMember member, ulong supporterRoleId)
        {
            return HasRole(member, supporterRoleId);
        }

        public static async Task<bool> HasModeratorRole(this DiscordClient client, ulong guildId, ulong userId, ulong moderatorRoleId)
        {
            var member = await client.GetMemberById(guildId, userId);
            if (member == null)
            {
                _logger.Error($"Failed to get user with id {userId}.");
                return false;
            }

            return member.HasModeratorRole(moderatorRoleId);
        }

        public static bool HasModeratorRole(this DiscordMember member, ulong moderatorRoleId)
        {
            return HasRole(member, moderatorRoleId);
        }

        public static bool HasRole(this DiscordMember member, ulong roleId)
        {
            var role = member.Roles.FirstOrDefault(x => x.Id == roleId);
            return role != null;
        }

        public static bool HasRole(this DiscordClient client, DiscordMember member, string roleName)
        {
            var role = client.GetRoleFromName(roleName);
            if (role == null) return false;

            return HasRole(member, role.Id);
        }

        public static DiscordRole GetRoleFromName(this DiscordClient client, string roleName)
        {
            foreach (var guild in client.Guilds)
            {
                var role = guild.Value.Roles.FirstOrDefault(x => string.Compare(x.Name, roleName, true) == 0);
                if (role != null)
                    return role;
            }

            return null;
        }
    }
}