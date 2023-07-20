using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Telegram.BotAPI;
using Telegram.BotAPI.AvailableMethods;
using Telegram.BotAPI.AvailableTypes;
using Telegram.BotAPI.GettingUpdates;

namespace Sandwich.Sandbox
{
    public partial class TelegramBotTesting : Form
    {
        private string _botToken = "6353338876:AAGVYnchfDJBu185dLAyI7k8Qxq3kqNy1uY"; //This should be configurable to get 1 per client
        private long _myUserId = 5033078505; //This should be configurable for client to put their userId in
        private long _chatId;
        private bool _enabled = true;
        private BotClient? _botClient;

        public TelegramBotTesting()
        {
            InitializeComponent();

            _botClient = new BotClient(_botToken);

            Task.Run(() =>
            {
                try
                {
                    Configure();
                }
                catch (Exception ex)
                {
                    if (InvokeRequired)
                        MessageBox.Show(ex.ToString());
                    else
                        MessageBox.Show(ex.ToString());
                }
            });
        }
        public async Task Configure()
        {
            SubWay.Instance?.LoggerSync?.Info("Setting up the telegram service");
            var updates = _botClient.GetUpdates();
            while (true)
            {
                if (updates.Any())
                {
                    foreach (var update in updates)
                    {
                        HandleMessage(update);
                    }
                    var offset = updates.Last().UpdateId + 1;
                    updates = _botClient.GetUpdates(offset);
                }
                else
                {
                    updates = _botClient.GetUpdates();
                }

                await Task.Delay(200);
            }
        }

        public void HandleMessage(Update update)
        {
            string text = string.Empty;
            switch (update.Type)
            {
                case UpdateType.Unknown:
                    break;
                case UpdateType.Message:
                    _chatId = update.Message.Chat.Id;
                    text = update.Message.Text;
                    break;
                case UpdateType.EditedMessage:
                    break;
                case UpdateType.ChannelPost:
                    break;
                case UpdateType.EditedChannelPost:
                    break;
                case UpdateType.InlineQuery:
                    break;
                case UpdateType.ChosenInlineResult:
                    break;
                case UpdateType.CallbackQuery:
                    text = update.CallbackQuery.Data;
                    break;
                case UpdateType.ShippingQuery:
                    break;
                case UpdateType.PreCheckoutQuery:
                    break;
                case UpdateType.Poll:
                    break;
                case UpdateType.PollAnswer:
                    break;
                case UpdateType.MyChatMember:
                    break;
                case UpdateType.ChatMember:
                    break;
                case UpdateType.ChatJoinRequest:
                    break;
                default:
                    break;
            }

            Action a = () =>
            {
                if (txtIncoming.TextLength > 0)
                {
                    txtIncoming.AppendText(Environment.NewLine);
                    txtIncoming.AppendText(Environment.NewLine);
                }
                txtIncoming.AppendText($"---{update.Type}---");
                txtIncoming.AppendText(Environment.NewLine);
                txtIncoming.AppendText(text);
            };

            if (InvokeRequired)
                Invoke(a);
            else
                a();
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            // Inline Buttons
            List<List<InlineKeyboardButton>> inlineButtons = new();
            foreach (var row in txtInlineButtons.Lines)
            {
                List<InlineKeyboardButton> rowButtonss = new();
                foreach (var button in row.Split("|"))
                {
                    InlineKeyboardButton urlButton = new InlineKeyboardButton(button);
                    urlButton.CallbackData = button;
                    rowButtonss.Add(urlButton);
                }
                inlineButtons.Add(rowButtonss);
            }
            // Keyboard markup
            InlineKeyboardMarkup inlineButtonsMarkup = new InlineKeyboardMarkup(inlineButtons.ToArray());



            List<List<KeyboardButton>> keyboardButtons = new();
            foreach (var row in txtInlineButtons.Lines)
            {
                List<KeyboardButton> rowButtonss = new();
                foreach (var button in row.Split("|"))
                {
                    KeyboardButton urlButton = new KeyboardButton(button);
                    rowButtonss.Add(urlButton);
                }
                keyboardButtons.Add(rowButtonss);
            }

            // Send message!
            SendMessageArgs args = new(_chatId, txtTextToSend.Text);

            if (inlineButtons.Count > 0)
                args.ReplyMarkup = inlineButtonsMarkup;
            else if (keyboardButtons.Count > 0)
                args.ReplyMarkup = new ReplyKeyboardMarkup() { Keyboard = keyboardButtons };

            _botClient.SendMessage(args);

            if (chkClearInputsOnSend.Checked)
            {
                txtTextToSend.Clear();
                txtInlineButtons.Clear();
            }
        }

        private void btnClearIncoming_Click(object sender, EventArgs e)
        {
            txtIncoming.Clear();
        }
    }
}
