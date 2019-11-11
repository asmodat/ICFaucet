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
        private async Task<TokenProps> GetTokenProps(Message m) // verify that user is a part of the master chat
        {
            var text = m.Text?.Trim();
            var args = text.Split(" ");
            var cliArgs = CLIHelper.GetNamedArguments(args);

            var token = args.TryGetValueOrDefault(2)?.TrimStartSingle("$"); // $ATOM
            var baseName = token?.ToUpper();

            if (token.IsNullOrEmpty())
                token = cliArgs.GetValueOrDefault("token");

            token = token?.ToLower();
            var propsVar = Environment.GetEnvironmentVariable($"{token?.ToLower()}_PROPS");
            TokenProps props;

            if (!propsVar.IsNullOrEmpty())
                props = propsVar.JsonDeserialize<TokenProps>();
            else
                props = new TokenProps();

            if (token.IsNullOrWhitespace() || token.Length < 2 || token.Length > 10) // vlaidate token name
            {
                await _TBC.SendTextMessageAsync(text: $"Token name `${token ?? "undefined"}` is invalid  or not defined.", chatId: new ChatId(m.Chat.Id), replyToMessageId: m.MessageId, parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
                return null;
            }

            props.name = token;

            if (props.index < 0 || props.index > 99999999)
                props.index = (cliArgs.GetValueOrDefault("index")).ToIntOrDefault(BitcoinEx.GetCoinIndex(baseName));

            if (props.index < 0 || props.index > 99999999) // vlaidate coin index
            {
                await _TBC.SendTextMessageAsync(text: $"*index* flag `{props.index}` is invalid.", chatId: new ChatId(m.Chat.Id), replyToMessageId: m.MessageId, parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
                return null;
            }

            props.address = (args.TryGetValueOrDefault(3)?.Trim() ?? cliArgs.GetValueOrDefault("address"));

            if (props.address.IsNullOrWhitespace() || !Bech32Ex.TryDecode(props.address, out var hrp, out var addrBytes)) // vlaidate coin index
            {
                await _TBC.SendTextMessageAsync(text: $"*address* flag `{props.address ?? "undefined"}` is invalid.", chatId: new ChatId(m.Chat.Id), replyToMessageId: m.MessageId, parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
                return null;
            }

            props.prefix = hrp;

            var lcd = cliArgs.GetValueOrDefault("lcd");
            if (!lcd.IsNullOrWhitespace())
                props.lcd = lcd;

            var client = new CosmosHub(lcd: props.lcd);
            node_info nodeInfo;
            try
            {
                nodeInfo = await client.GetNodeInfo();
            }
            catch
            {
                await _TBC.SendTextMessageAsync(text: $"*lcd* flag `{props.lcd ?? "undefined"}` is invalid or node can NOT be reached.", chatId: new ChatId(m.Chat.Id), replyToMessageId: m.MessageId, parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
                return null;
            }

            var network = cliArgs.GetValueOrDefault("network");
            if (network.IsNullOrWhitespace())
                network = nodeInfo?.network;
            if (network.IsNullOrWhitespace())
                network = props.network;
            props.network = network;

            if (props.network.IsNullOrWhitespace() || props.network.Length <= 1 || props.network.Length >= 20)
            {
                await _TBC.SendTextMessageAsync(text: $"*network* flag `{props.network ?? "undefined"}` is invalid.", chatId: new ChatId(m.Chat.Id), replyToMessageId: m.MessageId, parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
                return null;
            }

            var denom = cliArgs.GetValueOrDefault("denom");
            if (!denom.IsNullOrWhitespace())
                denom = props?.denom;
            if (denom.IsNullOrEmpty())
                denom = props.name.ToLower();
            props.denom = denom;

            return props;
        }
    }
}
