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
using ICFaucet.Models;
namespace ICFaucet
{
    public partial class Function
    {
        private async Task TransferProcessAcceptCallback(CallbackQuery c, long from, string to)
        {
            var text = c?.Message?.ReplyToMessage?.Text;
            var fromUser = c?.Message?.ReplyToMessage?.From;
            var toUser = c?.Message?.ReplyToMessage?.ReplyToMessage?.From;
            var chat = c.Message.Chat;
            var replyId = c?.Message?.ReplyToMessage?.MessageId ?? 0;

            if (c?.Message?.ReplyToMessage?.EditDate != null)
            {
                await _TBC.SendTextMessageAsync(chatId: chat, $"Transaction will *NOT* be processed, message was edited by the author.",
                                replyToMessageId: replyId,
                                parseMode: ParseMode.Markdown);
                return;
            }

            var args = text.Split(" ");
            var cliArgs = CLIHelper.GetNamedArguments(args);

            var props = await GetTokenTransferProps(chat, replyId, text, from, to);

            if (props == null) //failed to read properties
                return;

            var fromUA = await GetUserAccount(from, createNewAcount: false);
            var acc = new AsmodatStandard.Cryptography.Cosmos.Account(props.prefix, (uint)props.index);
            acc.InitializeWithMnemonic(fromUA.GetSecret());

            Account fromAccountInfo;
            Token fromAccountBalance;
            var client = new CosmosHub(lcd: props.lcd, timeoutSeconds: _cosmosHubClientTimeout);

            try
            {
                fromAccountInfo = await client.GetAccount(account: props.origin);
                fromAccountBalance = fromAccountInfo?.coins?.FirstOrDefault(x => x?.denom?.ToLower() == props.denom);
                props.denom = fromAccountBalance?.denom ?? props.denom;
                var fromBalance = (fromAccountBalance?.amount).ToLongOrDefault(0);

                if (fromBalance < (props.amount + props.fees) || fromAccountInfo?.coins == null)
                {
                    await _TBC.SendTextMessageAsync(text: $"Transaction including fees requires `{props.amount + props.fees} {props.denom}` but your account balance is `{fromBalance} {props.denom}`",
                        chatId: chat, replyToMessageId: replyId, parseMode: ParseMode.Markdown);
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"[ERROR] => Filed to fetch '{props.denom ?? "undefined"}' balance of '{props.origin ?? "undefined"}' from '{props.lcd ?? "undefined"}': '{ex.JsonSerializeAsPrettyException(Newtonsoft.Json.Formatting.Indented)}'");
                await _TBC.SendTextMessageAsync(text: $"Your account balance is `0 {props.denom}` or lcd '{props.lcd ?? "undefined"}' property is invalid.",
                        chatId: chat, replyToMessageId: replyId, parseMode: ParseMode.Markdown);
                return;
            }

            var doc = await client.CreateMsgSend(
                                account: fromAccountInfo,
                                to: props.address,
                                amount: props.amount,
                                denom: fromAccountBalance.denom,
                                fees: props.fees,
                                gas: props.gas,
                                memo: props.memo ?? "Kira Interchain Wallet - Join us at https://t.me/kirainterex");

            var tx = doc.CreateTx(acc);

            var txResponse = await client.PostTx(tx);

            var inviteLink = await GetMasterChatInviteLink();

            string debugLog = null;
            if (text.Contains("--debug"))
                debugLog = $"\nDebug Log: {txResponse.raw_log}";

            if (txResponse.height.ToLongOrDefault(0) <= 0)
            {
                await _TBC.SendTextMessageAsync(chatId: chat,
                    $"Failed Action: `tx send`\n" +
                    $"Amount: {props.amount} {props.denom}\n" +
                    $"From: {fromUser.GetMarkDownUsername()}\n" +
                    $"To: {toUser?.GetMarkDownUsername() ?? $"`{props.address}`"}\n" +
                    $"Debug Log: `{txResponse.raw_log}`\n" +
                    $"Network Id: `{props.network ?? "undefined"}`\n" +
                    $"Sequence: `{fromAccountInfo.sequence}`\n" +
                    $"Tx Hash: `{txResponse.txhash}`",
                    replyToMessageId: replyId,
                    parseMode: ParseMode.Markdown);

                sequences[props.network] = sequences.GetValueOrDefault(props.network, -1) - 1;
            }
            else
            {
                await _TBC.SendTextMessageAsync(chatId: chat,
                        $"{fromUser.GetMarkDownUsername()} sent `{props.amount} {props.denom}` to {toUser?.GetMarkDownUsername() ?? $"`{props.address}`"}\n" +
                        $"{debugLog}\n" +
                        $"Network Id: `{props.network ?? "undefined"}`\n" +
                        $"Tx Hash: `{txResponse.txhash}`",
                        replyToMessageId: replyId,
                        parseMode: ParseMode.Markdown);
            }
        }
    }
}
