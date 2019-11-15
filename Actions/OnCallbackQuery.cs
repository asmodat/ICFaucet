using System;
using System.Diagnostics;
using System.Threading.Tasks;
using AsmodatStandard.Extensions;
using AsmodatStandard.Extensions.Collections;
using Amazon.Lambda.Core;
using AsmodatStandard.Networking;
using Telegram.Bot;
using System.Collections.Generic;
using Telegram.Bot.Types;
using AsmodatStandard.Extensions.Threading;
using AsmodatStandard.Extensions.Security;
using AWSWrapper.SM;
using Newtonsoft.Json.Linq;
using System.Security;
using System.Threading;
using AWSWrapper.S3;
using ICFaucet.Models;
using Asmodat.Cryptography.Bitcoin.Mnemonic;
using System.Linq;

namespace ICFaucet
{
    public partial class Function
    {
        public enum OptionKeys
        {
            start = 0,
            showSecret = 1,
            recoverSecret = 2,
            yesRecoverSecret = 3,
            newSecret = 4,
            yesNewSecret = 5,
            faucetHelp = 6,
            txHelp = 7,
            tradeHelp = 8,
            txConfirm = 9,
            txCancel = 10
        }

        private void _TBC_OnCallbackQuery(object sender, Telegram.Bot.Args.CallbackQueryEventArgs e)
        {
            var chat = e.CallbackQuery?.Message?.Chat;
            var user = e.CallbackQuery?.From;
            var data = e.CallbackQuery?.Data;

            if (chat == null || user == null || user.IsBot != false || data.IsNullOrEmpty())
                return;

            var options = EnumEx.ToStringArray<OptionKeys>();

            var args = data.Split(" ");
            if (!options.Any(x => x == args[0]))
                return; //no options found

            _logger.Log($"[info] => Chat: @{chat.Username ?? "undefined"}:{chat.Id.ToString()} => User: @{user.Username ?? "undefined"}:{user.Id.ToString()} => Callback ({e.CallbackQuery.Id}): '{data}'");

            _ssCbqLocker.Lock(() =>
            {
                _callbacks.Add(e.CallbackQuery);
            });
        }

        
    }
}
