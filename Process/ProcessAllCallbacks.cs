using System.Threading.Tasks;
using AsmodatStandard.Extensions;
using AsmodatStandard.Extensions.Collections;
using Telegram.Bot.Types;
namespace ICFaucet
{
    public partial class Function
    {
        private async Task<bool> ProcessAllCallbacks(CallbackQuery c)
        {
            var chat = c.Message.Chat;
            var data = c.Data;
            var user = c.From;
            var responseToUser = c?.Message?.ReplyToMessage?.From;

            if (responseToUser?.Id != user.Id)
                return false; //ignore callbacks from users to whom response was not made

            if (data.IsNullOrEmpty())
                return false;

            var args = data.Split(" ");

            switch (args[0])
            {
                case nameof(OptionKeys.txConfirm):
                    {
                        await _TBC.DeleteMessageAsync(chat, c.Message.MessageId);

                        await TransferProcessAcceptCallback(c, 
                            args.TryGetValueOrDefault(1,"0").ToLongOrDefault(), 
                            args.TryGetValueOrDefault(2,"0"));

                        return true;
                    }
                case nameof(OptionKeys.txCancel):
                    {
                        await _TBC.SendTextMessageAsync(chatId: chat, $"Transaction was cancelled by {user.GetMarkDownUsername()} 😢",
                            replyToMessageId: c.Message.ReplyToMessage.MessageId,
                            parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);

                        await _TBC.DeleteMessageAsync(chat, c.Message.MessageId);
                        return true;
                    }
            }

            Log($"[All Callbacks] => Did not found any command option for '{data ?? "undefined"}'.");
            return false;
        }
    }
}
