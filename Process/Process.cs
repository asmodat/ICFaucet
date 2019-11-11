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

namespace ICFaucet
{
    public partial class Function
    {
        public static SemaphoreSlim _ssLocker = new SemaphoreSlim(1, 1);
        private ConcurrentDictionary<string, long> sequences = new ConcurrentDictionary<string, long>();

        private async Task ProcessMessage(Message m)
        {
            var chat = m.Chat;
            var userId = m.From.Id;
            var text = m.Text?.Trim();

            if (m.From.IsBot == true || !(text?.ToLower()).StartsWith("give me $"))
                return;

            if (!await this.CheckMasterChatMembership(m))
                return;

            var props = await GetTokenProps(m);

            if (props == null) //failed to read properties
                return;

            var acc = new AsmodatStandard.Cryptography.Cosmos.Account(props.prefix, (uint)props.index);
            acc.InitializeWithMnemonic(_mnemonic.Release());
            var cosmosAdress = acc.CosmosAddress;

            TxResponse txResponse = null;
            Account accountInfo = null;
            Token accountBalance = null;
            var notEnoughFunds = false;
            var client = new CosmosHub(lcd: props.lcd);

            if (props.address != cosmosAdress)
            {
                try
                {
                    var userAccountInfo = await client.GetAccount(account: props.address);
                    var userAccountBalance = (userAccountInfo?.coins).FirstOrDefault(x => x.denom.ToLower() == props.denom);
                    var userBalance = (userAccountBalance?.amount).ToLongOrDefault(0);

                    if (userBalance >= props.amount)
                    {
                        await _TBC.SendTextMessageAsync(text: $"Your account balance exceeds `{props.amount} {props.denom}`", chatId: new ChatId(m.Chat.Id), replyToMessageId: m.MessageId, parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
                        return;
                    }
                }
                catch (Exception ex)
                {

                }
            }

            await _ssLocker.Lock(async () =>
            {
                accountInfo = await client.GetAccount(account: cosmosAdress);
                accountBalance = (accountInfo?.coins).FirstOrDefault(x => x.denom.ToLower() == props.denom);
                var faucetBalance = (accountBalance?.amount).ToLongOrDefault(0);
                props.denom = accountBalance?.denom ?? props.denom;
                if (faucetBalance <= 0)
                {
                    notEnoughFunds = true;
                    return;
                }

                var sequence = accountInfo.sequence.ToLongOrDefault();
                var oldSeque = sequences.GetValueOrDefault(props.network, -1);
                sequences[props.network] = Math.Max(sequence, oldSeque + 1);
                accountInfo.sequence = sequences[props.network].ToString();

                props.amount = Math.Max(Math.Min(props.amount, faucetBalance / 2), 1);
            });

            if (notEnoughFunds)
            {
                await _TBC.SendTextMessageAsync(chatId: chat,
                    $"Faucet does not have enough `{props.denom ?? "undefined"}` tokens or coin index ({props.index}) is invalid.\n\n" +
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
                                denom: accountBalance.denom,
                                fees: props.fees,
                                gas: props.gas,
                                memo: "Kira Universal Interchain Faucet - Join us at https://t.me/kirainterex");

            var tx = doc.CreateTx(acc);

            txResponse = await client.PostTx(tx);

            var inviteLink = await GetMasterChatInviteLink();

            string debugLog = null;
            if (text.Contains("--debug"))
                debugLog = $"\nDebug Log: {txResponse.raw_log}";

            if (txResponse.height.ToLongOrDefault(0) <= 0)
            {
                await _TBC.SendTextMessageAsync(chatId: chat,
                    $"Failed sending `{props.amount} {props.denom}` to @{m.From.Username}\n" +
                    $"Debug Log: `{txResponse.raw_log}`\n" +
                    $"Network Id: `{props.network ?? "undefined"}`\n" +
                    $"Sequence: `{accountInfo.sequence}`\n" +
                    $"Tx Hash: `{txResponse.txhash}`",
                    replyToMessageId: m.MessageId,
                    parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);

                sequences[props.network] = sequences.GetValueOrDefault(props.network, -1) - 1;
            }
            else
            {
                await _TBC.SendTextMessageAsync(chatId: chat,
                        $"{inviteLink} sent you `{props.amount} {props.denom}`\n" +
                        $"{debugLog}\n" +
                        $"Network Id: `{props.network ?? "undefined"}`\n" +
                        $"Tx Hash: `{txResponse.txhash}`",
                        replyToMessageId: m.MessageId,
                        parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
            }
        }
    }
}
