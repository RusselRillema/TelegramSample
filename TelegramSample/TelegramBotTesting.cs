using System.Text.Json.Nodes;
using System.Windows.Forms;
using Telegram.BotAPI;
using Telegram.BotAPI.AvailableMethods;
using Telegram.BotAPI.AvailableMethods.FormattingOptions;
using Telegram.BotAPI.AvailableTypes;
using Telegram.BotAPI.GettingUpdates;
using Telegram.BotAPI.UpdatingMessages;
using TelegramSample;

namespace Sandwich.Sandbox
{
    public partial class TelegramBotTesting : Form
    {

        private long? _chatId;
        private bool _enabled = true;
        private BotClient? _botClient;
        TelegramBotSecrets telegramBotSecrets;

        public TelegramBotTesting()
        {
            InitializeComponent();
            string fileLocation = "BotSecrets.json";
            if (!System.IO.File.Exists(fileLocation))
            {
                Instructions inst = new();
                inst.ShowDialog();
                throw new Exception("Please use the BotSecrets_example.json file as an example to create a BotSecrets.json file with your secret information and mark it as copy always to ensure it's in the Bin folder.");
            }
            string content = System.IO.File.ReadAllText(fileLocation);
            telegramBotSecrets = Newtonsoft.Json.JsonConvert.DeserializeObject<TelegramBotSecrets>(content);

            _botClient = new BotClient(telegramBotSecrets.BotToken);

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
            var updates = _botClient.GetUpdates();
            while (true)
            {
                try
                {
                    if (updates.Any())
                    {
                        foreach (var update in updates)
                        {
                            try
                            {
                                HandleMessage(update);
                            }
                            catch (UnauthorizedAccessException ex)
                            {
                                if (update?.Message?.Chat?.Id != null)
                                    _botClient.SendMessage(update.Message.Chat.Id, "Unauthrozed");
                            }
                        }
                        var offset = updates.Last().UpdateId + 1;
                        updates = _botClient.GetUpdates(offset);
                    }
                    else
                    {
                        updates = _botClient.GetUpdates();
                    }
                }
                catch (Exception)
                { }

                await Task.Delay(200);
            }
        }

        public void HandleMessage(Update update)
        {
            string text = string.Empty;
            string id = string.Empty;
            switch (update.Type)
            {
                case UpdateType.Unknown:
                    break;
                case UpdateType.Message:
                    if (update.Message.From.Id != telegramBotSecrets.UserId)
                        throw new UnauthorizedAccessException();

                    _chatId = update.Message.Chat.Id;
                    text = update.Message.Text;
                    id = update.Message.MessageId.ToString();
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
                    id = update.CallbackQuery.Id;
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


                txtMessageIds.AppendText($"{id}:{Environment.NewLine}{text}");
                txtMessageIds.AppendText($"{Environment.NewLine}{Environment.NewLine}");
            };

            if (InvokeRequired)
                Invoke(a);
            else
                a();
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            try
            {
                if (_chatId == null)
                    throw new Exception("Cannot send messages before we know what the chatID is. Send a message from telegram first.");
                if (string.IsNullOrWhiteSpace(txtTextToSend.Text))
                    throw new Exception("Cannot send message with blank text");

                if (txtInlineButtons.TextLength > 0 && txtKeyboardButtons.TextLength > 0)
                {
                    MessageBox.Show($"You cannot send inline buttons and keyboard buttons in the same message{Environment.NewLine}The inline buttons will be used for this message.");
                }

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
                InlineKeyboardMarkup inlineButtonsMarkup = new InlineKeyboardMarkup(inlineButtons.ToArray());

                // Keyboard markup
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
                SendMessageArgs args = new(_chatId.Value, txtTextToSend.Text);

                if (inlineButtons.Count > 0)
                    args.ReplyMarkup = inlineButtonsMarkup;
                else if (keyboardButtons.Count > 0)
                    args.ReplyMarkup = new ReplyKeyboardMarkup() { Keyboard = keyboardButtons };

                args.ParseMode = ParseMode.MarkdownV2;
                //args.ParseMode = ParseMode.HTML;

                var message = _botClient.SendMessage(args);
                txtMessageIds.AppendText($"{message.MessageId}:{Environment.NewLine}{txtTextToSend.Text}");
                txtMessageIds.AppendText($"{Environment.NewLine}{Environment.NewLine}");

                if (chkClearInputsOnSend.Checked)
                {
                    txtTextToSend.Clear();
                    txtInlineButtons.Clear();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void btnClearIncoming_Click(object sender, EventArgs e)
        {
            txtIncoming.Clear();
        }

        private void btnClearChat_Click(object sender, EventArgs e)
        {
            try
            {
                if (_chatId == null)
                    throw new Exception("Cannot send messages before we know what the chatID is. Send a message from telegram first.");
                var chat = _botClient.GetChat(_chatId.Value);
                _botClient.SendMessage(_chatId.Value, @"/clearchat");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void bnDelete_Click(object sender, EventArgs e)
        {
            try
            {
                if (_chatId == null)
                    throw new Exception("Cannot send messages before we know what the chatID is. Send a message from telegram first.");
                if (int.TryParse(txtMessagId.Text, out int messageId))
                {
                    _botClient.DeleteMessage(_chatId.Value, messageId);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void btnEditButtons_Click(object sender, EventArgs e)
        {
            try
            {
                if (_chatId == null)
                    throw new Exception("Cannot send messages before we know what the chatID is. Send a message from telegram first.");
                if (int.TryParse(txtMessagId.Text, out int messageId))
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
                    InlineKeyboardMarkup inlineButtonsMarkup = new InlineKeyboardMarkup(inlineButtons.ToArray());

                    var emrm = new EditMessageReplyMarkup();
                    emrm.ReplyMarkup = inlineButtonsMarkup;
                    emrm.ChatId = _chatId.Value;
                    emrm.MessageId = messageId;
                    _botClient.EditMessageReplyMarkup(emrm);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
    }
}
