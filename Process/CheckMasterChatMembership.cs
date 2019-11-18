using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace ICFaucet
{
    public partial class Function
    {
        private async Task<bool> CheckMasterChatMembership(Message m) // verify that user is a part of the master chat
        {
            var chatId = m.Chat.Id;

            if (chatId == _masterChatId.Identifier) // do not check if message originates from masterchat
                return true;

            if (await _TBC.IsChatMember(_masterChatId, m.From))
                return true;

            var inviteLink = await GetMasterChatInviteLink();
            await _TBC.SendTextMessageAsync(
                chatId: new ChatId(chatId),
                $"To interact with the bot you must be a member of\n*{_masterChat.Title}* Group\nClick [HERE]({inviteLink}) to join.", 
                replyToMessageId: m.MessageId,
                disableWebPagePreview: true,
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);

            return false;
        }

    }
}
