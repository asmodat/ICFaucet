using System.Threading.Tasks;
using AsmodatStandard.Extensions;
using AsmodatStandard.Extensions.Collections;
using Telegram.Bot.Types;
using AsmodatStandard.Extensions.Security;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using ICFaucet.Models;
using AWSWrapper.S3;
using Asmodat.Cryptography.Bitcoin.Mnemonic;
namespace ICFaucet
{
    public partial class Function
    {
        private async Task ProcessCallbacks(CallbackQuery c)
        {
            var chat = c.Message.Chat;
            var user = c.From;

            if (await ProcessAllCallbacks(c))
                return;

            if (chat.Id == user.Id)
            {
                await ProcessPrivateCallbacks(c);
                return;
            }
        }

        public readonly string _accountRecoveryMessageTitle = "KIRA INTERCHAIN WALLET RECOVERY";

        public async Task RecoveryConfirmation(Chat chat)
        {
            var optionsKeyboard = new InlineKeyboardMarkup(new[]
                        {
                            new [] // first row
                            {
                                InlineKeyboardButton.WithCallbackData("YES, Recover My Wallet", OptionKeys.yesRecoverSecret.ToString()),
                                InlineKeyboardButton.WithCallbackData("NO!, TAKE ME BACK.", OptionKeys.start.ToString())
                            }
                        });

            await _TBC.SendTextMessageAsync(chatId: chat,
                $"This action will irreversibly *delete* your current secret (wallet). Make 100% sure, that you backed up your secret words before you proceed to recover your account.", 
                replyMarkup: optionsKeyboard, parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
            return;
        }

        public async Task NewSecretConfirmation(Chat chat)
        { 
            var optionsKeyboard = new InlineKeyboardMarkup(new[]
                        {
                            new [] // first row
                            {
                                InlineKeyboardButton.WithCallbackData("YES, Generate New Wallet", OptionKeys.yesNewSecret.ToString()),
                                InlineKeyboardButton.WithCallbackData("NO!, TAKE ME BACK.", OptionKeys.start.ToString())
                            }
                        });
            await _TBC.SendTextMessageAsync(chatId: chat,
                $"This action will irreversibly *delete* your secret (wallet) and generate new one. Make 100% sure, that you backed up your secret words before you proceed.", 
                replyMarkup: optionsKeyboard, parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
            return;
        }

        public async Task ShowSecret(long userId, Chat chat)
        {
            var acc = await GetUserAccount(userId, createNewAcount: false);
            var mnemonic = acc.GetSecret();
            await _TBC.SendTextMessageAsync(chatId: chat,
                $"Write down following {(mnemonic ?? "").Split(" ").Length} *secret* words. They will allow you to recover your wallet in case of emergency. It's important to maitain order of words when writing them down.\n\n" +
                $"`{mnemonic}`\n\n" +
                $"*WARNING!!!* Never share above secret with anyone.",
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
            return;
        }

        public Task<UserAccount> GetUserAccount(User user, bool createNewAcount = false) => GetUserAccount(userId: user.Id, createNewAcount: createNewAcount);
        public async Task<UserAccount> GetUserAccount(long userId, bool createNewAcount = false)
        {
            if (userId == _bot.Id)
                return new UserAccount(userId, mnemonic: _mnemonic.Release());

            var userKey = $"accounts/{userId}";
            if (createNewAcount || !await _S3.ObjectExistsAsync(_bucket, key: userKey))
            {
                var entropy = RandomEx.NextBytesSecure(16);
                var bip39 = new Bip39(entropyBytes: entropy, passphrase: "", language: Bip39.Language.English);
                var newUA = new UserAccount(userId, bip39.MnemonicSentence);
                await _S3.UploadTextAsync(_bucket, userKey, newUA.JsonSerialize());
                return newUA;
            }

            var oldUA = await _S3.DownloadJsonAsync<UserAccount>(_bucket, key: userKey, throwIfNotFound: true);
            return oldUA.SecureCopy();
        }

        public async Task RecoverUserAccount(Chat chat, long userId, string mnemonic)
        {
            await _TBC.SendChatActionAsync(chat, ChatAction.Typing); //simulate keystrokes
            var account = await GetUserAccount(userId, createNewAcount: false);

            mnemonic = mnemonic?.Trim();
            if (mnemonic.IsNullOrEmpty())
            {
                await _TBC.SendTextMessageAsync(chatId: chat, $"Account recovery *failed*, secret words were not specifed.",
                    parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
                return;
            }

            mnemonic = mnemonic.ToLower();
            var words = mnemonic.Split(" ");

            if (words.Length < 12)
            {
                await _TBC.SendTextMessageAsync(chatId: chat, $"Account recovery *failed*, you submitted less then the minimum required *12* words.",
                    parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
                return;
            }

            if (words.Length % 3 != 0)
            {
                await _TBC.SendTextMessageAsync(chatId: chat, $"Account recovery *failed*, you submitted number of words not divisible by *3*.",
                    parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
                return;
            }

            bool success;
            try
            {
                var bip39 = new Bip39(mnemonic, passphrase: "", language: Bip39.Language.Unknown);
                success = bip39.MnemonicSentence == mnemonic;
                account.SetSecret(mnemonic);
            }
            catch
            {
                success = false;
            }

            if(!success)
            {
                await _TBC.SendTextMessageAsync(chatId: chat, $"Account recovery *failed*, submitted words do not comply with the BIP39 standard. Click [here](https://github.com/bitcoin/bips/blob/master/bip-0039.mediawiki) to learn more.",
                    parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                    disableWebPagePreview: true);
                return;
            }

            var userKey = $"accounts/{userId}";
            await _S3.UploadTextAsync(_bucket, userKey, account.JsonSerialize());
            await _TBC.SendTextMessageAsync(chatId: chat, $"Your account was recovered successfully!",
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
        }

        private async Task<bool> PrivateProcess(Message m)
        {
            var chat = m.Chat;
            var text = m.Text?.Trim() ?? "";
            await _TBC.SendChatActionAsync(chat, ChatAction.Typing); //simulate keystrokes

            if (text.ToLower() == "/start")
            {
                await DisplayMainMenu(chat);
                return true;
            }
            else if ((m?.ReplyToMessage?.Text).Contains(_accountRecoveryMessageTitle))
            {
                await RecoverUserAccount(chat, m.From.Id, text);
                return true;
            }

            return false;
        }

        public async Task DisplayMainMenu(Chat chat)
        {
            var optionsKeyboard = new InlineKeyboardMarkup(new[]
                    {
                        new [] // first row
                        {
                            InlineKeyboardButton.WithCallbackData("Show Secret", OptionKeys.showSecret.ToString()),
                            InlineKeyboardButton.WithCallbackData("Acount Recovery", OptionKeys.recoverSecret.ToString()),
                            InlineKeyboardButton.WithCallbackData("New Account", OptionKeys.newSecret.ToString()),
                        },
                        new [] // second row
                        {
                            InlineKeyboardButton.WithCallbackData("Faucet", OptionKeys.faucetHelp.ToString()),
                            InlineKeyboardButton.WithCallbackData("Transactions", OptionKeys.txHelp.ToString()),
                            InlineKeyboardButton.WithCallbackData("Trading", OptionKeys.tradeHelp.ToString()),
                        }
                    });

            await _TBC.SendTextMessageAsync(chatId: chat, $"WELCOME TO *KIRA WALLET* MAIN MENU", 
                replyMarkup: optionsKeyboard, parseMode: ParseMode.Markdown);
        }
    }
}
