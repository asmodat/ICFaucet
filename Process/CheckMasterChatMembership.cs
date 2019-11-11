using System;
using System.Diagnostics;
using System.Threading.Tasks;
using AsmodatStandard.Extensions;
using AsmodatStandard.Extensions.Collections;
using Amazon.Lambda.Core;

using AsmodatStandard.Networking;
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
using AsmodatStandard.Extensions.Security;
using AsmodatStandard.Cryptography.Bitcoin;
using ICWrapper.Cosmos.CosmosHub;
using System.Threading;
using AsmodatStandard.Extensions.Threading;
using ICWrapper.Cosmos.CosmosHub.Models;
using AsmodatStandard.IO;
using ICFaucet.Models;
using AsmodatStandard.Extentions.Cryptography;

namespace ICFaucet
{
    public partial class Function
    {
        private async Task<bool> CheckMasterChatMembership(Message m) // verify that user is a part of the master chat
        {
            var chatId = m.Chat.Id;
            var user = m.From;

            if (chatId == _masterChatId.Identifier) // do not check if message originates from masterchat
                return true;

            if (await _TBC.IsChatMember(_masterChatId, m.From))
                return true;

            var inviteLink = await GetMasterChatInviteLink();
            await _TBC.SendTextMessageAsync(
                chatId: new ChatId(chatId),
                $"You must be a member of *{_masterChat.Title}*\nJoin us here: {inviteLink}.", 
                replyToMessageId: m.MessageId,
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);

            return false;
        }

    }
}
