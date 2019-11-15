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
        }

        private async Task TransactionProcessMessage(Message m)
        {
            if (!await this.CheckMasterChatMembership(m))
                return;

            var chat = m.Chat;
            var from = m.From;
            var to = (m.ReplyToMessage?.ForwardFrom == null) ? m.ReplyToMessage.From : null;
            var text = (m.Text?.Trim() ?? "").Trim('\'', '"', '`', '*', '[', ']');

            var args = text.Split(" ");
            var cliArgs = CLIHelper.GetNamedArguments(args);

            var toUsername = args.TryGetValueOrDefault(1, "").Trim(' ', '\'', '"', '*', '`', '[', ']');
            var toAddress = (args.TryGetValueOrDefault(3, "") ?? cliArgs.GetValueOrDefault("address")).Trim(' ', '\'', '"', '*', '`', '[', ']');

            if (!Bech32Ex.TryDecode(toAddress, out var hrp, out var addrBytes))
                toAddress = null;

            if (!toAddress.IsNullOrWhitespace())
                to = null;

            if (toAddress.IsNullOrWhitespace() && to == null && toUsername.StartsWith("@"))
                to = await TryGetUserByUsername(toUsername.TrimStart("@"));

            if (to == null && toAddress.IsNullOrWhitespace())
            {
                await _TBC.SendTextMessageAsync(chatId: chat,
                    $"Transaction can't be send, user @{toUsername ?? "null"} is not an active member of *{chat.Title}* group or `address` property is invalid. Try a 'reply to option', for example: Reply to -> `tip <amount> $<token_name>` rather then `tx @<username> <amount> $<token>` or specify `address` argument, e.g: `tx <amount> $<token> --address=<publicKey>`.",
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

            props.denom = props.denom ?? token.ToLower();
            props.amount = (args.FirstOrDefault(x => x.Trim().IsDigits()).Trim() ?? cliArgs.GetValueOrDefault("amount")).ToLongOrDefault(0);
            props.fees = cliArgs.GetValueOrDefault("fees", props.fees.ToString()).ToLongOrDefault(0);

            if (props.amount < 0 || props.fees < 0 || props.denom.IsNullOrWhitespace())
            {
                await _TBC.SendTextMessageAsync(chatId: chat,
                    $"`amount`, `fees` or `token` was not specified.",
                    replyToMessageId: m.MessageId,
                    parseMode: ParseMode.Markdown);
                return;
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
                $"Fees: `{props.fees} {props.denom}`",
                replyToMessageId: m.MessageId,
                replyMarkup: optionsKeyboard,
                parseMode: ParseMode.Markdown);
        }
    }
}
