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
using AWSWrapper.S3;
using Asmodat.Cryptography.Bitcoin.Mnemonic;
using ICFaucet.Models;
namespace ICFaucet
{
    public partial class Function
    {
        private async Task ProcessPrivateCallbacks(CallbackQuery c)
        {
            var chat = c.Message.Chat;
            var data = c.Data;
            var user = c.From;

            if (data.IsNullOrEmpty())
                return;

            var args = data.Split(" ");

            switch (args[0])
            {
                
                case nameof(OptionKeys.start):
                    {
                        await DisplayMainMenu(chat);
                        return;
                    }
                case nameof(OptionKeys.showSecret):
                    {
                        await ShowSecret(user.Id, chat);
                        return;
                    }
                case nameof(OptionKeys.newSecret):
                    {
                        await NewSecretConfirmation(chat);
                        return;
                    }
                case nameof(OptionKeys.yesNewSecret):
                    {
                        await GetUserAccount(user.Id, createNewAcount: true);
                        await _TBC.SendTextMessageAsync(chatId: chat, $"New wallet was created successfully!", 
                            parseMode: ParseMode.Markdown);
                        return;
                    }
                case nameof(OptionKeys.recoverSecret):
                    {
                        await RecoveryConfirmation(chat);
                        return;
                    }
                case nameof(OptionKeys.yesRecoverSecret):
                    {
                        await _TBC.SendTextMessageAsync(chatId: chat,
                            $"*[ {_accountRecoveryMessageTitle} ]*\nInput minimum of *12* secret words separated by whitespaces to recover your account. Words submitted must comply with *BIP39* standard.", 
                            parseMode: ParseMode.Markdown,replyMarkup: new ForceReplyMarkup());
                        return;
                    }
                case nameof(OptionKeys.faucetHelp):
                    {
                        await _TBC.SendTextMessageAsync(chatId: chat,
@"Kira Faucet allows you to claim tokens from any `cosmos-sdk` based project.

To interact with the faucet you must be a member of @kirainterex

To receive tokens send following command:
`Give me $<token_name> <account_address> --index=<coin_index> --lcd=<lcd_url_address>`

To deposit tokens join send following command:
`Give me $<token_name> deposit --index=<coin_index> --prefix=<wallet_prefix>`

To register new token ping @asmodat, and use faucet with implicit commands:
`Give me $<token_name> <account_address>`
`Give me $<token_name> deposit`", 
parseMode: ParseMode.Default);
                        return;
                    }
                case nameof(OptionKeys.txHelp):
                    {
                        await _TBC.SendTextMessageAsync(chatId: chat,
@"Kira Tip Bot allows you to transfer tokens of any `cosmos-sdk` project.

To interact with the tip bot you must be a member of @kirainterex

To transfer tokens with explicit commands:
`tip @<username> <amount> $<token_name> --prefix=<wallet_prefix> --index=<coin_index> --lcd=<lcd_url_address>`
Reply to -> `tip <amount> $<token_name> --index=<coin_index> --lcd=<lcd_url_address>`
`tx <amount> $<token_name> <wallet_address> --index=<coin_index> --lcd=<lcd_url_address>`

To register new token ping @asmodat, and use faucet with implicit commands:
`tip @<username> <amount> $<token_name>`
`tx <amount> $<token_name> <wallet_address>`", 
parseMode: ParseMode.Default);
                        return;
                    }
                case nameof(OptionKeys.tradeHelp):
                    {
                        await _TBC.SendTextMessageAsync(chatId: chat, $"That one's comming 🔜 😉", parseMode: ParseMode.Default);
                        return;
                    }
                default:
                    {
                        await DisplayMainMenu(chat);
                        Log($"[Private Callbacks] => Did not found any command option for '{data ?? "undefined"}'.");
                        return;
                    }
            }
        }

    }
}
