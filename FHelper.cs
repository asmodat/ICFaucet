using System;
using System.Diagnostics;
using System.Threading.Tasks;
using AsmodatStandard.Extensions;
using AsmodatStandard.Extensions.Collections;
using Amazon.Lambda.Core;

using AsmodatStandard.Networking;
//using AWSWrapper.SM;
using Telegram.Bot;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.Json;

using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Amazon.Lambda.APIGatewayEvents;
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
    }
}
