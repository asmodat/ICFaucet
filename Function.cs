/*/ DEBUG ONLY
#define TEST
using EMG.Lambda.LocalRunner;
/*/
#define PUBLISH
//*/

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
using AsmodatStandard.Cryptography;
using AWSWrapper.SM;
using Newtonsoft.Json.Linq;
using System.Security;
using System.Threading;
using AWSWrapper.S3;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
namespace ICFaucet
{
    public partial class Function
    {
        private SMHelper _SM;
        private S3Helper _S3;
        private ILambdaLogger _logger;
        private ILambdaContext _context;
        private bool _verbose;
        private TelegramBotClient _TBC;
        private ChatId _masterChatId;
        private User _bot;
        private int _maxParallelism;
        private double _maxMessageAge;
        private List<Message> _messages;
        private List<CallbackQuery> _callbacks;
        public static SemaphoreSlim _ssMsgLocker = new SemaphoreSlim(1, 1);
        public static SemaphoreSlim _ssCbqLocker = new SemaphoreSlim(1, 1);
        private SecureString _mnemonic;
        private readonly string _version = "v0.1.6";
        private string _bucket;
        private int _cosmosHubClientTimeout;
        private int _lambdaTime;
        private Stopwatch _sw;

        public Function()
        {
            _SM = new SMHelper();
            _S3 = new S3Helper();
            _messages = new List<Message>();
            _callbacks = new List<CallbackQuery>();
            
        }

#if (TEST)
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
#endif

        private void Log(string msg)
        {
            if (msg.IsNullOrEmpty() || !_verbose)
                return;

            _logger.Log(msg);
        }

