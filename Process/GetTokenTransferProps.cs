using System;
using System.Threading.Tasks;
using AsmodatStandard.Extensions;
using AsmodatStandard.Extensions.Collections;
using System.Collections.Generic;
using System.Linq;
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
        public static TokenProps GetTokenPropsFromTextCommand(string text)
        {
            var args = text.Split(" ");
            var cliArgs = CLIHelper.GetNamedArguments(args);

            var token = args.FirstOrDefault(x => x.StartsWith("$"))?.TrimStart("$");

            if (token.IsNullOrEmpty())
                token = cliArgs.GetValueOrDefault("token");

            token = token?.ToLower();
            var propsVar = Environment.GetEnvironmentVariable($"{token}_PROPS");
            TokenProps props;

            if (!propsVar.IsNullOrEmpty())
                props = propsVar.JsonDeserialize<TokenProps>();
            else
                props = new TokenProps();

            props.name = token;

            var lcd = cliArgs.GetValueOrDefault("lcd");
            if (!lcd.IsNullOrWhitespace())
                props.lcd = lcd;

            props.fees = cliArgs.GetValueOrDefault("fees").ToBigIntOrDefault(props.fees);
            props.gas = cliArgs.GetValueOrDefault("gas", cliArgs.GetValueOrDefault("gass")).ToBigIntOrDefault(props.gas);

            return props;
        }

        private async Task<TokenProps> GetTokenTransferProps(Chat chat, int replyMessageId, string text, long from, string to) // verify that user is a part of the master chat
        {
            var args = text.Split(" ");
            var cliArgs = CLIHelper.GetNamedArguments(args);

            var props = GetTokenPropsFromTextCommand(text);
            var baseName = props?.name?.ToUpper();

            if (props.name.IsNullOrWhitespace() || props.name.Length < 2 || props.name.Length > 10) // validate token name
            {
                await _TBC.SendTextMessageAsync(text: $"Token name `${props.name ?? "undefined"}` is invalid.\nCheck description to see allowed parameters.", 
                    chatId: chat, 
                    replyToMessageId: replyMessageId, parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
                return null;
            }

            props.amount = (args.FirstOrDefault(x => x.Trim().IsDigits()).Trim() ?? cliArgs.GetValueOrDefault("amount")).ToLongOrDefault(0);

            if (props.index < 0 || props.index > 99999999)
                props.index = (cliArgs.GetValueOrDefault("index")).ToIntOrDefault(BitcoinEx.GetCoinIndex(baseName));

            if (props.index < 0 || props.index > 99999999) // vlaidate coin index
            {
                await _TBC.SendTextMessageAsync(text: $"*index* flag `{props.index}` is invalid.\nCheck description to see allowed parameters.", 
                    chatId: chat, replyToMessageId: replyMessageId, parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
                return null;
            }

            if (Bech32Ex.TryDecode(to, out var hrp, out var addrBytes))
            {
                props.prefix = hrp;
                props.address = to;
            }

            props.prefix = cliArgs.GetValueOrDefault("prefix") ?? props.prefix;

            if (props.prefix.IsNullOrWhitespace())
            {
                await _TBC.SendTextMessageAsync(text: $"*prefix* ({props.prefix ?? "undefined"}) or *address* ({props.address ?? "undefined"}) flag was not defined.\nCheck description to see allowed parameters.",
                        chatId: chat, replyToMessageId: replyMessageId, parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
                return null;
            }

            var toUserId = to.ToLongOrDefault(0);
            if (toUserId != 0)
            {
                var toUA = await GetUserAccount(toUserId, createNewAcount: false);
                var toAcc = new AsmodatStandard.Cryptography.Cosmos.Account(props.prefix, (uint)props.index);
                toAcc.InitializeWithMnemonic(toUA.GetSecret());
                props.address = toAcc.CosmosAddress;
            }

            if (!Bech32Ex.CanDecode(props.address)) // validate address
            {
                await _TBC.SendTextMessageAsync(text: $"*address* flag `{props.address ?? "undefined"}` is invalid.\nCheck description to see allowed parameters.",
                    chatId: chat, replyToMessageId: replyMessageId, parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
                return null;
            }

            var fromUA = await GetUserAccount(from, createNewAcount: false);
            var fromAcc = new AsmodatStandard.Cryptography.Cosmos.Account(props.prefix, (uint)props.index);
            fromAcc.InitializeWithMnemonic(fromUA.GetSecret());
            props.origin = fromAcc.CosmosAddress;

            var client = new CosmosHub(lcd: props.lcd, timeoutSeconds: _cosmosHubClientTimeout);
            node_info nodeInfo;
            try
            {
                nodeInfo = await client.GetNodeInfo();
            }
            catch
            {
                await _TBC.SendTextMessageAsync(text: $"*lcd* flag `{props.lcd ?? "undefined"}` is invalid or node can NOT be reached.\nCheck description to see allowed parameters.", 
                    chatId: chat, replyToMessageId: replyMessageId, parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
                return null;
            }

            var network = cliArgs.GetValueOrDefault("network");
            if (network.IsNullOrWhitespace())
                network = nodeInfo?.network;
            if (network.IsNullOrWhitespace())
                network = props.network;
            props.network = network;

            if (props.network.IsNullOrWhitespace() || props.network.Length <= 1)
            {
                await _TBC.SendTextMessageAsync(text: $"*network* flag `{props.network ?? "undefined"}` is invalid.\nCheck description to see allowed parameters.", 
                    chatId: chat, replyToMessageId: replyMessageId, parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
                return null;
            }

            props.denom = cliArgs.GetValueOrDefault("denom") ?? props.denom ?? props.name.ToLower();
            return props;
        }
    }
}
