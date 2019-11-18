using System;
using System.Threading.Tasks;
using AsmodatStandard.Extensions;
using AsmodatStandard.Extensions.Collections;
using System.Collections.Generic;
using System.Linq;
using Telegram.Bot.Types;
using AsmodatStandard.Extensions.Security;
using ICWrapper.Cosmos.CosmosHub;
using System.Threading;
using AsmodatStandard.Extensions.Threading;
using ICWrapper.Cosmos.CosmosHub.Models;
using System.Collections.Concurrent;
using AsmodatStandard.IO;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using ICFaucet.Models;
using AsmodatStandard.Extentions.Cryptography;

namespace ICFaucet
{
    public partial class Function
    {
        public static SemaphoreSlim _ssLocker = new SemaphoreSlim(1, 1);
        private ConcurrentDictionary<string, long> sequences = new ConcurrentDictionary<string, long>();

        private async Task ProcessMessage(Message m)
        {
            var chat = m.Chat;
            var user = m.From;
            var text = (m.Text?.Trim() ?? "").Trim('\'','"','`','*','[',']');
            var textLower = text.ToLower();
            if (user?.IsBot != false)
                return;

            if (chat.Id == user?.Id && await PrivateProcess(m))
                return;

            if (textLower.StartsWith("give me $") || 
                textLower.StartsWith("get me $") ||
                textLower.StartsWith("show me the money! $"))
            {
                await FaucetProcessMessage(m);
                return;
            }

            if (textLower.StartsWith("get my $") ||
                textLower.StartsWith("show my $") ||
                textLower.StartsWith("give my $"))
            {
                if (!await this.CheckMasterChatMembership(m))
                    return;

                await GetAccount(m);
                return;
            }

            if (textLower.Contains(" $") && textLower.Split().Any(x => x.IsDigits()) &&
                (textLower.StartsWith("tip ") || textLower.StartsWith("tx ")))
            {
                await TransactionProcessMessage(m);
                return;
            }

            if (textLower.StartsWith("get tx") ||
                textLower.StartsWith("show tx") ||
                textLower.StartsWith("query tx"))
            {
                if (!await this.CheckMasterChatMembership(m))
                    return;

                await TxHashDiscovery(m);
                return;
            }
        }


        public async Task TxHashDiscovery(Message msg)
        {
            var chat = msg?.Chat;
            var text = $"{msg?.Text} {msg?.ReplyToMessage?.Text} {msg?.ReplyToMessage?.ReplyToMessage?.Text}";

            if (chat == null || text.IsNullOrWhitespace())
                return;

            var arr = text.Split();
            string hash = null;
            foreach (var s in arr)
            {
                hash = s.Trim("*", "`", "\"", "'", " ", "[", "]", "(", ")", "\n", "\r");
                if (hash.Length > 32 && hash.IsHex())
                    break;
            }

            if (!hash.IsHex())
                return;

            var props = GetTokenPropsFromTextCommand(text);

            var lcds = FHelper.GetAllLCDs();
            if (!props.lcd.IsNullOrEmpty())
                lcds = lcds.Prepend(props.lcd).ToArray();

            string error = null, gas = null, height = null, timestamp = null, network = null, output = null, log = null;
            TxsResponse txs = null;

            if(lcds.IsNullOrEmpty())
            {
                await _TBC.SendTextMessageAsync(chatId: chat, "*lcd* property was not found", replyToMessageId: msg.MessageId, parseMode: ParseMode.Markdown);
                return;
            }

            for (int i = 0; i < lcds.Length; i++)
            {
                error = null;
                var client = new CosmosHub(lcd: lcds[i], timeoutSeconds: _cosmosHubClientTimeout);
                var keystrokeTask = _TBC.SendChatActionAsync(chat, ChatAction.Typing); //simulate keystrokes

                try
                {
                    var t1 = client.GetNodeInfo();
                    var t2 = client.GetTxs(hash);
                    props.network = (await t1).network ?? props.network;
                    txs = await t2;
                    if((txs.height).ToLongOrDefault(-1) > 0)
                        break;
                }
                catch(Exception ex)
                {
                    await keystrokeTask;
                    error = $"\nError: `Tx hash was not found.`";
                    _logger.Log($"[DISCOVERY PROCESS ERROR] => Filed ('{msg?.Chat?.Id ?? 0}') to query tx hash: '{ex.JsonSerializeAsPrettyException(Newtonsoft.Json.Formatting.Indented)}'");
                }
            }

            if (error.IsNullOrWhitespace() && !(txs?.error).IsNullOrWhitespace())
                error = $"\nError: `{txs.error}`";

            if (error.IsNullOrWhitespace())
            {
                gas = $"\nGas Used: `{txs.gas_used}`\nGas Wanted: `{txs.gas_wanted}`";
                height = $"\nHeight: `{txs.height}`";
                timestamp = $"\nTimestamp: `{txs.timestamp}`";
                network = $"\nNetwork: `{props.network}`";
                log = $"\nLog: `{txs.raw_log}`";

                var outputJson = (txs?.tx?.value).JsonSerialize(Newtonsoft.Json.Formatting.Indented);
                outputJson = outputJson.TrimOnTrigger('[', '\n', '\r', ' ');
                outputJson = outputJson.TrimOnTrigger(']', '\n', '\r', ' ');
                outputJson = outputJson.TrimOnTrigger('}', '\n', '\r', ' ');

                if (outputJson.Length > 8 && outputJson.Length < 3072)
                    output = $"\n\nOutput: `{outputJson}`";
                else if (outputJson.Length >= 3072)
                    output = $"\n\nOutput:\n`Too long to display 😢`";
            }

            await _TBC.SendTextMessageAsync(chatId: chat,
                $"Hash: `{hash}`\n" + height + gas + network + timestamp + log + error + output,
                            replyToMessageId: msg.MessageId,
                            parseMode: ParseMode.Markdown);
        }

