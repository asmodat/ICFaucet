using System.Threading.Tasks;
using AsmodatStandard.Extensions;
using AsmodatStandard.Extensions.Collections;
using System.Collections.Generic;
using System.Linq;
using Telegram.Bot.Types;
using ICWrapper.Cosmos.CosmosHub;
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

            var props = GetTokenPropsFromTextCommand(text);
            var baseName = props?.name?.ToUpper();

            if (props.name.IsNullOrWhitespace() || props.name.Length < 2 || props.name.Length > 10) // validate token name
            {
                await _TBC.SendTextMessageAsync(text: $"Token name `${props.name ?? "undefined"}` is invalid.\nCheck description to see allowed parameters.", chatId: new ChatId(m.Chat.Id), replyToMessageId: m.MessageId, parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
                return null;
            }

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

            if (props.network.IsNullOrWhitespace() || props.network.Length <= 1)
            {
                await _TBC.SendTextMessageAsync(text: $"*network* flag `{props.network ?? "undefined"}` is invalid.\nCheck description to see allowed parameters.", chatId: new ChatId(m.Chat.Id), replyToMessageId: m.MessageId, parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
                return null;
            }

            props.denom = cliArgs.GetValueOrDefault("denom") ?? props.denom ?? props.name.ToLower();
            return props;
        }
    }
}