        public async Task FunctionHandler(ILambdaContext context)
        {
            _sw = Stopwatch.StartNew();
            _context = context;
            _logger = _context.Logger;
            _logger.Log($"{context?.FunctionName} => {nameof(FunctionHandler)} => Started");
            _verbose = Environment.GetEnvironmentVariable("verbose").ToBoolOrDefault(true);
            _masterChatId = new ChatId(Environment.GetEnvironmentVariable("MASTER_CHAT_ID").ToLongOrDefault(-1001261081309));
            _maxParallelism = Environment.GetEnvironmentVariable("MAX_PARALLELISM").ToIntOrDefault(0);
            _cosmosHubClientTimeout = Environment.GetEnvironmentVariable("HUB_CLIENT_TIMEOUT").ToIntOrDefault(7);
            _maxMessageAge = Environment.GetEnvironmentVariable("MAX_MESSAGE_AGE").ToDoubleOrDefault(24 * 3600);
            _bucket = Environment.GetEnvironmentVariable("BUCKET_NAME");
            _lambdaTime = Environment.GetEnvironmentVariable("LAMBDA_TIME").ToIntOrDefault((15 * 60 * 1000) - 5000);

            var secretName = Environment.GetEnvironmentVariable("SECRET_NAME") ?? "KiraFaucetBot";

            if (Environment.GetEnvironmentVariable("test_connection").ToBoolOrDefault(false))
                Log($"Your Internet Connection is {(SilyWebClientEx.CheckInternetAccess(timeout: 5000) ? "" : "NOT")} available.");

            var secret = JObject.Parse(await _SM.GetSecret(secretName));

#if (TEST)
            var accessToken = secret["test_token"]?.ToString();
            //_mnemonic = (secret["test_mnemonic"]?.ToString()).ToSecureString();
            _mnemonic = (secret["mnemonic"]?.ToString()).ToSecureString();
            _bucket = "kira-faucet-test";
#elif (PUBLISH)
            var accessToken = secret["token"]?.ToString();
            _mnemonic = (secret["mnemonic"]?.ToString()).ToSecureString();
#endif
            _TBC = new TelegramBotClient(accessToken);
            _bot = await _TBC.GetMeAsync();
            Log($"[INFO] {_bot.FirstName} {_version} started! Bot Name: @{_bot.Username ?? "undefined"}, Bot Id: '{_bot.Id}', Master Chat: '{_masterChatId.Identifier}'");

            _TBC.OnMessage += Tbc_OnMessage;
            _TBC.OnCallbackQuery += _TBC_OnCallbackQuery;
            _TBC.OnInlineQuery += _TBC_OnInlineQuery;
            _TBC.OnInlineResultChosen += _TBC_OnInlineResultChosen;
            _TBC.StartReceiving();

            try
            {
                Log($"Processing...");
                var finalize = false;
                while (true)
                {
#if (PUBLISH)
                    if (!finalize && _sw.ElapsedMilliseconds >= _lambdaTime)
                    {
                        _TBC.StopReceiving();
                        finalize = true;
                        _logger.Log($"Finalizing, elapsed {_sw.ElapsedMilliseconds} / {_lambdaTime} [ms] ...");
                    }
#endif

                    if (_messages.IsNullOrEmpty() && _callbacks.IsNullOrEmpty())
                    {
                        if (finalize)
                        {
                            _logger.Log($"Lambda was finalized gracefully within {_lambdaTime - _sw.ElapsedMilliseconds} ms.");
                            return;
                        }
                        else
                        {
                            await Task.Delay(100);
                            continue;
                        }
                    }

                    Message[] msgArr = null;
                    _ssMsgLocker.Lock(() =>
                    {
                        msgArr = _messages.ToArray().DeepCopy();
                        _messages.Clear();
                    });

                    var t0 = ParallelEx.ForEachAsync(msgArr, async msg => {
                        
                        async Task ProcessUser(Message m)
                        {
                            var user = m.From;
                            var replyUser = m.ReplyToMessage?.From;

                            if (user != null)
                                await UpdateUserData(user);

                            if (replyUser != null && user?.Id != replyUser.Id)
                                await UpdateUserData(replyUser);
                        }

#if (TEST)
                        await ProcessUser(msg);
#elif (PUBLISH)
                        try
                        {
                            await ProcessUser(msg);
                        }
                        catch (Exception ex)
                        {
                            _logger.Log($"[USR ERROR] => Filed ('{msg?.Chat?.Id ?? 0}') to save user status: '{ex.JsonSerializeAsPrettyException(Newtonsoft.Json.Formatting.Indented)}'");
                        }
#endif
                    });

                    var t1 = ParallelEx.ForEachAsync(msgArr, async msg =>
                    {
#if (TEST)
                        await ProcessMessage(msg);
#elif (PUBLISH)
                        try
                        {
                            await ProcessMessage(msg);
                        }
                        catch (Exception ex)
                        {
                            _logger.Log($"[MSG ERROR] => Filed ('{msg?.Chat?.Id ?? 0}') to process message ({msg?.MessageId}): '{ex.JsonSerializeAsPrettyException(Newtonsoft.Json.Formatting.Indented)}'");
                            await _TBC.SendTextMessageAsync(chatId: msg.Chat,
                                $"Something went wrong, visit {await GetMasterChatInviteLink()} to find help.", 
                                replyToMessageId: msg.MessageId,
                                parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
                        }
#endif
                    }, maxDegreeOfParallelism: _maxParallelism);

                    CallbackQuery[] cbqArr = null;
                    _ssCbqLocker.Lock(() =>
                    {
                        cbqArr = _callbacks.ToArray().DeepCopy();
                        _callbacks.Clear();
                    });
                    var t2 = ParallelEx.ForEachAsync(cbqArr, async cbq =>
                    {
#if (TEST)
                        await ProcessCallbacks(cbq);
#elif (PUBLISH)
                        try
                        {
                            await ProcessCallbacks(cbq);
                        }
                        catch (Exception ex)
                        {
                            _logger.Log($"[CBQ ERROR] => Filed ('{cbq.Message?.Chat?.Id ?? 0}') to process callback ({cbq.Id}): '{ex.JsonSerializeAsPrettyException(Newtonsoft.Json.Formatting.Indented)}'");
                            await _TBC.SendTextMessageAsync(chatId: cbq.Message.Chat,
                                $"Something went wrong, visit {await GetMasterChatInviteLink()} to find help.",
                                parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
                        }
#endif
                    }, maxDegreeOfParallelism: _maxParallelism);

                    await Task.WhenAll(t0, t1, t2);     
                }
            }
            finally
            {
                _logger.Log($"{context?.FunctionName} => {nameof(FunctionHandler)} => Stopped, Eveluated within: {_sw.ElapsedMilliseconds} [ms]");
            }
        }

        public async Task UpdateUserData(User user)
        {
            if (user == null || user.Username.IsNullOrWhitespace())
                return;

            var userKey = $"users/{user.Username.SHA256().ToHexString()}";
            var newUser = user.JsonSerialize();
            
            if (await _S3.ObjectExistsAsync(_bucket, key: userKey))
            {
               var oldUser = await _S3.DownloadTextAsync(_bucket, userKey);
               if (oldUser?.ReplaceMany((" ",""),("\n", ""), ("\r", "")) == newUser?.Replace((" ",""),("\n", ""),("\r", "")))
                return;
            }
            
            await _S3.UploadTextAsync(_bucket, userKey, newUser);
        }

        public async Task<User> TryGetUserByUsername(string username)
        {
            if (username.IsNullOrWhitespace())
                return null;

            var userKey = $"users/{username.SHA256().ToHexString()}";
            if (await _S3.ObjectExistsAsync(_bucket, key: userKey))
            {
                var user = await _S3.DownloadJsonAsync<User>(_bucket, userKey);
                if (user.Id != 0)
                    return user;
            }

            return null;
        }
    }
}