        private async Task TransactionProcessMessage(Message m)
        {
            if (!await this.CheckMasterChatMembership(m))
                return;

            var chat = m.Chat;
            var from = m.From;
            var to = m.ReplyToMessage?.From;
            var text = (m.Text?.Trim() ?? "").Trim('\'', '"', '`', '*', '[', ']');

            var args = text.Split(" ");
            var cliArgs = CLIHelper.GetNamedArguments(args);

            var toUsername = args.TryGetValueOrDefault(1, "").Trim(' ', '\'', '"', '*', '`', '[', ']');
            var toAddress = (cliArgs.GetValueOrDefault("address") ?? args.TryGetValueOrDefault(3, "")).Trim(' ', '\'', '"', '*', '`', '[', ']');

            if (!Bech32Ex.TryDecode(toAddress, out var hrp, out var addrBytes))
                toAddress = null;

            if (!toAddress.IsNullOrWhitespace())
                to = null;

            if (toAddress.IsNullOrWhitespace() && to == null && toUsername.StartsWith("@"))
                to = await TryGetUserByUsername(toUsername.TrimStart("@"));

            if (to == null && toAddress.IsNullOrWhitespace())
            {
                await _TBC.SendTextMessageAsync(chatId: chat,
                    $"Transaction can't be send.\n" +
                    $"User @{toUsername ?? "null"} is not an active member of *{chat.Title}* group, `address` property is invalid or you responded to the old message that bot can't see.\n" +
                    $"Try a 'reply to option', for example: Reply to -> `tip <amount> $<token_name>` rather then `tx @<username> <amount> $<token>` or specify `address` argument, e.g: `tx <amount> $<token> --address=<publicKey>`.",
                    replyToMessageId: m.MessageId,
                    parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
                return;
            }

            var token = (args.FirstOrDefault(x => x.StartsWith("$")).Trim() ?? cliArgs.GetValueOrDefault("token") ?? "").TrimStart("$");
            var propsVar = Environment.GetEnvironmentVariable($"{token?.ToLower()}_PROPS");
            TokenProps props;

            if (!propsVar.IsNullOrEmpty())
                props = propsVar.JsonDeserialize<TokenProps>();
            else
                props = new TokenProps();

            props.denom = cliArgs.GetValueOrDefault("denom") ?? props.denom ?? token.ToLower();
            props.amount = (args.FirstOrDefault(x => x.Trim().IsDigits()).Trim() ?? cliArgs.GetValueOrDefault("amount")).ToLongOrDefault(0);
            props.fees = cliArgs.GetValueOrDefault("fees", props.fees.ToString()).ToLongOrDefault(0);

            if (props.amount < 0 || props.fees < 0)
            {
                await _TBC.SendTextMessageAsync(chatId: chat,
                    $"`amount` or `fees` or `token` were not specified.",
                    replyToMessageId: m.MessageId,
                    parseMode: ParseMode.Markdown);
                return;
            }

            string wallet = null;
            try
            {
                var account = await GetUserAccount(from);
                var acc = new AsmodatStandard.Cryptography.Cosmos.Account(props.prefix, (uint)props.index);
                acc.InitializeWithMnemonic(account.GetSecret());
                var client = new CosmosHub(lcd: props.lcd, timeoutSeconds: _cosmosHubClientTimeout);
                var fromAccountInfo = await client.GetAccount(account: acc.CosmosAddress);
                var fromAccountBalance = fromAccountInfo?.coins?.FirstOrDefault(x => x?.denom?.ToLower() == props.denom.ToLower());
                var fromBalance = (fromAccountBalance?.amount).ToBigIntOrDefault(0);
                wallet = $"Wallet: `{fromBalance} {props.denom}`";
            }
            catch
            {
                
            }

            var optionsKeyboard = new InlineKeyboardMarkup(new[]
                    {
                        new [] // first row
                        {
                            InlineKeyboardButton.WithCallbackData("YES, SEND", $"{OptionKeys.txConfirm.ToString()} {from.Id} {to?.Id.ToString() ?? toAddress}"),
                            InlineKeyboardButton.WithCallbackData("NO!, CANCEL", OptionKeys.txCancel.ToString())
                        },
                    });

            string toConfirm = (to == null ? $"To: `{toAddress}`\n" : $"To: {to.GetMarkDownUsername()} (`{to.Id}`)\n");
            await _TBC.SendTextMessageAsync(chatId: chat,
                $"*[ CONFIRM REQUEST {m.MessageId} ]*\n" +
                $"Action: `transfer`\n" +
                $"From: {from.GetMarkDownUsername()} (`{from.Id}`)\n" +
                toConfirm +
                $"Amount: `{props.amount} {props.denom}`\n" +
                $"Fees: `{props.fees} {props.denom}`\n" + wallet,
                replyToMessageId: m.MessageId,
                replyMarkup: optionsKeyboard,
                parseMode: ParseMode.Markdown);
        }
    }
}
