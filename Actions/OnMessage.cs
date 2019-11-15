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
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace ICFaucet
{
    public partial class Function
    {
        private void Tbc_OnMessage(object sender, Telegram.Bot.Args.MessageEventArgs e)
        {
            if (e.Message == null) // do not process empy message
                return;

            var chat = e.Message.Chat;
            var chatId = chat.Username ?? chat.Id.ToString();
            var text = e.Message?.Text ?? "";
            _logger.Log($"[info] => Chat: @{chat.Username ?? "undefined"}:{chat.Id.ToString()} => User: @{e.Message.From.Username ?? "undefined"}:{e.Message.From.Id.ToString()} => Message ({e.Message.MessageId}): '{text}'");

            var dt = (e.Message?.EditDate ?? e.Message?.Date ?? DateTime.UtcNow);
            if (!dt.IsUTC())
                dt = dt.ToUniversalTime();

            if ((DateTime.UtcNow - dt).TotalSeconds > _maxMessageAge) // do not process old message
            {
                _logger.Log($"[info] => Message ({e.Message.MessageId}) will not be processed because is older then {_maxMessageAge}s");
                return;
            }

            if (text.IsNullOrWhitespace() || text.Length < 3)
                return;

            if (e.Message.ForwardFrom?.Id != null)
                return; // do not process forwarded messages

            _ssMsgLocker.Lock(() =>
            {
                _messages.Add(e.Message);
            });
        }
    }
}
