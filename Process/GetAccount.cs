using System;
using System.Threading.Tasks;
using AsmodatStandard.Extensions;
using AsmodatStandard.Extensions.Collections;
using System.Collections.Generic;
using Telegram.Bot.Types;
using AsmodatStandard.Extensions.Security;
using AsmodatStandard.IO;
using ICFaucet.Models;
using AsmodatStandard.Extentions.Cryptography;
using ICWrapper.Cosmos.CosmosHub;
using ICWrapper.Cosmos.CosmosHub.Models;
using System.Linq;

namespace ICFaucet
{
    public partial class Function
    {
        private async Task<bool> GetAccount(Message m) // verify that user is a part of the master chat
        {
            var text = m.Text?.Trim();
            var user = m.From;
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

            props.name = token;

            var account = args.TryGetValueOrDefault(3)?.Trim()?.ToLower();

            props.memo = cliArgs.GetValueOrDefault("memo") ?? "";
            if (!props.memo.IsNullOrEmpty() && props.memo.Length > 256)
            {
                await _TBC.SendTextMessageAsync(text: $"*memo* can't have more then 256 characters, but was {props.memo.Length} characters.\nCheck description to see allowed parameters.",
                    chatId: new ChatId(m.Chat.Id), replyToMessageId: m.MessageId, parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
                return true;
            }

            if (props.index < 0 || props.index > 99999999)
                props.index = (cliArgs.GetValueOrDefault("index")).ToIntOrDefault(BitcoinEx.GetCoinIndex(baseName));

            if (props.index < 0 || props.index > 99999999) // vlaidate coin index
            {
                await _TBC.SendTextMessageAsync(text: $"*index* flag `{props.index}` is invalid.\nCheck description to see allowed parameters.", chatId: new ChatId(m.Chat.Id), replyToMessageId: m.MessageId, parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
                return true;
            }

            if(props.prefix.IsNullOrWhitespace())
                props.prefix = cliArgs.GetValueOrDefault("prefix");

            if (props.prefix.IsNullOrWhitespace()) // vlaidate address prefix
            {
                await _TBC.SendTextMessageAsync(text: $"*prefix* flag `{props.address ?? "undefined"}` is invalid.\nCheck description to see allowed parameters.", chatId: new ChatId(m.Chat.Id), replyToMessageId: m.MessageId, parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
                return true;
            }

            var ua = await GetUserAccount(user.Id);
            var acc = new AsmodatStandard.Cryptography.Cosmos.Account(props.prefix, (uint)props.index);
            acc.InitializeWithMnemonic(ua.GetSecret());
            var cosmosAdress = acc.CosmosAddress;

            if (account.EquailsAny(StringComparison.OrdinalIgnoreCase, "address", "account", "acc", "addr", "a", "deposit", "d", "adr", "adres", "adress", "addres"))
            {
                await _TBC.SendTextMessageAsync(chatId: m.Chat,
                        $"{user.GetMarkDownUsername()} Public Address Is:\n`{cosmosAdress}`",
                        replyToMessageId: m.MessageId,
                        parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);

                return true;
            }
            else if (account.EquailsAny(StringComparison.OrdinalIgnoreCase, "balance"))
            {
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
                    return true;
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
                    return true;
                }

                var denom = cliArgs.GetValueOrDefault("denom");
                if (!denom.IsNullOrWhitespace())
                    denom = props?.denom;
                if (denom.IsNullOrEmpty())
                    denom = props.name.ToLower();
                props.denom = denom;

                var fromAccountInfo = await client.GetAccount(account: cosmosAdress);
                var fromAccountBalance = fromAccountInfo?.coins?.FirstOrDefault(x => x?.denom?.ToLower() == props.denom);
                props.denom = fromAccountBalance?.denom ?? props.denom;

                await _TBC.SendTextMessageAsync(chatId: m.Chat,
                        $"{user.GetMarkDownUsername()} Account Balance:\n" +
                        $"Address: `{cosmosAdress}`\n" +
                        $"Amount: `{fromAccountBalance?.amount ?? "0"} {props.denom}`\n" +
                        $"Network: `{props.network}`",
                        replyToMessageId: m.MessageId,
                        parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);

                return true;
            }
            else
            {
                await _TBC.SendTextMessageAsync(text: $"Command `{account ?? "undefined"}` is invalid.\nCheck description to see allowed parameters.", chatId: new ChatId(m.Chat.Id), replyToMessageId: m.MessageId, parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
                return false;
            }
        }
    }
}
