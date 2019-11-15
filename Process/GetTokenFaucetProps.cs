﻿using System;
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
        private async Task<TokenProps> GetTokenFaucetProps(Message m) // verify that user is a part of the master chat
        {
            var text = m.Text?.Trim();
            var args = text.Split(" ");
            var cliArgs = CLIHelper.GetNamedArguments(args);
            var user = m.From;

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

            if (token.IsNullOrWhitespace() || token.Length < 2 || token.Length > 10) // validate token name
            {
                await _TBC.SendTextMessageAsync(text: $"Token name `${token ?? "undefined"}` is invalid.\nCheck description to see allowed parameters.", chatId: new ChatId(m.Chat.Id), replyToMessageId: m.MessageId, parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
                return null;
            }

            props.name = token;

            if (props.index < 0 || props.index > 99999999)
                props.index = (cliArgs.GetValueOrDefault("index")).ToIntOrDefault(BitcoinEx.GetCoinIndex(baseName));

            if (props.index < 0 || props.index > 99999999) // vlaidate coin index
            {
                await _TBC.SendTextMessageAsync(text: $"*index* flag `{props.index}` is invalid.\nCheck description to see allowed parameters.", chatId: new ChatId(m.Chat.Id), replyToMessageId: m.MessageId, parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
                return null;
            }

            props.address = cliArgs.GetValueOrDefault("address") ?? args.FirstOrDefault(x => Bech32Ex.CanDecode(x));

            if (Bech32Ex.TryDecode(props.address, out var hrp, out var addrBytes))
                props.prefix = hrp;

            props.prefix = props.prefix ?? cliArgs.GetValueOrDefault("prefix");

            if (props.prefix.IsNullOrWhitespace())
            {
                await _TBC.SendTextMessageAsync(text: $"*prefix* ({props.prefix ?? "undefined"}) or *address* ({props.address ?? "undefined"}) flag was not defined.\nCheck description to see allowed parameters.",
                    chatId: new ChatId(m.Chat.Id), replyToMessageId: m.MessageId, parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
                return null;
            }

            if (!Bech32Ex.CanDecode(props.address))
            {
                var ua = await GetUserAccount(user, createNewAcount: false);
                var acc = new AsmodatStandard.Cryptography.Cosmos.Account(props.prefix, (uint)props.index);
                acc.InitializeWithMnemonic(ua.GetSecret());
                props.address = acc.CosmosAddress;
            }

            var lcd = cliArgs.GetValueOrDefault("lcd");
            if (!lcd.IsNullOrWhitespace())
                props.lcd = lcd;

            var client = new CosmosHub(lcd: props.lcd, timeoutSeconds: _cosmosHubClientTimeout);
            node_info nodeInfo;
            try
            {
                nodeInfo = await client.GetNodeInfo();
            }
            catch
            {
                await _TBC.SendTextMessageAsync(text: $"*lcd* flag `{props.lcd ?? "undefined"}` is invalid or node can NOT be reached.\nCheck description to see allowed parameters.", chatId: new ChatId(m.Chat.Id), replyToMessageId: m.MessageId, parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
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
                await _TBC.SendTextMessageAsync(text: $"*network* flag `{props.network ?? "undefined"}` is invalid.\nCheck description to see allowed parameters.", chatId: new ChatId(m.Chat.Id), replyToMessageId: m.MessageId, parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
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