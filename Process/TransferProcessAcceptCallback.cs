using System;
using System.Threading.Tasks;
using AsmodatStandard.Extensions;
using AsmodatStandard.Extensions.Collections;
using System.Collections.Generic;
using System.Linq;
using Telegram.Bot.Types;
using ICWrapper.Cosmos.CosmosHub;
using ICWrapper.Cosmos.CosmosHub.Models;
using AsmodatStandard.IO;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using AsmodatStandard.Extensions.Threading;
using System.Threading;
using System.Numerics;

namespace ICFaucet
{
    public partial class Function
    {
        public static SemaphoreSlim _txLocker = new SemaphoreSlim(1, 1);
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

            Account fromAccountInfo = null;
            Token fromAccountBalance = null;
            var notEnoughFunds = false;
            var client = new CosmosHub(lcd: props.lcd, timeoutSeconds: _cosmosHubClientTimeout);
            var sequenceKey = $"{props.origin}-{props.network}";
            BigInteger fromBalance = 0;
            try
            {
                await _txLocker.Lock(async () =>
                {
                    fromAccountInfo = await client.GetAccount(account: props.origin);
                    fromAccountBalance = fromAccountInfo?.coins?.FirstOrDefault(x => x?.denom?.ToLower() == props.denom.ToLower());
                    fromBalance = (fromAccountBalance?.amount).ToBigIntOrDefault(0);

                    if (fromBalance < (props.amount + props.fees))
                    {
                        notEnoughFunds = true;
                        return;
                    }

                    var sequence = fromAccountInfo.sequence.ToLongOrDefault();
                    var oldSeque = sequences.GetValueOrDefault(sequenceKey, -1);
                    sequences[sequenceKey] = Math.Max(sequence, oldSeque + 1);
                    fromAccountInfo.sequence = sequences[sequenceKey].ToString();
                });
            }
            catch (Exception ex)
            {
                _logger.Log($"[ERROR] => Filed to fetch '{props.denom ?? "undefined"}' balance of '{props.origin ?? "undefined"}' from '{props.lcd ?? "undefined"}': '{ex.JsonSerializeAsPrettyException(Newtonsoft.Json.Formatting.Indented)}'");
                await _TBC.SendTextMessageAsync(text: $"Your account balance is `0 {props.denom}` or lcd '{props.lcd ?? "undefined"}' property is invalid.",
                        chatId: chat, replyToMessageId: replyId, parseMode: ParseMode.Markdown);
                return;
            }

            if(notEnoughFunds)
            {
                await _TBC.SendTextMessageAsync(text: $"Transaction including fees requires `{props.amount + props.fees} {props.denom}` but your account balance is `{fromBalance} {props.denom}`",
                        chatId: chat, replyToMessageId: replyId, parseMode: ParseMode.Markdown);
                return;
            }

            var doc = await client.CreateMsgSend(
                                account: fromAccountInfo,
                                to: props.address,
                                amount: props.amount,
                                denom: fromAccountBalance?.denom ?? props.denom,
                                fees: props.fees,
                                gas: props.gas,
                                memo: props.memo ?? "Kira Interchain Wallet - Join us at https://t.me/kirainterex");

            var tx = doc.CreateTx(acc);
            var txResponse = await client.PostTx(tx);

            var toUserId = to.ToIntOrDefault(0);
            if (toUser == null && toUserId != 0)
                toUser = await _TBC.TryGetChatMember(chat, toUserId);

            var fromUserName = $"{fromUser.GetMarkDownUsername()}";
            var toUserName = $"{toUser?.GetMarkDownUsername() ?? $"`{props.address}`"}";
            var sentAmount = $"{props.amount} {fromAccountBalance?.denom ?? props.denom}";

            var statusMsg = "";
            var debugLog = $"\nDebug Log: {txResponse?.raw_log ?? txResponse.error}";
            var fromMsg = $"\nFrom: {fromUserName}";
            var toMsg = $"\nTo: {toUserName}";
            var amountMsg = $"\nAmount: `{sentAmount}`\n";
            var networkMsg = $"\nNetwork Id: `{props.network ?? "undefined"}`";
            var sequenceMsg = $"\nSequence: `{fromAccountInfo.sequence}`";
            var hashMsg = $"\nTx Hash: `{txResponse?.txhash}`";
            if (txResponse == null || txResponse.height.ToLongOrDefault(0) <= 0 || !txResponse.error.IsNullOrWhitespace())
            {
                statusMsg = $"*Failed* 😢 Action ❌: `tx send`\n" + fromMsg + toMsg + amountMsg;
                debugLog = $"\nDebug Log: {txResponse?.raw_log}";
                sequences[sequenceKey] = sequences.GetValueOrDefault(sequenceKey, -1) - 1;
            }
            else
            {
                statusMsg = $"*SUCCESS* 😄 {fromUserName} sent `{sentAmount}` 💸 to {toUserName} \n";

                if (!text.Contains("--debug"))
                {
                    debugLog = "";
                    sequenceMsg = "";
                }
            }

            await _TBC.SendTextMessageAsync(chatId: chat,
                    statusMsg +
                    debugLog +
                    networkMsg +
                    sequenceMsg +
                    hashMsg,
                    replyToMessageId: replyId,
                    parseMode: ParseMode.Markdown);
        }
    }
}
