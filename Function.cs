using System;
using System.Diagnostics;
using System.Threading.Tasks;
using AsmodatStandard.Extensions;
using AsmodatStandard.Extensions.Collections;
using Amazon.Lambda.Core;
using AsmodatStandard.Networking;
//using AWSWrapper.SM;
using Telegram.Bot;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.Json;

using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Amazon.Lambda.APIGatewayEvents;
using EMG.Lambda.LocalRunner;
using System.Collections.Concurrent;
using Telegram.Bot.Types;
using AsmodatStandard.Extensions.Threading;
using AsmodatStandard.Extensions.Security;
using AWSWrapper.SM;
using Newtonsoft.Json.Linq;
using ICWrapper;
using System.Security;
using System.Threading;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
namespace ICFaucet
{
    public partial class Function
    {
        private SMHelper _SM;
        private ILambdaLogger _logger;
        private ILambdaContext _context;
        private bool _verbose;
        private TelegramBotClient _TBC;
        private ChatId _masterChatId;
        private User _bot;
        private int _maxParallelism;
        private double _maxMessageAge;
        private List<Message> _messages;
        public static SemaphoreSlim _ssMsgLocker = new SemaphoreSlim(1, 1);
        private SecureString _mnemonic;

        public Function()
        {
            _SM = new SMHelper();
            _messages = new List<Message>();
        }

        //*/ DEBUG ONLY
        static async Task Main(string[] args)
        {
            var port = 5001;
            await LambdaRunner.Create()
                                .UsePort(port)
                                .Receives<string>()
                                .UsesAsyncFunctionWithNoResult<Function>((function, args, context) => function.FunctionHandler(context))
                                .Build()
                                .RunAsync();
        }
        //*/
        
        private void Log(string msg)
        {
            if (msg.IsNullOrEmpty() || !_verbose)
                return;

            _logger.Log(msg);
        }

        public async Task FunctionHandler(ILambdaContext context)
        {
            var sw = Stopwatch.StartNew();
            _context = context;
            _logger = _context.Logger;
            _logger.Log($"{context?.FunctionName} => {nameof(FunctionHandler)} => Started");
            _verbose = Environment.GetEnvironmentVariable("verbose").ToBoolOrDefault(true);
            _masterChatId = new ChatId(Environment.GetEnvironmentVariable("MASTER_CHAT_ID").ToLongOrDefault(-1001261081309));
            _maxParallelism = Environment.GetEnvironmentVariable("MAX_PARALLELISM").ToIntOrDefault(0);
            _maxMessageAge = Environment.GetEnvironmentVariable("MAX_MESSAGE_AGE").ToDoubleOrDefault(24*3600);
            var secretName = Environment.GetEnvironmentVariable("SECRET_NAME") ?? "KiraFaucetBot";

            if (Environment.GetEnvironmentVariable("test_connection").ToBoolOrDefault(false))
                Log($"Your Internet Connection is {(SilyWebClientEx.CheckInternetAccess(timeout: 5000) ? "" : "NOT")} available.");

            var secret = JObject.Parse(await _SM.GetSecret(secretName));
            var accessToken = secret["token"]?.ToString();
            _mnemonic = (secret["mnemonic"]?.ToString()).ToSecureString();
            _TBC = new TelegramBotClient(accessToken);
            _bot = await _TBC.GetMeAsync();
            Log($"[INFO] @{_bot.Username ?? "undefined"}, ID: '{_bot.Id}', Master Chat: '{_masterChatId.Identifier}'");

            _TBC.OnMessage += Tbc_OnMessage;
            _TBC.OnMessageEdited += Tbc_OnMessage;
            _TBC.StartReceiving();

            try
            {
                Log($"Processing...");
                while (true)
                {
                    if (_messages.IsNullOrEmpty())
                    {
                        await Task.Delay(100);
                        continue;
                    }

                    Message[] msgArr = null;
                    _ssMsgLocker.Lock(() =>
                    {
                        msgArr = _messages.ToArray().DeepCopy();
                        _messages.Clear();
                    });

                    await ParallelEx.ForEachAsync(msgArr, async msg =>
                    {
                        _logger.Log($"[info] => @{msg.From.Username ?? msg.From.Id.ToString()} => @{msg.Chat.Username ?? msg.Chat.Title ?? msg.Chat.Id.ToString()}, sent a message: '{msg?.Text ?? "undefined"}'");
                        /*/ DEBUG ONLY
                        await ProcessMessage(msg);
                        /*/
                        try
                        {
                            await ProcessMessage(msg);
                        }
                        catch (Exception ex)
                        {
                            _logger.Log($"[ERROR] => Filed ('{msg?.Chat?.Id ?? 0}') to process message '{msg?.Text ?? "undefined"}': '{ex.JsonSerializeAsPrettyException(Newtonsoft.Json.Formatting.Indented)}'");
                            await _TBC.SendTextMessageAsync(chatId: msg.Chat,
                                $"Something went wrong, visit {await GetMasterChatInviteLink()} to find help.", 
                                replyToMessageId: msg.MessageId,
                                parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
                        }
                        //*/

                    }, maxDegreeOfParallelism: _maxParallelism);
                }

            }
            finally
            {
                _logger.Log($"{context?.FunctionName} => {nameof(FunctionHandler)} => Stopped, Eveluated within: {sw.ElapsedMilliseconds} [ms]");
            }
        }

        private void Tbc_OnMessage(object sender, Telegram.Bot.Args.MessageEventArgs e)
        {
            if (e.Message == null) // do not process empy message
                return;

            var chat = e.Message.Chat;
            var chatId = chat.Username ?? chat.Id.ToString();
            _logger.Log($"[info] => Chat:{chat.Username ?? chat.Id.ToString()} => User: {e.Message.From.Username ?? e.Message.From.Id.ToString()} => Message ({e.Message.MessageId}): '{e.Message?.Text ?? "undefined"}'");

            var dt = (e.Message?.EditDate ?? e.Message?.Date ?? DateTime.UtcNow);
            if (!dt.IsUTC())
                dt = dt.ToUniversalTime();

            if ((DateTime.UtcNow - dt).TotalSeconds > _maxMessageAge) // do not process old message
            {
                _logger.Log($"[info] => Message ({e.Message.MessageId}) will not be processed because is older then {_maxMessageAge}s");
                return;
            }

            var text = e.Message.Text; // do not process short messages
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
