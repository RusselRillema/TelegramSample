namespace TelegramBot
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();

            SWTelegramBot.Instance.MessageEvent += Instance_MessageEvent;
        }

        private void Instance_MessageEvent(object? sender, MessageEventArgs e)
        {
            UpdateUI(e.Type, e.Message, e.MessageId, e.ChatId);
        }

        public void UpdateUI(string type, string message, string messageId, long? chatId = null)
        {
            if (InvokeRequired)
            {
                Invoke(() =>
                {
                    UpdateUI(type, message, messageId, chatId);
                });
                return;
            }

            txtMessageIds.AppendText($"{type} : {messageId} : {Environment.NewLine}");
            txtMessageIds.AppendText($"{Environment.NewLine}");
            txtMessageIds.AppendText($"{messageId}:{Environment.NewLine}{message}");
            txtMessageIds.AppendText($"{Environment.NewLine}{Environment.NewLine}");

            if (chatId != null)
            {
                lblChatId.Text = $"ChatId: {chatId?.ToString() ?? ""}";
            }
        }
    }
}