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
        private string _masterChatInviteLink = null;
        private Chat _masterChat;
        private async Task<string> GetMasterChatInviteLink()
        {
            if (!_masterChatInviteLink.IsNullOrEmpty())
                return _masterChatInviteLink;

            _masterChat = await _TBC.GetChatAsync(new ChatId(_masterChatId));
            _masterChatInviteLink = await _TBC.GetInviteLink(_masterChat);
            return _masterChatInviteLink;
        }
    }
}
