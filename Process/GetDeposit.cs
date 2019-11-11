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

namespace ICFaucet
{
    public partial class Function
    {
        private async Task<bool> GetDeposit(Message m) // verify that user is a part of the master chat
        {
            var text = m.Text?.Trim();
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

            var deposit = args.TryGetValueOrDefault(3)?.Trim()?.ToLower();

            if (deposit != "deposit")
                return false;

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

            var acc = new AsmodatStandard.Cryptography.Cosmos.Account(props.prefix, (uint)props.index);
            acc.InitializeWithMnemonic(_mnemonic.Release());
            var cosmosAdress = acc.CosmosAddress;

            await _TBC.SendTextMessageAsync(chatId: m.Chat,
                    $"Faucet Public Address: `{cosmosAdress}`",
                    replyToMessageId: m.MessageId,
                    parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);

            return true;
        }
    }
}
