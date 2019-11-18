using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AsmodatStandard.Extensions;
using AsmodatStandard.Extensions.Collections;
using ICFaucet.Models;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace ICFaucet
{
    public static class FHelper
    {
        public static async Task<string> GetInviteLink(this TelegramBotClient TBC, Chat chat)
        {
            if (!chat.Username.IsNullOrEmpty())
                return $"@{chat.Username}";

            if (!chat.InviteLink.IsNullOrEmpty())
                return $"{chat.InviteLink}";

            var chatInfo = await TBC.GetChatAsync(new ChatId(chat.Id));

            if (!chatInfo.InviteLink.IsNullOrEmpty())
                return $"{chat.InviteLink}";

            if (!chatInfo.Username.IsNullOrEmpty())
                return $"@{chat.Username}";

            return null;
        }

        public static bool IsActiveMember(this ChatMember member)
        {
            if (member == null)
                return false;

            if (member.IsMember == true)
                return true;

            if (member.Status == ChatMemberStatus.Administrator)
                return true;
            if (member.Status == ChatMemberStatus.Creator)
                return true;
            if (member.Status == ChatMemberStatus.Member)
                return true;

            return false;
        }

        public static async Task<User> TryGetChatMember(this TelegramBotClient TBC, ChatId chat, int user)
        {
            try
            {
                return (await TBC.GetChatMemberAsync(chat, user))?.User;
            }
            catch (Telegram.Bot.Exceptions.ApiRequestException ex)
            {
                throw null;
            }
        }

        public static async Task<bool> IsChatMember(this TelegramBotClient TBC, ChatId chat, User user)
        {
            ChatMember member;
            try
            {
                 member = await TBC.GetChatMemberAsync(chat, user.Id);
            }
            catch(Telegram.Bot.Exceptions.ApiRequestException ex)
            {
                if (ex.Message.ToLower().Contains("user is not a member"))
                    return false;

                throw ex;
            }

            return member.IsActiveMember();
        }

        public static string GetMarkDownUsername(this User user)
        {
            if (!user.Username.IsNullOrWhitespace())
                return $"@{user.Username}";

            var name = $"{user.FirstName?.Trim() ?? ""} {user.FirstName?.Trim() ?? ""}".Trim();
            return $"[{name}](tg://user?id={user.Id})";
        }

        public static string[] GetAllLCDs()
        {
            var lcds = new List<string>();
            foreach (System.Collections.DictionaryEntry env in Environment.GetEnvironmentVariables())
            {
                var propsKey = (string)env.Key;
                var propsVar = (string)env.Value;

                if (propsKey.IsNullOrWhitespace() || propsVar.IsNullOrWhitespace() || !propsKey.EndsWith("_PROPS"))
                    continue;

                TokenProps props = null;

                if (!propsVar.IsNullOrEmpty())
                    props = propsVar.JsonDeserialize<TokenProps>();

                if (props == null || props.lcd.IsNullOrWhitespace())
                    continue;

                lcds.Add(props.lcd);
            }

            return lcds.ToArray();
        }
    }
}
