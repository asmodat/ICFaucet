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

namespace ICFaucet
{
    public partial class Function
    {
        private async Task FaucetProcessMessage(Message m)
        {
            var chat = m.Chat;
            var userId = m.From.Id;
            var text = (m.Text?.Trim() ?? "").Trim('\'', '"', '`', '*', '[', ']');
            var args = text.Split(" ");
            var cliArgs = CLIHelper.GetNamedArguments(args);

            await _TBC.SendChatActionAsync(chat, ChatAction.Typing); //simulate keystrokes

            var token = args.TryGetValueOrDefault(2)?.TrimStartSingle("$"); // $ATOM
            if (token?.ToLower() == "kex" && Environment.GetEnvironmentVariable($"{token?.ToLower()}_PROPS").IsNullOrWhitespace())
            {
                await _TBC.SendTextMessageAsync(chatId: chat, $"That one's comming 🔜 😉",
                        replyToMessageId: m.MessageId, parseMode: ParseMode.Default);
                return;
            }

            if (!await this.CheckMasterChatMembership(m)) return;
            if (await GetDeposit(m)) return;

            var props = await GetTokenFaucetProps(m);

            if (props == null) //failed to read properties
                return;

            var acc = new AsmodatStandard.Cryptography.Cosmos.Account(props.prefix, (uint)props.index);
            acc.InitializeWithMnemonic(_mnemonic.Release());
            var cosmosAdress = acc.CosmosAddress;

            TxResponse txResponse = null;
            Account accountInfo = null;
            Token accountBalance = null;
            long faucetTokenBalance = 0;
            var notEnoughFunds = false;
            var client = new CosmosHub(lcd: props.lcd, timeoutSeconds: _cosmosHubClientTimeout);

            if (props.address != cosmosAdress)
            {
                try
                {
                    var userAccountInfo = await client.GetAccount(account: props.address);
                    var userAccountBalance = userAccountInfo?.coins?.FirstOrDefault(x => x?.denom?.ToLower() == props.denom);
                    var userBalance = (userAccountBalance?.amount ?? "0").ToBigIntOrDefault(0);

                    if (userBalance >= props.amount)
                    {
                        await _TBC.SendTextMessageAsync(text: $"Your account balance exceeds `{props.amount} {props.denom}`", chatId: new ChatId(m.Chat.Id), replyToMessageId: m.MessageId, parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
                        return;
                    }

                    props.amount = props.amount - userBalance; //only send difference up to max amount
                }
                catch (Exception ex)
                {
                    _logger.Log($"[ERROR] => Filed to fetch '{props.denom ?? "undefined"}' balance of '{props.address ?? "undefined"}' from '{props.lcd ?? "undefined"}': '{ex.JsonSerializeAsPrettyException(Newtonsoft.Json.Formatting.Indented)}'");
                }
            }

            var sequenceKey = $"{cosmosAdress}-{props.network}";
            await _ssLocker.Lock(async () =>
            {
                accountInfo = await client.GetAccount(account: cosmosAdress);
                accountBalance = accountInfo?.coins?.FirstOrDefault(x => x?.denom?.ToLower() == props.denom.ToLower());
                faucetTokenBalance = (accountBalance?.amount).ToLongOrDefault(0);

                if (faucetTokenBalance < (props.amount + props.fees))
                {
                    notEnoughFunds = true;
                    return;
                }

                var sequence = accountInfo.sequence.ToLongOrDefault();
                var oldSeque = sequences.GetValueOrDefault(sequenceKey, -1);
                sequences[sequenceKey] = Math.Max(sequence, oldSeque + 1);
                accountInfo.sequence = sequences[sequenceKey].ToString();
            });

            if (notEnoughFunds)
            {
                await _TBC.SendTextMessageAsync(chatId: chat,
                    $"Faucet does not have enough `{props.denom ?? "undefined"}` tokens ({faucetTokenBalance}) or coin index ({props.index}) is invalid.\n\n" +
                    $"Network Id: `{props.network ?? "undefined"}`\n" +
                    $"Faucet Public Address: `{cosmosAdress}`",
                    replyToMessageId: m.MessageId,
                    parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
                return;
            }

            var doc = await client.CreateMsgSend(
                                account: accountInfo,
                                to: props.address,
                                amount: props.amount,
                                denom: accountBalance?.denom ?? props.denom,
                                fees: props.fees,
                                gas: props.gas,
                                memo: props.memo ?? "Kira Interchain Faucet - Join us at https://t.me/kirainterex");

            var tx = doc.CreateTx(acc);

            txResponse = await client.PostTx(tx);
            var inviteLink = await GetMasterChatInviteLink();

            string debugLog = null;
            if (text.Contains("--debug"))
                debugLog = $"\nDebug Log: {txResponse?.raw_log}";

            if (txResponse == null || txResponse.height.ToLongOrDefault(0) <= 0 || !txResponse.error.IsNullOrWhitespace())
            {
                await _TBC.SendTextMessageAsync(chatId: chat,
                    $"*Failed* 😢 sending `{props.amount} {props.denom}` to {m.From.GetMarkDownUsername()} ❌\n" +
                    $"Debug Log: `{txResponse?.raw_log ?? txResponse?.error}`\n" +
                    $"Network Id: `{props?.network ?? "undefined"}`\n" +
                    $"Sequence: `{accountInfo?.sequence}`\n" +
                    $"Tx Hash: `{txResponse?.txhash}`",
                    replyToMessageId: m.MessageId,
                    parseMode: ParseMode.Markdown);

                sequences[sequenceKey] = sequences.GetValueOrDefault(sequenceKey, -1) - 1;
            }
            else
            {
                await _TBC.SendTextMessageAsync(chatId: chat,
                        $"*SUCCESS* 😄 {inviteLink} sent you `{props.amount} {props.denom}` 💸\n" +
                        $"{debugLog}\n" +
                        $"Network Id: `{props.network ?? "undefined"}`\n" +
                        $"Tx Hash: `{txResponse.txhash}`",
                        replyToMessageId: m.MessageId,
                        parseMode: ParseMode.Markdown);
            }
        }
    }
}
