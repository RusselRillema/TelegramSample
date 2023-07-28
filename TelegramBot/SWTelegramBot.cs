using Microsoft.VisualBasic;
using Sandwich;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime;
using System.Windows.Forms;
using System.Windows.Input;
using Telegram.BotAPI;
using Telegram.BotAPI.AvailableMethods;
using Telegram.BotAPI.AvailableMethods.FormattingOptions;
using Telegram.BotAPI.AvailableTypes;
using Telegram.BotAPI.GettingUpdates;
using Telegram.BotAPI.UpdatingMessages;
using static TelegramBot.BalancesTelegramController;
using static TelegramBot.PositionsTelegramController;
using static TelegramBot.TradeTelegramController;
using File = System.IO.File;

namespace TelegramBot
{
    public class SWTelegramBot
    {
        public static ConcurrentDictionary<long, TelegramSettings> TelegramSettingsPerChat = new ConcurrentDictionary<long, TelegramSettings>();
        private static ConcurrentDictionary<long, BaseTelegramController> _lastUsedControllerPerChat = new ConcurrentDictionary<long, BaseTelegramController>();
        public static bool IsLastUsedControllerForChat(long chatId, BaseTelegramController controller)
        {
            if (!_lastUsedControllerPerChat.TryGetValue(chatId, out var lastConttollerForChat))
                return false;
            return lastConttollerForChat == controller;
        }

        public event EventHandler<MessageEventArgs> MessageEvent;

        private bool _enabled = true;

        private System.Threading.Timer _pingTimer;

        private Dictionary<string, BaseTelegramController> _controllersByType = new();
        private long? _chatId;
        private BotClient _botClient;
        private TelegramBotSecrets telegramBotSecrets;

        public static SWTelegramBot _instance;
        public static SWTelegramBot Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new SWTelegramBot();
                return _instance;
            }
        }

        public SWTelegramBot()
        {
            if (!_enabled)
                return;

            string fileLocation = "BotSecrets.json";
            if (!File.Exists(fileLocation))
            {
                throw new Exception("Please use the BotSecrets_example.json file as an example to create a BotSecrets.json file with your secret information and mark it as copy always to ensure it's in the Bin folder.");
            }
            string content = File.ReadAllText(fileLocation);
            var secrets = Newtonsoft.Json.JsonConvert.DeserializeObject<TelegramBotSecrets>(content);

            if (secrets == null)
                throw new Exception("Could not load saved bot configuration");

            telegramBotSecrets = secrets;

            _botClient = new BotClient(telegramBotSecrets.BotToken);
            InitializeControllers();

            _pingTimer = new System.Threading.Timer(PingTimer_Tick);
            _pingTimer?.Change(5000, Timeout.Infinite);

            Configure();
        }

        private async void PingTimer_Tick(object state)
        {
            try
            {
                await RunPings();
            }
            catch (Exception ex)
            {
                //Log message
            }
            finally
            {
                _pingTimer?.Change(5000, Timeout.Infinite);
            }
        }

        public async Task Configure()
        {

            await Task.Delay(500);
            var updates = await _botClient.GetUpdatesAsync();
            while (true)
            {
                try
                {
                    if (updates.Any())
                    {
                        foreach (var update in updates)
                        {
                            HandleMessage(update);
                        }
                        var offset = updates.Last().UpdateId + 1;
                        updates = await _botClient.GetUpdatesAsync(offset);
                    }
                    else
                    {
                        updates = await _botClient.GetUpdatesAsync();
                    }
                }
                catch (Exception ex)
                {
                    ShowError(ex);
                }

                await Task.Delay(200);
            }

        }

        private void ShowError(Exception ex)
        {
            MessageEvent?.Invoke(this, new("Error", ex.Message, "NA"));
        }

        private void InitializeControllers()
        {
            var controllers = Utils.GetEnumerableOfType<BaseTelegramController>(_botClient);
            foreach (var controller in controllers)
            {
                string controllerName = controller.GetType().Name.ToLower();
                if (_controllersByType.ContainsKey(controllerName))
                    throw new Exception($"Controller initialization failed. Cannot add multiple controllers with the same name: {controllerName}");

                _controllersByType.Add(controllerName, controller);
            }
        }

        public void HandleMessage(Update update)
        {
            Task.Run(async () =>
            {
                try
                {
                    BaseTelegramController controller;
                    string text = string.Empty;
                    string id = string.Empty;
                    switch (update.Type)
                    {
                        case UpdateType.Message:
                            if (update?.Message?.Text == null)
                                break;
                            if (update?.Message?.From?.Id == null || !telegramBotSecrets.UserIds.Contains(update.Message.From.Id))
                                throw new UnauthorizedAccessException();

                            await EnsureChatIsConfigured(update.Message.Chat.Id);

                            if (await RunCommandMessage(update.Message.Chat, update.Message.Text, update.Message.MessageId))
                            {
                                await _botClient.DeleteMessageAsync(update.Message.Chat.Id, update.Message.MessageId);
                                break;
                            }

                            if (!_lastUsedControllerPerChat.TryGetValue(update.Message.Chat.Id, out controller))
                                await _botClient.DeleteMessageAsync(update.Message.Chat.Id, update.Message.MessageId);
                            else
                                if (!await controller.HandleMessage(update.Message))
                                    await _botClient.DeleteMessageAsync(update.Message.Chat.Id, update.Message.MessageId);
                            break;
                        case UpdateType.CallbackQuery:
                            if (update?.CallbackQuery?.Data == null || update?.CallbackQuery?.Message?.Chat == null)
                                break;

                            await EnsureChatIsConfigured(update?.Message?.Chat?.Id);

                            if (_lastUsedControllerPerChat.TryGetValue(update.CallbackQuery.Message.Chat.Id, out var lastUsedController)) //TODO: Need to check if the last controller has changed!
                                await lastUsedController.CleanUp(update.CallbackQuery.Message.Chat.Id);

                            if (await RunCommandMessage(update.CallbackQuery.Message.Chat, update.CallbackQuery.Data, update.CallbackQuery.Message.MessageId))
                                break;

                            CallbackQueryCallbackData data = new(update.CallbackQuery.Data);
                            string fullPath = data.Path;
                            var paths = fullPath.Split('/');
                            var command = paths[0];
                            if (!command.EndsWith("telegramcontroller"))
                                command += "telegramcontroller";
                            if (_controllersByType.TryGetValue(command, out controller))
                            {
                                await controller.HandleCallbackQuery(update.CallbackQuery, fullPath, data);
                                //this must happen after we use the controller as the controller needs to know if it was the last one to be used or not
                                UpdateLastUsedController(update.CallbackQuery.Message.Chat, controller); 
                            }
                            break;
                        case UpdateType.Unknown:
                        case UpdateType.EditedMessage:
                        case UpdateType.ChannelPost:
                        case UpdateType.EditedChannelPost:
                        case UpdateType.InlineQuery:
                        case UpdateType.ChosenInlineResult:
                        case UpdateType.ShippingQuery:
                        case UpdateType.PreCheckoutQuery:
                        case UpdateType.Poll:
                        case UpdateType.PollAnswer:
                        case UpdateType.MyChatMember:
                        case UpdateType.ChatMember:
                        case UpdateType.ChatJoinRequest:
                        default:
                            break;
                    }

                    MessageEvent?.Invoke(this, new(update.Type.ToString(), text, id, _chatId));
                }
                catch (UnauthorizedAccessException ex)
                {
                    if (update?.Message?.Chat?.Id != null)
                        await _botClient.SendMessageAsync(update.Message.Chat.Id, "Unauthrozed");
                }
                catch (Exception ex)
                {
                    //Add logging here
                    ShowError(ex);
                }
            });
        }

        private async Task EnsureChatIsConfigured(long? id)
        {
            if (id == null)
                return;
            if (!TelegramSettingsPerChat.TryGetValue(id.Value, out _))
                TelegramSettingsPerChat.TryAdd(id.Value, new());
            var commands = await _botClient.GetMyCommandsAsync();
            var setCommands = new SetMyCommandsArgs();
            setCommands.Commands = new List<BotCommand>()
            {
                new BotCommand() {Command = "home", Description = "Open the home menu"},
                new BotCommand() {Command = "accounts", Description = "Show loaded accounts"},
                new BotCommand() {Command = "settings", Description = "Configure chat settings"},
                new BotCommand() {Command = "trade", Description = "Start configuring a new order"},
                new BotCommand() {Command = "balances", Description = "View your balances on loaded accounts"},
                new BotCommand() {Command = "positions", Description = "View your positions on loaded accounts"},
                new BotCommand() {Command = "exposures", Description = "View your exposures on loaded accounts"},
                new BotCommand() {Command = "orders", Description = "View your orders on loaded accounts"},
            };
            await _botClient.SetMyCommandsAsync(setCommands);
        }

        private async Task<bool> RunCommandMessage(Chat chat, string command, int messageId)
        {
            command = command.ToLower();
            if (command == "/start")
                command = "/home";
            if (command.StartsWith("/"))
                command = command.Remove(0, 1);

            if (command == "ping")
            {
                await RunPing(chat.Id, messageId);
                return true;
            }

            if (!command.EndsWith("telegramcontroller"))
                command += "telegramcontroller";
            if (_controllersByType.TryGetValue(command, out BaseTelegramController controller))
            {
                try
                {
                    await controller.RunInitCommand(chat);
                    //this must happen after we use the controller as the controller needs to know if it was the last one to be used or not
                    UpdateLastUsedController(chat, controller);
                    return true;
                }
                catch (Exception ex)
                {
                    //Log here
                    ShowError(ex);
                }
            }
            return false;
        }

        private void UpdateLastUsedController(Chat chat, BaseTelegramController controller)
        {
            if (_lastUsedControllerPerChat.TryGetValue(chat.Id, out var oldController))
                _lastUsedControllerPerChat.TryUpdate(chat.Id, controller, oldController);
            else
                _lastUsedControllerPerChat.TryAdd(chat.Id, controller);
        }

        public async Task SendMessage(SendMessageArgs args)
        {
            var message = await _botClient.SendMessageAsync(args);
            MessageEvent?.Invoke(this, new("Sending message", args.Text, message.MessageId.ToString(), _chatId));
        }

        private async Task RunPings()
        {
            List<Task> pings = new List<Task>();
            foreach (var item in TelegramSettingsPerChat.ToList())
            {
                if (item.Value.Ping_AutoPing)
                    pings.Add(RunPing(item.Key, item.Value, null));
            }
            //TODO: Add aggregate exception here
            await Task.WhenAll(pings);
        }
        private async Task RunPing(long chatId, int? pingRequestMessageId)
        {
            if (TelegramSettingsPerChat.TryGetValue(chatId, out var settings))
                await RunPing(chatId, settings, pingRequestMessageId);
        }
        private async Task RunPing(long chatId, TelegramSettings chatSettings, int? pingRequestMessageId)
        {
            var message = CreatePingMessage(chatSettings.Ping_MetricToShow);
            bool sendNew = false;
            if (chatSettings.PingMessageId.HasValue)
            {
                EditMessageTextArgs args = new(message)
                {
                    ChatId = chatId,
                    MessageId = chatSettings.PingMessageId.Value
                };
                var pingMessage = await _botClient.EditMessageTextAsync(args);
                //pingMessage = await _botClient.EditMessageReplyMarkupAsync(new() { ChatId = chatId, MessageId = pingMessageId.PingMessageId.Value });
                chatSettings.PingMessageId = pingMessage.MessageId;

                var chat = await _botClient.GetChatAsync(chatId);
                if (chat.PinnedMessage?.MessageId != pingMessage.MessageId || pingRequestMessageId.HasValue)
                {
                    try
                    {
                        await _botClient.UnPinChatMessageAsync(chatId, pingMessage.MessageId);
                        await _botClient.DeleteMessageAsync(chatId, pingMessage.MessageId);
                    }
                    catch (Exception ex)
                    {
                        //Log
                    }
                    sendNew = true;
                }
            }
            
            if (!chatSettings.PingMessageId.HasValue || sendNew)
            {
                
                var pingMessage = await _botClient.SendMessageAsync(chatId, message);
                chatSettings.PingMessageId = pingMessage.MessageId;
                _botClient.UnpinAllChatMessages(chatId);
                await _botClient.PinChatMessageAsync(chatId, pingMessage.MessageId);
            }

            if (pingRequestMessageId.HasValue) 
            {
                await _botClient.DeleteMessageAsync(chatId, pingRequestMessageId.Value);
            }
        }

        private string CreatePingMessage(PingMetric ping_MetricToShow)
        {
            string metric = "";
            switch (ping_MetricToShow)
            {
                case PingMetric.None:
                    break;
                case PingMetric.UPnL:
                    metric = "UPnL: $12k";
                    break;
                case PingMetric.NAV:
                    metric = "NAV: $14M";
                    break;
                case PingMetric.ClosestToLiq:
                    metric = "Liq%: 9.7";
                    break;
                default:
                    break;
            }
            return $"Last: {DateTime.UtcNow.TimeOfDay.ToString(@"hh\:mm\:ss")} {metric}";
        }
    }


    #region Controllers
    public abstract class BaseTelegramController : IComparable<BaseTelegramController>
    {
        protected BotClient Client;
        protected static InlineKeyboardButton HomeButton = new InlineKeyboardButton("🏡 Home") { CallbackData = "/home" };
        protected static List<InlineKeyboardButton> HomeButtonRow = new() { HomeButton };
        protected InlineKeyboardButton TradeButton = new InlineKeyboardButton("💸 Trade") { CallbackData = "/trade" };
        protected InlineKeyboardButton SettingsButton = new InlineKeyboardButton("⚙ Settings") { CallbackData = "/settings" };
        public BaseTelegramController(BotClient client)
        {
            Client = client;
        }

        public int CompareTo(BaseTelegramController? other)
        {
            if (other == null)
                return -1;

            return this.GetType().Name.CompareTo(other.GetType().Name);
        }

        public abstract Task HandleCallbackQuery(CallbackQuery update, string path, CallbackQueryCallbackData data);
        public abstract Task<bool> HandleMessage(Telegram.BotAPI.AvailableTypes.Message update);

        public abstract Task RunInitCommand(Chat chat);

        protected async Task<Telegram.BotAPI.AvailableTypes.Message> BaseSendOrUpdate(ConcurrentDictionary<long, int> chatIdToMessageId, Chat chat, string messageText, List<List<InlineKeyboardButton>>? inlineButtons)
        {
            messageText = TelegramHelper.EscapeSpecialCharacters(messageText);
            InlineKeyboardMarkup? inlineButtonsMarkup = null;
            if (inlineButtons != null)
                inlineButtonsMarkup = new InlineKeyboardMarkup(inlineButtons.ToArray());

            bool sendNew = false;

            if (!SWTelegramBot.IsLastUsedControllerForChat(chat.Id, this))
                sendNew = true;
            if (!chatIdToMessageId.TryGetValue(chat.Id, out int messageId))
            {
                sendNew = true;
            }
            else if (sendNew)
            {
                try
                {
                    chatIdToMessageId.TryRemove(chat.Id, out var _);
                    await Client.DeleteMessageAsync(chat.Id, messageId);
                }
                catch { }
            }

            if (!sendNew)
            {
                try
                {
                    EditMessageTextArgs editMessageTextArgs = new(messageText)
                    {
                        ChatId = chat.Id,
                        MessageId = messageId,
                        ParseMode = ParseMode.MarkdownV2,
                        ReplyMarkup = inlineButtonsMarkup
                    };
                    var message = await Client.EditMessageTextAsync(editMessageTextArgs);
                    return message;
                }
                catch (Exception ex)
                {
                    try
                    {
                        chatIdToMessageId.TryRemove(chat.Id, out _);
                        await Client.DeleteMessageAsync(chat.Id, messageId);
                    }
                    catch { }
                    sendNew = true;
                }
            }

            if (sendNew)
            {
                SendMessageArgs args = new(chat.Id, messageText);
                args.ParseMode = ParseMode.MarkdownV2;
                args.ReplyMarkup = inlineButtonsMarkup;
                var newMessage = await Client.SendMessageAsync(args);
                chatIdToMessageId.TryAdd(chat.Id, newMessage.MessageId);
                return newMessage;
            }

            throw new Exception("Well this shouldn't have happened");
        }

        public abstract Task CleanUp(long chatId);
    }

    public class HomeTelegramController : BaseTelegramController
    {
        ConcurrentDictionary<long, int> _chatIdToHomeMessageId = new();
        public HomeTelegramController(BotClient client) : base(client)
        {
        }

        public override async Task HandleCallbackQuery(CallbackQuery update, string path, CallbackQueryCallbackData data)
        {
            if (update?.Message?.Chat?.Id == null)
                return;

            await RunInitCommand(update.Message.Chat);
        }

        public override async Task<bool> HandleMessage(Telegram.BotAPI.AvailableTypes.Message update)
        {
            return false;
            //Log message here
        }

        public override async Task RunInitCommand(Chat chat)
        {
            try
            {
                _chatIdToHomeMessageId.TryRemove(chat.Id, out int messageId);
                await Client.DeleteMessageAsync(chat.Id, messageId);
            }
            catch (Exception ex)
            {
                //Log here
            }
            string messageText =
@"🏡 *Home* 

Hi Russel 🔥
You have 3 accounts loaded
Use the buttons below to navigate";



            var balancesButton = new InlineKeyboardButton("💰 Balances")
            {
                CallbackData = "/balances"
            };
            var positionsButton = new InlineKeyboardButton("🧘 ‍Positions")
            {
                CallbackData = "/positions"
            };
            List<InlineKeyboardButton> row1 = new()
            {
                balancesButton, positionsButton
            };

            var ordersButton = new InlineKeyboardButton("🧾 Orders")
            {
                CallbackData = "/orders"
            };
            var exposuresButton = new InlineKeyboardButton("💥 Exposures")
            {
                CallbackData = "/exposures"
            };
            List<InlineKeyboardButton> row2 = new()
            {
                ordersButton, exposuresButton
            };

            var accountsButton = new InlineKeyboardButton("👝 Accounts")
            {
                CallbackData = "/accounts"
            };
            
            List<InlineKeyboardButton> row3 = new()
            {
                accountsButton, SettingsButton
            };

            List<InlineKeyboardButton> row4 = new()
            {
                TradeButton
            };


            List<List<InlineKeyboardButton>> inlineButtons = new()
            {
                row1,
                row2,
                row3,
                row4
            };

            InlineKeyboardMarkup inlineButtonsMarkup = new InlineKeyboardMarkup(inlineButtons.ToArray());
            SendMessageArgs args = new(chat.Id, messageText);
            args.ParseMode = ParseMode.MarkdownV2;
            args.ReplyMarkup = inlineButtonsMarkup;
            var message = await Client.SendMessageAsync(args);
            _chatIdToHomeMessageId.TryAdd(message.Chat.Id, message.MessageId);
        }

        public override async Task CleanUp(long chatId)
        {
        }
    }

    public class BalancesTelegramController : BaseTelegramController
    {
        public enum BalancesViewOptions
        {
            Summary, Coin, Exchange, Account
        }
        ConcurrentDictionary<long, int> _chatIdToMessageId = new();

        public BalancesTelegramController(BotClient client) : base(client)
        {
        }
        
        #region BaseTelegramController
        public override async Task HandleCallbackQuery(CallbackQuery update, string path, CallbackQueryCallbackData data)
        {
            if (update.Message?.Chat == null)
                return;

            string messageText = "";
            Enum.TryParse(data.Data, out BalancesViewOptions view);
            switch (view)
            {
                case BalancesViewOptions.Coin:
                    messageText = await GetBalanceByCoinMessageText();
                    break;
                case BalancesViewOptions.Exchange:
                    messageText = await GetBalanceByExchangeMessageText();
                    break;
                case BalancesViewOptions.Account:
                    messageText = await GetBalanceByAccountMessageText();
                    break;
                default:
                    await RunInitCommand(update.Message.Chat);
                    return;
                    break;
            }

            var message = await SendOrUpdateBalancesMessage(update.Message.Chat, messageText, view);
        }
        public override async Task<bool> HandleMessage(Telegram.BotAPI.AvailableTypes.Message update)
        {
            return false;
            //Log message here
        }
        public override async Task RunInitCommand(Chat chat)
        {
            string messageText = await GetBalanceSummaryText();
            await SendOrUpdateBalancesMessage(chat, messageText, BalancesViewOptions.Summary);
        }

        public override async Task CleanUp(long chatId)
        {
        }

        private async Task<Telegram.BotAPI.AvailableTypes.Message> SendOrUpdateBalancesMessage(Chat chat, string messageText, BalancesViewOptions view)
        {
            InlineKeyboardButton _summary = new InlineKeyboardButton($"💰 Summary {(view == BalancesViewOptions.Summary ? "✅" : "")}") { CallbackData = new CallbackQueryCallbackData("balances", BalancesViewOptions.Summary.ToString()).ToString() };
            InlineKeyboardButton _byCoinButton = new InlineKeyboardButton($"💰 Coin {(view == BalancesViewOptions.Coin ? "✅" : "")}") { CallbackData = new CallbackQueryCallbackData("balances", BalancesViewOptions.Coin.ToString()).ToString() };
            InlineKeyboardButton _byExchangeButton = new InlineKeyboardButton($"💰 Exchange {(view == BalancesViewOptions.Exchange ? "✅" : "")}") { CallbackData = new CallbackQueryCallbackData("balances", BalancesViewOptions.Exchange.ToString()).ToString() };
            InlineKeyboardButton _byAccountButton = new InlineKeyboardButton($"💰 Account {(view == BalancesViewOptions.Account ? "✅" : "")}") { CallbackData = new CallbackQueryCallbackData("balances", BalancesViewOptions.Account.ToString()).ToString() };

            List<InlineKeyboardButton> row1 = new()
            {
                _summary, _byCoinButton
            };
            List<InlineKeyboardButton> row2 = new()
            {
                _byExchangeButton, _byAccountButton
            };

            List<List<InlineKeyboardButton>> inlineButtons = new()
            {
                row1,
                row2,
                HomeButtonRow,
            };
            return await BaseSendOrUpdate(_chatIdToMessageId, chat, messageText, inlineButtons);
        }
        #endregion
        
        private async Task<string> GetBalanceSummaryText()
        {
            return
@"*Balances Summary*
`
Total:        $4,348,152
Top Exchange: Binance
Top Coin:     BTC`

Balances can be viewed as a summary or aggregated by one of the below categories:";
        }
        private async Task<string> GetBalanceByCoinMessageText()
        {
            return
@"*Balances by Coin*
`
BTC: 0.5014      ($14,876.54)
ETH: 3.414       ($ 6,341.74)
SHIB: 14,102,479 ($   107.88)
Other:           ($ 4,467.34)

Total            ($25,813.87)`

Balances can be viewed as a summary or aggregated by one of the below categories:";
        }
        private async Task<string> GetBalanceByAccountMessageText()
        {
            return
@"*Balances by Account*
`
Strat1_ByBIT     : $13,573.37
Strat1_BitMEX    : $ 7,641.41
Strat1_BINANANCE : $ 3,127.20
PA_Trading_Bybit : $ 1,345.14
PA_Trading_Bi... : $   110.67

Total              $25,813.87`

Balances can be viewed as a summary or aggregated by one of the below categories:";
        }
        private async Task<string> GetBalanceByExchangeMessageText()
        {
            return
@"*Balances by Exchange*
`
ByBIT     : $14,918.51
BitMEX    : $ 7,752.08
Binance   : $ 3,127.20

Total       $25,813.87`

Balances can be viewed as a summary or aggregated by one of the below categories:";
        }
    }

    public class PositionsTelegramController : BaseTelegramController
    {
        public class Position
        {
            public string PositionText { get; set; }
            public int PositionId { get; set; }
        }
        InlineKeyboardButton _bySummary = new InlineKeyboardButton($"🔙") { CallbackData = new CallbackQueryCallbackData("positions", PositionsViewOptions.Summary.ToString()).ToString() };
        InlineKeyboardButton _details = new InlineKeyboardButton($"🔍 View Details") { CallbackData = new CallbackQueryCallbackData("positions", PositionsViewOptions.Details.ToString()).ToString() };
        InlineKeyboardButton _byLiquidationButton = new InlineKeyboardButton($"Liquidation") { CallbackData = new CallbackQueryCallbackData("positions", PositionsViewOptions.Liquidation.ToString()).ToString() };
        InlineKeyboardButton _byWinButton = new InlineKeyboardButton($"🏆 Winners") { CallbackData = new CallbackQueryCallbackData("positions", PositionsViewOptions.Win.ToString()).ToString() };
        InlineKeyboardButton _byLoseButton = new InlineKeyboardButton($"Losers") { CallbackData = new CallbackQueryCallbackData("positions", PositionsViewOptions.Lose.ToString()).ToString() };
        InlineKeyboardButton _byLongButton = new InlineKeyboardButton($"Long") { CallbackData = new CallbackQueryCallbackData("positions", PositionsViewOptions.Long.ToString()).ToString() };
        InlineKeyboardButton _byShortButton = new InlineKeyboardButton($"Short") { CallbackData = new CallbackQueryCallbackData("positions", PositionsViewOptions.Short.ToString()).ToString() };
        InlineKeyboardButton _bySizeButton = new InlineKeyboardButton($"Size") { CallbackData = new CallbackQueryCallbackData("positions", PositionsViewOptions.Size.ToString()).ToString() };

        public enum PositionsViewOptions
        {
            ShowButtons, Summary, Details, Liquidation, Win, Lose, Long, Short, Size
        }
        ConcurrentDictionary<long, int> _chatIdToMessageId = new();
        ConcurrentDictionary<long, int> _chatIdToPromptId = new();
        ConcurrentDictionary<long, List<int>> _chatIdToPositionMessages = new();

        public PositionsTelegramController(BotClient client) : base(client) {}

        #region BaseTelegramController
        public override async Task HandleCallbackQuery(CallbackQuery update, string path, CallbackQueryCallbackData data)
        {
            if (update.Message?.Chat == null)
                return;
            var message = await MoveMessageToBottomIfNecessary(update);
            var chat = message.Chat;

            if (!SWTelegramBot.TelegramSettingsPerChat.TryGetValue(chat.Id, out var settings))
                return;

            if (path == "positions")
                await ManageBasePositionsPath(message, data, chat, settings);
            if (path == "positions/marketClose")
            {

            }

            if (path == "positions/limitClose")
            {

            }
        }
        public override async Task<bool> HandleMessage(Telegram.BotAPI.AvailableTypes.Message message)
        {
            if (message?.Chat == null)
                return false;

            await RemovePrompt(message.Chat.Id);
            
            return false;
            //Log message here
        }
        public override async Task RunInitCommand(Chat chat)
        {
            await RemovePrompt(chat.Id);
            await SendSummaryView(chat);
        }

        public override async Task CleanUp(long chatId)
        {
            await RemovePrompt(chatId);
        }
        #endregion

        private async Task<Telegram.BotAPI.AvailableTypes.Message> MoveMessageToBottomIfNecessary(CallbackQuery update)
        {
            if (!SWTelegramBot.IsLastUsedControllerForChat(update.Message.Chat.Id, this))
            {
                SendMessageArgs args = new(update.Message.Chat.Id, TelegramHelper.EscapeSpecialCharacters(update.Message.Text));
                args.ParseMode = ParseMode.MarkdownV2;
                args.ReplyMarkup = update.Message.ReplyMarkup;
                var message = await Client.SendMessageAsync(args);
                try
                {
                    await Client.DeleteMessageAsync(update.Message.Chat.Id, update.Message.MessageId);
                }
                catch (Exception ex)
                {
                    //Log
                }
                _chatIdToMessageId.TryRemove(update.Message.Chat.Id, out _);
                _chatIdToMessageId.TryAdd(message.Chat.Id, message.MessageId);
                
                return message;
            }
            return update.Message;
        }

        private async Task ManageBasePositionsPath(Telegram.BotAPI.AvailableTypes.Message message, CallbackQueryCallbackData data, Chat chat, TelegramSettings settings)
        {
            Enum.TryParse(data.Data, out PositionsViewOptions view);
            switch (view)
            {
                case PositionsViewOptions.Summary:
                    await RemovePrompt(chat.Id);
                    await SendSummaryView(chat);
                    break;
                case PositionsViewOptions.Details:
                    await RemovePrompt(chat.Id);
                    await SendOrUpdateViewDetailsByPrompt(chat.Id, settings, null);
                    break;
                case PositionsViewOptions.Liquidation:
                    await RemovePrompt(chat.Id);
                    await SendLiquidationView(chat, settings);
                    break;
                case PositionsViewOptions.Win:
                    await RemovePrompt(chat.Id);
                    await SendWinnersView(chat, settings);
                    break;
                case PositionsViewOptions.Lose:
                    await RemovePrompt(chat.Id);
                    await SendLosersView(chat, settings);
                    break;
                case PositionsViewOptions.Long:
                    await RemovePrompt(chat.Id);
                    await SendLongView(chat, settings);
                    break;
                case PositionsViewOptions.Short:
                    await RemovePrompt(chat.Id);
                    await SendShortView(chat, settings);
                    break;
                case PositionsViewOptions.Size:
                    await RemovePrompt(chat.Id);
                    await SendSizeView(chat, settings);
                    break;
                case PositionsViewOptions.ShowButtons:
                    settings.Pos_ShowActionButtonsPerPosition = !settings.Pos_ShowActionButtonsPerPosition;
                    await SettingsTelegramController.UpdateSettingsMessage(Client, chat.Id, settings);
                    await SendOrUpdateViewDetailsByPrompt(chat.Id, settings, message.MessageId);
                    break;
                default:
                    await RemovePrompt(chat.Id);
                    await RunInitCommand(message.Chat);
                    return;
                    break;
            }
        }
        private async Task SendSummaryView(Chat chat)
        {
            var messageText = await GetPositionsSummaryText();

            List<InlineKeyboardButton> row1 = new()
            {
                _details, HomeButton
            };

            List<List<InlineKeyboardButton>> inlineButtons = new()
            {
                row1
            };

            await BaseSendOrUpdate(_chatIdToMessageId, chat, messageText, inlineButtons);
        }
        private async Task SendWinnersView(Chat chat, TelegramSettings settings)
        {
            var messageText = await GetPositionsSummaryText();

            List<InlineKeyboardButton> row1 = new()
            {
                _bySummary, HomeButton
            };

            List<List<InlineKeyboardButton>> inlineButtons = new()
            {
                row1
            };

            await BaseSendOrUpdate(_chatIdToMessageId, chat, messageText, inlineButtons);
        }
        private async Task SendLosersView(Chat chat, TelegramSettings settings)
        {
            var messageText = await GetPositionsSummaryText();

            List<InlineKeyboardButton> row1 = new()
            {
                _bySummary, HomeButton
            };

            List<List<InlineKeyboardButton>> inlineButtons = new()
            {
                row1
            };

            await BaseSendOrUpdate(_chatIdToMessageId, chat, messageText, inlineButtons);
        }
        private async Task SendLiquidationView(Chat chat, TelegramSettings settings)
        {
            if (settings.Pos_ShowActionButtonsPerPosition)
                await SendLiquidationViewWithButtons(chat);
            else
                await SendLiquidationViewSingle(chat);
        }

        private async Task SendLiquidationViewSingle(Chat chat)
        {
            var messageText = await GetPositionsByLiquidationMessageText();

            List<InlineKeyboardButton> row1 = new()
            {
                _bySummary, HomeButton
            };

            List<List<InlineKeyboardButton>> inlineButtons = new()
            {
                row1
            };

            await BaseSendOrUpdate(_chatIdToMessageId, chat, messageText, inlineButtons);
        }

        private async Task SendLiquidationViewWithButtons(Chat chat)
        {
            var messages = await GetPositionsByLiquidationDetails();
            await BaseSendOrUpdate(_chatIdToMessageId, chat, "*Positions closest to liquidation* 👇", null);
            List<int> possitionMessageIds = new();
            var lastPosition = messages.Last();
            foreach (var position in messages)
            {
                var inlineButtons = await GetClosePositionButtons(position);
                var message = await Client.SendMessageAsync(TelegramHelper.BuildSendMessageArgs(chat.Id, position.PositionText, inlineButtons));
                possitionMessageIds.Add(message.MessageId);
            }

            List<List<InlineKeyboardButton>> backButtonRow = new() { new() { _bySummary /*new($"Back") { CallbackData = new CallbackQueryCallbackData(@"positions/back", "").ToString() }*/ } };
            var backMessage = await Client.SendMessageAsync(TelegramHelper.BuildSendMessageArgs(chat.Id, "*Positions closest to liquidation* 👆", backButtonRow));
            possitionMessageIds.Add(backMessage.MessageId);

            _chatIdToPositionMessages.TryAdd(chat.Id, possitionMessageIds);
        }
        private async Task<List<List<InlineKeyboardButton>>> GetClosePositionButtons(Position position)
        {
            var market = new InlineKeyboardButton($"Market Close") { CallbackData = new CallbackQueryCallbackData(@"positions/marketClose", position.PositionId.ToString()).ToString() };
            var limit = new InlineKeyboardButton($"Limit Close") { CallbackData = new CallbackQueryCallbackData(@"positions/limitClose", position.PositionId.ToString()).ToString() };
            List<InlineKeyboardButton> row = new()
            {
                market, limit
            };
            List<List<InlineKeyboardButton>> buttons = new()
            {
                row
            };
            return buttons;
        }
        private async Task SendLongView(Chat chat, TelegramSettings settings)
        {
            var messageText = await GetPositionsLongText();

            List<InlineKeyboardButton> row1 = new()
            {
                _bySummary, HomeButton
            };

            List<List<InlineKeyboardButton>> inlineButtons = new()
            {
                row1
            };

            await BaseSendOrUpdate(_chatIdToMessageId, chat, messageText, inlineButtons);
        }
        private async Task SendShortView(Chat chat, TelegramSettings settings)
        {
            var messageText = await GetPositionsShortText();

            List<InlineKeyboardButton> row1 = new()
            {
                _bySummary, HomeButton
            };

            List<List<InlineKeyboardButton>> inlineButtons = new()
            {
                row1
            };

            await BaseSendOrUpdate(_chatIdToMessageId, chat, messageText, inlineButtons);
        }
        private async Task SendSizeView(Chat chat, TelegramSettings settings)
        {
            var messageText = await GetPositionsSummaryText();

            List<InlineKeyboardButton> row1 = new()
            {
                _bySummary, HomeButton
            };

            List<List<InlineKeyboardButton>> inlineButtons = new()
            {
                row1
            };

            await BaseSendOrUpdate(_chatIdToMessageId, chat, messageText, inlineButtons);
        }
        private async Task SendOrUpdateViewDetailsByPrompt(long chatId, TelegramSettings settings, int? messageId)
        {
            string messageText = @"View position details by:";
            var actionsEmoji = TelegramHelper.GetBoolEmoji(settings.Pos_ShowActionButtonsPerPosition);
            var showButtonsButton = new InlineKeyboardButton($"Show Actions {actionsEmoji}") { CallbackData = new CallbackQueryCallbackData("positions", PositionsViewOptions.ShowButtons.ToString()).ToString() };
            List<InlineKeyboardButton> row1 = new()
            {
                showButtonsButton
            };
            List<InlineKeyboardButton> row2 = new()
            {
                _byLiquidationButton, _bySizeButton
            };
            List<InlineKeyboardButton> row3 = new()
            {
                _byWinButton, _byLoseButton
            };
            List<InlineKeyboardButton> row4 = new()
            {
                _byLongButton, _byShortButton
            };

            List<List<InlineKeyboardButton>> inlineButtons = new()
            {
                row1,
                row2,
                row3,
                row4
            };

            
            if (messageId.HasValue)
            {
                InlineKeyboardMarkup inlineButtonsMarkup = new InlineKeyboardMarkup(inlineButtons.ToArray());
                EditMessageReplyMarkup args = new EditMessageReplyMarkup()
                {
                    ChatId = chatId,
                    MessageId = messageId.Value,
                    ReplyMarkup = inlineButtonsMarkup
                };
                await Client.EditMessageReplyMarkupAsync(args);
            }
            else
            {
                var message = await Client.SendMessageAsync(TelegramHelper.BuildSendMessageArgs(chatId, messageText, inlineButtons));
                messageId = message.MessageId;
            }
            _chatIdToPromptId.TryAdd(chatId, messageId.Value);
        }



        private async Task RemovePrompt(long chatId)
        {
            List<Task> tasks = new();
            if (_chatIdToPromptId.TryRemove(chatId, out int promptMessageId))
            {
                try
                {
                    tasks.Add(Client.DeleteMessageAsync(chatId, promptMessageId));
                }
                catch (Exception) { }
            }

            if (_chatIdToPositionMessages.TryRemove(chatId, out List<int> possitionMessageIds))
            {
                foreach (var possitionMessageId in possitionMessageIds)
                {
                    try
                    {
                        tasks.Add(Client.DeleteMessageAsync(chatId, possitionMessageId));
                    }
                    catch (Exception) { }
                }
            }
            await Task.WhenAll(tasks);
            //TODO: Add with aggregate exceptions
        }

        private async Task<Telegram.BotAPI.AvailableTypes.Message> SendOrUpdateBalancesMessage(Chat chat, string messageText, PositionsViewOptions view)
        {

            List<InlineKeyboardButton> row1 = new()
            {
                _bySummary, _byLiquidationButton
            };
            List<InlineKeyboardButton> row2 = new()
            {
                _byWinButton, _byLoseButton, _bySizeButton,
            };
            List<InlineKeyboardButton> row3 = new()
            {
                _byLongButton, _byShortButton
            };

            List<InlineKeyboardButton> row4 = new()
            {
                HomeButton, SettingsButton
            };

            List<List<InlineKeyboardButton>> inlineButtons = new()
            {
                row1,
                row2,
                row3,
                row4,
            };

            //InlineKeyboardMarkup inlineButtonsMarkup = new InlineKeyboardMarkup(inlineButtons.ToArray());
            return await BaseSendOrUpdate(_chatIdToMessageId, chat, messageText, inlineButtons);
        }

        private async Task<string> GetPositionsSummaryText()
        {
            return
@"*Positions*

# of positions: 6
Total UPnL:  $ -0.16
Min Distance to Liq: 70%

`Symbol   $ Total  Eff Exp
TOTAL      -2.2m  (  9.3%)`
*Longs  (4)*
` BTC       12.8k  ( 40.4%)
 ADA          12  (  1.0%)
 BAT         981  (  1.0%)
 BLUR       9.8m  (  1.0%)`
*Shorts  (3)*
` BTC      -12.8k  ( -4.4%)
 ADA         -12  ( -1.0%)
 BLUR      -9.8m  ( -1.0%)`

Positions can be viewed as a summary or aggregated by one of the below categories:";
        }
        private async Task<string> GetPositionsByLiquidationMessageText()
        {
            return
@"*Positions closest to liquidation*
`
▲ XBTUSD        BitMEX 
20k  @29,100    2.5% 

▼ ETHUSD        Binance 
1.6m  @2,100    5.9% 

▲ XBTUSD        BitMEX 
20k  @29,100    10.48% 

▼ ETHUSD        Binance 
1.6m  @2,100    23.15% 

Total            ($25,813.87)`

Positions can be viewed as a summary or aggregated by one of the below categories:";
        }
        private async Task<string> GetPositionsByWinLoseText()
        {
            return
@"*Positions (Winners/Losers)*
`
Strat1_ByBIT     : $13,573.37
Strat1_BitMEX    : $ 7,641.41
Strat1_BINANANCE : $ 3,127.20
PA_Trading_Bybit : $ 1,345.14
PA_Trading_Bi... : $   110.67

Total              $25,813.87`

Positions can be viewed as a summary or aggregated by one of the below categories:";
        }
        private async Task<string> GetPositionsLongText()
        {
            return
@"*Top 5 Long Positions*
`
🟩🟩🟩 LONGS 🟩🟩🟩

▲ XBTUSD        BitMEX 
20k  @29,100    + $45k

▼ ETHUSD        Binance 
1.6m  @2,100    - $105k

▲ BTCUSDT_Perp    BitMEX 
20k  @29,100      + $412

▼ AACEUSDC_Coin   Huobi 
20k  @29,100      - $1.4
`

Positions can be viewed as a summary or aggregated by one of the below categories:";
        }
        private async Task<string> GetPositionsShortText()
        {
            return
@"*Top 5 Short Positions*
`
🟥🟥🟥 SHORTS 🟥🟥🟥

▲ BTCUSDT_Perp    BitMEX 
20k  @29,100      + $412

▼ AACEUSDC_Coin   Huobi 
20k  @29,100      - $1.4

▲ XBTUSD        BitMEX 
20k  @29,100    + $45k

▼ ETHUSD        Binance 
1.6m  @2,100    - $105k
`

Positions can be viewed as a summary or aggregated by one of the below categories:";
        }
        private async Task<string> GetPositionsBySizeMessageText()
        {
            return
@"*Positions by size*
`
ByBIT     : $14,918.51
BitMEX    : $ 7,752.08
Binance   : $ 3,127.20

Total       $25,813.87`

Positions can be viewed as a summary or aggregated by one of the below categories:";
        }
        private async Task<string> GetPositionsByLiquidationDetailsMessageText()
        {
            string text = $"*Positions closest to liquidation*{Environment.NewLine}{Environment.NewLine}";
            text += string.Join($"{Environment.NewLine}{Environment.NewLine}", await GetPositionsByLiquidationDetails());
            text += $"{Environment.NewLine}{Environment.NewLine}";
            text +=
@"`Total            ($25,813.87)`

Positions can be viewed as a summary or aggregated by one of the below categories:";
            return text;
        }
        private async Task<List<Position>> GetPositionsByLiquidationDetails()
        {
            List<Position> positions = new List<Position>()
            {
                new Position()
                {
                    PositionId = 1,
                    PositionText =
@"`🏅XBTUSD
Pr:30,200(USD)    Qt:200,000.15(USD)
UPnL:0.0053(BTC)  Liq%:2.5
Mark:30,500(USD)`",
                },
                new Position()
                {
                    PositionId = 2,
                    PositionText =
@"`🏅XBTUSD
Pr:30,200(USD)    Qt:200,000.15(USD)
UPnL:0.0053(BTC)  Liq%:5.9
Mark:30,500(USD)`",
                },
                new Position()
                {
                    PositionId = 3,
                    PositionText =
@"`🏅XBTUSD
Pr:30,200(USD)    Qt:200,000.15(USD)
UPnL:0.0053(BTC)  Liq%:10.48
Mark:30,500(USD)`",
                },
                new Position()
                {
                    PositionId = 4,
                    PositionText =
@"`🏅XBTUSD
Pr:30,200(USD)    Qt:200,000.15(USD)
UPnL:0.0053(BTC)  Liq%:23.15
Mark:30,500(USD)`",
                },
            };

            return positions;
        }
        private async Task<string> GetPositionsByWinLoseDetailsText()
        {
            return
@"*Positions (Winners/Losers)*
`
Strat1_ByBIT     : $13,573.37
Strat1_BitMEX    : $ 7,641.41
Strat1_BINANANCE : $ 3,127.20
PA_Trading_Bybit : $ 1,345.14
PA_Trading_Bi... : $   110.67

Total              $25,813.87`

Positions can be viewed as a summary or aggregated by one of the below categories:";
        }
        private async Task<string> GetPositionsByLongShortDetailsMessageText()
        {
            return
@"*Positions (Long/Short)*
`
ByBIT     : $14,918.51
BitMEX    : $ 7,752.08
Binance   : $ 3,127.20

Total       $25,813.87`

Positions can be viewed as a summary or aggregated by one of the below categories:";
        }
        private async Task<string> GetPositionsBySizeDetailsMessageText()
        {
            return
@"*Positions by size*
`
ByBIT     : $14,918.51
BitMEX    : $ 7,752.08
Binance   : $ 3,127.20

Total       $25,813.87`

Positions can be viewed as a summary or aggregated by one of the below categories:";
        }
    }

    public class OrdersTelegramController : BaseTelegramController
    {
        public OrdersTelegramController(BotClient client) : base(client)
        {
        }

        public override Task HandleCallbackQuery(CallbackQuery update, string path, CallbackQueryCallbackData data)
        {
            throw new NotImplementedException();
        }

        public override async Task<bool> HandleMessage(Telegram.BotAPI.AvailableTypes.Message update)
        {
            return false;
            //Log message here
        }

        public override async Task RunInitCommand(Chat chat)
        {
            await Client.SendMessageAsync(chat.Id, "Orders coming soon...");
        }

        public override async Task CleanUp(long chatId)
        {
        }
    }

    public class ExposuresTelegramController : BaseTelegramController
    {
        public ExposuresTelegramController(BotClient client) : base(client)
        {
        }

        public override Task HandleCallbackQuery(CallbackQuery update, string path, CallbackQueryCallbackData data)
        {
            throw new NotImplementedException();
        }

        public override async Task<bool> HandleMessage(Telegram.BotAPI.AvailableTypes.Message update)
        {
            return false;
            //Log message here
        }

        public override async Task RunInitCommand(Chat chat)
        {
            await Client.SendMessageAsync(chat.Id, "Exposures coming soon...");
        }

        public override async Task CleanUp(long chatId)
        {
        }
    }

    public class TradeTelegramController : BaseTelegramController
    {
        public enum OrderType
        {
            Market,
            Limit
        }
        public class TelegramTradePoco
        {
            public Guid TradeId { get; } = Guid.NewGuid();
            public int MainMessageId { get; set; }
            public string? AccountId { get; set; }
            public string? AccountName { get; set; }
            public string? SymbolSW { get; set; }
            public string? SymbolExchange { get; set; }
            public OrderType? OrderType { get; set; }
            public decimal? Price { get; set; }
            public decimal? Quantity { get; set; }
            public int? QtyPricePromptMessageId { get; set; }
            public bool IsWaitingForUserInput { get; set; } = false;
            public bool IsEmpty
            {
                get
                {
                    return AccountId == null;
                }
            }

            public void Reset()
            {
                AccountId = null;
                AccountName = null;
                SymbolSW = null;
                SymbolExchange = null;
                OrderType = null;
                Price = null;
                Quantity = null;
                QtyPricePromptMessageId = null;
                IsWaitingForUserInput = false;
            }
        }

        public class TradeInstrumentSelectionCallBackData
        {
            public TradeInstrumentSelectionCallBackData(string tradeId, string accountId, string symboleSW)
            {
                AccountId = accountId;
                SymbolSW = symboleSW;
            }
            public TradeInstrumentSelectionCallBackData(string data)
            {
                var elements = data.Split(':');
                if (elements.Length != 2)
                {
                    throw new ArgumentException();
                }
                AccountId = elements[0];
                SymbolSW = elements[1];
            }
            public string AccountId { get; set; }
            public string SymbolSW { get; set; }
            public override string ToString()
            {
                return $"{AccountId}:{SymbolSW}";
            }
        }

        ConcurrentDictionary<long, ConcurrentDictionary<int, TelegramTradePoco>> _chatIdToMessageIdToTradePoco = new();
        ConcurrentDictionary<long, TelegramTradePoco> _chatIdToLastUsedTradePoco = new();

        public TradeTelegramController(BotClient client) : base(client) {}
        #region BaseTelegramController
        public override async Task HandleCallbackQuery(CallbackQuery update, string path, CallbackQueryCallbackData data)
        {
            if (update?.Message?.Chat?.Id == null)
                return;


            if (!_chatIdToMessageIdToTradePoco.TryGetValue(update.Message.Chat.Id, out ConcurrentDictionary<int, TelegramTradePoco> chatTrades))
                return;

            if (!chatTrades.TryGetValue(update.Message.MessageId, out TelegramTradePoco tradePoco))
                return;

            var message = await MoveMessageToBottomIfNecessary(update, tradePoco);

            UpdateLastUsedTradePocoPerChatId(message.Chat.Id, tradePoco);

            await DeletePromptMessage(message.Chat.Id, tradePoco);

            if (path == "trade/inst")
            {
                TradeInstrumentSelectionCallBackData callbackData = new(data.Data);
                tradePoco.AccountId = callbackData.AccountId;
                tradePoco.AccountName = TelegramDataProvider.GetAccountName(tradePoco.AccountId);
                tradePoco.SymbolSW = callbackData.SymbolSW;
                tradePoco.SymbolExchange = TelegramDataProvider.GetSymbolExchange(tradePoco.AccountId, tradePoco.SymbolSW);
                await UpdateMessageToQuantitySelection(data, tradePoco, message);
                return;
            }

            if (path == "trade/qty")
            {
                if (data.Data == "custom")
                {
                    var promptMessage = await Client.SendMessageAsync(update.Message.Chat.Id, "Please enter your custom quantity:");
                    tradePoco.QtyPricePromptMessageId = promptMessage.MessageId;
                    tradePoco.IsWaitingForUserInput = true;
                    return;
                }

                if (!decimal.TryParse(data.Data, out var qty))
                    return;
                tradePoco.Quantity = qty;
                await UpdateMessageToPriceSelection(tradePoco, message);

                return;
            }

            if (path == "trade/price")
            {
                await Client.SendMessageAsync(update.Message.Chat.Id, "Not implemented yet");
            }

            if (path == "trade/back")
            {
                if (data.Data == "qty")
                {
                    await RunInitCommand(message.Chat);
                }
                else if (data.Data == "price")
                {
                    tradePoco.Quantity = null;
                    await UpdateMessageToQuantitySelection(data, tradePoco, message);
                }
                else if (data.Data == "confirm")
                {
                    tradePoco.Price = null;
                    await UpdateMessageToPriceSelection(tradePoco, message);
                }
            }
        }

        public override async Task<bool> HandleMessage(Telegram.BotAPI.AvailableTypes.Message message)
        {
            if (message.Chat?.Id == null)
                return false;

            if (!_chatIdToLastUsedTradePoco.TryGetValue(message.Chat.Id, out TelegramTradePoco tradePoco))
                return false;

            if (!decimal.TryParse(message.Text, out var decimalValue))
                return false;

            await DeletePromptMessage(message.Chat.Id, tradePoco);
            if (!tradePoco.IsWaitingForUserInput)
                return false;

            tradePoco.IsWaitingForUserInput = false;

            if (tradePoco.SymbolSW == null)
            {
                tradePoco.Quantity = null;
                tradePoco.Price = null;
                return false;
            }
            else if (tradePoco.Quantity == null)
            {
                tradePoco.Quantity = decimalValue;

                string messageText = GetBaseTradeMessageText(tradePoco);
                messageText += $"{Environment.NewLine}Select Price:";
                InlineKeyboardMarkup inlineButtonsMarkup = GetPriceInputButtons();

                EditMessageTextArgs textArgs = new(messageText)
                {
                    ReplyMarkup = inlineButtonsMarkup,
                    ParseMode = ParseMode.MarkdownV2,
                    ChatId = message.Chat.Id,
                    MessageId = tradePoco.MainMessageId,
                };


                await Client.EditMessageTextAsync(textArgs);
                await Client.DeleteMessageAsync(message.Chat.Id, message.MessageId);
            }
            else if (tradePoco.Price == null)
            {
                tradePoco.Price = decimalValue;
                string messageText = GetBaseTradeMessageText(tradePoco);
                messageText += $"{Environment.NewLine}Confirm Trade:";


                List<InlineKeyboardButton> row1 = new()
                {
                    new("Execute") {CallbackData =  new CallbackQueryCallbackData("trade/execute", "").ToString()},
                };

                List<List<InlineKeyboardButton>> inlineButtons = new()
                {
                    row1,
                    HomeButtonRow
                };


                InlineKeyboardMarkup inlineButtonsMarkup = new InlineKeyboardMarkup(inlineButtons.ToArray());

                EditMessageTextArgs textArgs = new(messageText)
                {
                    ReplyMarkup = inlineButtonsMarkup,
                    ParseMode = ParseMode.MarkdownV2,
                    ChatId = message.Chat.Id,
                    MessageId = tradePoco.MainMessageId,
                };

                await Client.EditMessageTextAsync(textArgs);
                await Client.DeleteMessageAsync(message.Chat.Id, message.MessageId);
            }
            return true;
        }

        public override async Task RunInitCommand(Chat chat)
        {
            TelegramTradePoco tradePoco = new();
            var trades = _chatIdToMessageIdToTradePoco.GetOrAdd(chat.Id, new ConcurrentDictionary<int, TelegramTradePoco>());
            foreach (var trade in trades.ToList())
            {
                await DeletePromptMessage(chat.Id, trade.Value);
                //Removed the if statement because we only want 1 active trade per chat
                //If we keep this removed the concurrent dictionary can be changed
                //if (trade.Value.IsEmpty) 
                {
                    trades.TryRemove(trade.Key, out var poco);
                    Client.DeleteMessage(chat.Id, trade.Key);
                }
            }

            string messageText = GetBaseTradeMessageText(tradePoco);
            messageText += $"{Environment.NewLine}Select Instrument:";

            var favourites = TelegramDataProvider.GetFavourites();

            List<List<InlineKeyboardButton>> inlineButtons = new();
            foreach (var account in TelegramDataProvider.GetLoadedAccounts())
            {
                List<InlineKeyboardButton> accRow = new()
                {
                    //new InlineKeyboardButton ($"👝    {account.AccountName}    👝")
                    new InlineKeyboardButton ($"▓▓▓▓    {account.AccountName}    ▓▓▓▓")
                    {
                        CallbackData = "Account1"
                    }
                };
                inlineButtons.Add(accRow);

                var favsForExchange = favourites.Where(x => x.Exchange == account.Exchange).ToList();

                while (favsForExchange.Any())
                {
                    List<InlineKeyboardButton> instRowRow = new();
                    var fav = favsForExchange.First();
                    var data = new TradeInstrumentSelectionCallBackData(tradePoco.TradeId.ToString(), account.AccountID, fav.SymbolSW);
                    var button = new InlineKeyboardButton($"🏅 {fav.SymbolExchange}") { CallbackData = new CallbackQueryCallbackData("trade/inst", data.ToString()).ToString() };
                    instRowRow.Add(button);
                    favsForExchange.Remove(fav);

                    if (favsForExchange.Count() > 0)
                    {
                        var fav2 = favsForExchange.First();
                        var data2 = new TradeInstrumentSelectionCallBackData(tradePoco.TradeId.ToString(), account.AccountID, fav.SymbolSW);
                        var button2 = new InlineKeyboardButton($"🏅 {fav2.SymbolExchange}") { CallbackData = new CallbackQueryCallbackData("trade/inst", data2.ToString()).ToString() };
                        instRowRow.Add(button2);
                        favsForExchange.Remove(fav2);
                    }

                    inlineButtons.Add(instRowRow);
                }
            }

            inlineButtons.Add(HomeButtonRow);

            InlineKeyboardMarkup inlineButtonsMarkup = new InlineKeyboardMarkup(inlineButtons.ToArray());
            SendMessageArgs args = new(chat.Id, messageText);
            args.ParseMode = ParseMode.MarkdownV2;
            args.ReplyMarkup = inlineButtonsMarkup;
            var message = await Client.SendMessageAsync(args);
            trades.TryAdd(message.MessageId, tradePoco);
            tradePoco.MainMessageId = message.MessageId;
            UpdateLastUsedTradePocoPerChatId(chat.Id, tradePoco);
        }

        public override async Task CleanUp(long chatId)
        {
            if (_chatIdToLastUsedTradePoco.TryGetValue(chatId, out var poco))
                await DeletePromptMessage(chatId, poco);
        }
        #endregion
        private async Task UpdateMessageToQuantitySelection(CallbackQueryCallbackData data, TelegramTradePoco tradePoco, Telegram.BotAPI.AvailableTypes.Message message)
        {

            string messageText = GetBaseTradeMessageText(tradePoco);
            messageText += $"{Environment.NewLine}Select Quantity:";
            InlineKeyboardMarkup inlineButtonsMarkup = CreateQuantityInputButtons();

            await UpdateMessage(message, messageText, inlineButtonsMarkup);
        }
        private static InlineKeyboardMarkup CreateQuantityInputButtons()
        {
            List<InlineKeyboardButton> row1 = new()
                {
                    new("100") {CallbackData =  new CallbackQueryCallbackData("trade/qty", "100").ToString()},
                    new("1,000") {CallbackData =  new CallbackQueryCallbackData("trade/qty", "1000").ToString()}
                };

            List<InlineKeyboardButton> row2 = new()
                {
                    new("10,000") {CallbackData =  new CallbackQueryCallbackData("trade/qty", "10000").ToString()},
                    new("50,000") {CallbackData =  new CallbackQueryCallbackData("trade/qty", "50000").ToString()}
                };

            List<InlineKeyboardButton> row3 = new()
                {
                    new("100,000") {CallbackData =  new CallbackQueryCallbackData("trade/qty", "100000").ToString()},
                    new("500,000") {CallbackData =  new CallbackQueryCallbackData("trade/qty", "500000").ToString()}
                };

            List<InlineKeyboardButton> row4 = new()
                {
                    new("Custom") {CallbackData =  new CallbackQueryCallbackData("trade/qty", "custom").ToString()},
                };

            List<List<InlineKeyboardButton>> inlineButtons = new()
                {
                    row1,
                    row2,
                    row3,
                    row4,
                    CreateBackButtonRow("qty"),
                    HomeButtonRow
                };


            InlineKeyboardMarkup inlineButtonsMarkup = new InlineKeyboardMarkup(inlineButtons.ToArray());
            return inlineButtonsMarkup;
        }
        private async Task UpdateMessageToPriceSelection(TelegramTradePoco tradePoco, Telegram.BotAPI.AvailableTypes.Message message)
        {
            string messageText = GetBaseTradeMessageText(tradePoco);
            messageText += $"{Environment.NewLine}Select Price:";
            InlineKeyboardMarkup inlineButtonsMarkup = GetPriceInputButtons();

            //EditMessageTextArgs textArgs = CreateEnterPriceEditMessage(update.Message.Chat.Id, tradePoco.MainMessageId, tradePoco);
            //await Client.EditMessageTextAsync(textArgs);

            await UpdateMessage(message, messageText, inlineButtonsMarkup);
        }
        private static InlineKeyboardMarkup GetPriceInputButtons()
        {
            List<InlineKeyboardButton> row1 = new()
                {
                    new("Market") {CallbackData =  new CallbackQueryCallbackData("trade/price", "market").ToString()},
                };

            List<InlineKeyboardButton> row2 = new()
                {
                    new("Best Bid") {CallbackData =  new CallbackQueryCallbackData("trade/price", "bb").ToString()},
                    new("Best Offer") {CallbackData =  new CallbackQueryCallbackData("trade/price", "bo").ToString()}
                };

            List<InlineKeyboardButton> row3 = new()
                {
                    new("Best Bid - 5") {CallbackData =  new CallbackQueryCallbackData("trade/price", "bb-5").ToString()},
                    new("Best Offer + 5") {CallbackData =  new CallbackQueryCallbackData("trade/price", "bo+5").ToString()}
                };

            List<InlineKeyboardButton> row4 = new()
                {
                    new("Best Bid - 10") {CallbackData =  new CallbackQueryCallbackData("trade/price", "bb-10").ToString()},
                    new("Best Offer + 10") {CallbackData =  new CallbackQueryCallbackData("trade/price", "bo+10").ToString()}
                };

            List<InlineKeyboardButton> row5 = new()
                {
                    new("Best Bid - 15") {CallbackData =  new CallbackQueryCallbackData("trade/price", "bb-15").ToString()},
                    new("Best Offer + 15") {CallbackData =  new CallbackQueryCallbackData("trade/price", "bo+15").ToString()}
                };

            List<InlineKeyboardButton> row6 = new()
                {
                    new("Custom") {CallbackData =  new CallbackQueryCallbackData("trade/price", "custom").ToString()},
                };


            List<List<InlineKeyboardButton>> inlineButtons = new()
                {
                    row1,
                    row2,
                    row3,
                    row4,
                    row5,
                    row6,
                    CreateBackButtonRow("price"),
                    HomeButtonRow
                };


            InlineKeyboardMarkup inlineButtonsMarkup = new InlineKeyboardMarkup(inlineButtons.ToArray());
            return inlineButtonsMarkup;
        }
        private string GetBaseTradeMessageText(TelegramTradePoco tradePoco)
        {
            return
$@"*Trade*

Use the buttons below to setup your order\. We'll show you the final summary before placing the order\.

Account: {TelegramHelper.EscapeSpecialCharacters(tradePoco.AccountName ?? "")}
Symbol: {TelegramHelper.EscapeSpecialCharacters(tradePoco.SymbolExchange ?? "")}
Quantity: {TelegramHelper.EscapeSpecialCharacters(tradePoco.Quantity.ToString() ?? "")}
Price: {TelegramHelper.EscapeSpecialCharacters(tradePoco.Price.ToString() ?? "")}
";
        }
        private static List<InlineKeyboardButton> CreateBackButtonRow(string backData)
        {
            return new()
                {
                    new("🔙") {CallbackData =  new CallbackQueryCallbackData("trade/back", backData).ToString()},
                };
        }

        private async Task<Telegram.BotAPI.AvailableTypes.Message> MoveMessageToBottomIfNecessary(CallbackQuery update, TelegramTradePoco tradePoco)
        {
            if (!SWTelegramBot.IsLastUsedControllerForChat(update.Message.Chat.Id, this))
            {
                //await TryDeleteMessageAndRemoveFromDictionary(update.Message.Chat.Id, update.Message.MessageId);
                //await Client.DeleteMessageAsync(update.Message.Chat.Id, update.Message.MessageId);
                SendMessageArgs args = new(update.Message.Chat.Id, TelegramHelper.EscapeSpecialCharacters(update.Message.Text));
                args.ParseMode = ParseMode.MarkdownV2;
                args.ReplyMarkup = update.Message.ReplyMarkup;
                var message = await Client.SendMessageAsync(args);
                tradePoco.MainMessageId = message.MessageId;
                try
                {
                    await Client.DeleteMessageAsync(update.Message.Chat.Id, update.Message.MessageId);
                }
                catch (Exception ex)
                {
                    //Log
                }
                if (_chatIdToMessageIdToTradePoco.TryGetValue(update.Message.Chat.Id, out var messageToTradePoco))
                {
                    if (messageToTradePoco.TryRemove(update.Message.MessageId, out var poco))
                        messageToTradePoco.TryAdd(message.MessageId, poco);
                }
                return message;
            }
            return update.Message;
        }
        private async Task UpdateMessage(Telegram.BotAPI.AvailableTypes.Message message, string? messageText, InlineKeyboardMarkup inlineButtonsMarkup)
        {
            if (string.IsNullOrWhiteSpace(messageText))
            {
                EditMessageReplyMarkup replyMarkup = new EditMessageReplyMarkup()
                {
                    ChatId = message.Chat.Id,
                    MessageId = message.MessageId,
                    ReplyMarkup = inlineButtonsMarkup
                };
                await Client.EditMessageReplyMarkupAsync(replyMarkup);
            }
            else
                {
                    EditMessageTextArgs textArgs = new(messageText)
                    {
                        ChatId = message.Chat.Id,
                        MessageId = message.MessageId,
                        ReplyMarkup = inlineButtonsMarkup,
                        ParseMode = ParseMode.MarkdownV2,
                    };

                    await Client.EditMessageTextAsync(textArgs);
                }
        }
        private void UpdateLastUsedTradePocoPerChatId(long chatId, TelegramTradePoco tradePoco)
        {
            if (_chatIdToLastUsedTradePoco.TryGetValue(chatId, out var oldPoco))
                _chatIdToLastUsedTradePoco.TryUpdate(chatId, tradePoco, oldPoco);
            else
                _chatIdToLastUsedTradePoco.TryAdd(chatId, tradePoco);
        }
        private async Task DeletePromptMessage(long chatId, TelegramTradePoco tradePoco)
        {
            if (tradePoco.QtyPricePromptMessageId != null)
            {
                try
                {
                    await Client.DeleteMessageAsync(chatId, tradePoco.QtyPricePromptMessageId.Value);
                }
                catch (Exception ex)
                {
                    //Log message
                }
                tradePoco.QtyPricePromptMessageId = null;
            }
        }
    }

    public class AccountsTelegramController : BaseTelegramController
    {
        public AccountsTelegramController(BotClient client) : base(client)
        {
        }

        public override Task HandleCallbackQuery(CallbackQuery update, string path, CallbackQueryCallbackData data)
        {
            throw new NotImplementedException();
        }

        public override async Task<bool> HandleMessage(Telegram.BotAPI.AvailableTypes.Message update)
        {
            return false;
            //Log message here
        }

        public override async Task RunInitCommand(Chat chat)
        {
            await Client.SendMessageAsync(chat.Id, "Accounts coming soon...");
        }

        public override async Task CleanUp(long chatId)
        {
        }
    }

    public enum SettingsButtons
    {
        ShowPositionsActions,
        AutoPing,
        ShowUPnL,
        ShowNAV,
        ShowClosestToLiquidation
    }
    public class SettingsTelegramController : BaseTelegramController
    {
        public SettingsTelegramController(BotClient client) : base(client) { }

        public override async Task HandleCallbackQuery(CallbackQuery update, string path, CallbackQueryCallbackData data)
        {
            if (update?.Message?.Chat?.Id == null)
                return;
            if (!SWTelegramBot.TelegramSettingsPerChat.TryGetValue(update.Message.Chat.Id, out var settings))
            {
                try
                {
                    await Client.DeleteMessageAsync(update.Message.Chat.Id, update.Message.MessageId);
                }
                catch (Exception ex)
                {

                }
                await RunInitCommand(update.Message.Chat);
                return;
            }

            if (!Enum.TryParse(data.Data, out SettingsButtons button))
                return;

            switch (button)
            {
                case SettingsButtons.ShowPositionsActions:
                    settings.Pos_ShowActionButtonsPerPosition = !settings.Pos_ShowActionButtonsPerPosition;
                    break;
                case SettingsButtons.AutoPing:
                    settings.Ping_AutoPing = !settings.Ping_AutoPing;
                    break;
                case SettingsButtons.ShowUPnL:
                    SetPingMetricFromClick(settings, PingMetric.UPnL);
                    break;
                case SettingsButtons.ShowNAV:
                    SetPingMetricFromClick(settings, PingMetric.NAV);
                    break;
                case SettingsButtons.ShowClosestToLiquidation:
                    SetPingMetricFromClick(settings, PingMetric.ClosestToLiq);
                    break;
                default:
                    break;
            }

            InlineKeyboardMarkup inlineButtonsMarkup = BuildSettingsButtons(settings);
            var editMessageArgs = new EditMessageReplyMarkup()
            {
                ChatId = update.Message.Chat.Id,
                MessageId = update.Message.MessageId,
                ReplyMarkup = inlineButtonsMarkup,
            };
            await Client.EditMessageReplyMarkupAsync(editMessageArgs);
        }

        private static void SetPingMetricFromClick(TelegramSettings settings, PingMetric pingMetricClicked)
        {
            if (settings.Ping_MetricToShow == pingMetricClicked)
                settings.Ping_MetricToShow = PingMetric.None;
            else
                settings.Ping_MetricToShow = pingMetricClicked;
        }

        public override async Task<bool> HandleMessage(Telegram.BotAPI.AvailableTypes.Message update)
        {
            return false;
            //Log message here
        }

        public override async Task RunInitCommand(Chat chat)
        {
            var settings = SWTelegramBot.TelegramSettingsPerChat.GetOrAdd(chat.Id, new TelegramSettings());
            try
            {
                if (settings.SettingsMessageId.HasValue)
                {
                    await Client.DeleteMessageAsync(chat.Id, settings.SettingsMessageId.Value);
                    settings.SettingsMessageId = null;
                }
            }
            catch (Exception ex)
            {
                //Log here
            }
            string messageText =
@"⚙ *Settings*

Use the bottons below to toggle your settings";

            InlineKeyboardMarkup inlineButtonsMarkup = BuildSettingsButtons(settings);
            SendMessageArgs args = new(chat.Id, messageText);
            args.ParseMode = ParseMode.MarkdownV2;
            args.ReplyMarkup = inlineButtonsMarkup;
            var message = await Client.SendMessageAsync(args);
            settings.SettingsMessageId = message.MessageId;
        }

        public override async Task CleanUp(long chatId)
        {
        }
        public static async Task UpdateSettingsMessage(BotClient client, long chatId, TelegramSettings settings)
        {
            if (!settings.SettingsMessageId.HasValue)
                return;

            InlineKeyboardMarkup inlineButtonsMarkup = BuildSettingsButtons(settings);
            var editMessageArgs = new EditMessageReplyMarkup()
            {
                ChatId = chatId,
                MessageId = settings.SettingsMessageId.Value,
                ReplyMarkup = inlineButtonsMarkup,
            };
            await client.EditMessageReplyMarkupAsync(editMessageArgs);
        }
        private static InlineKeyboardMarkup BuildSettingsButtons(TelegramSettings settings)
        {
            List<InlineKeyboardButton> row1 = new()
            {
                BuildSettingsButton(settings, SettingsButtons.ShowPositionsActions)
            };

            List<InlineKeyboardButton> row2 = new()
            {
                BuildSettingsButton(settings, SettingsButtons.AutoPing)
            };
            List<InlineKeyboardButton> row3 = new()
            {
                BuildSettingsButton(settings, SettingsButtons.ShowUPnL),
                BuildSettingsButton(settings, SettingsButtons.ShowNAV),
                BuildSettingsButton(settings, SettingsButtons.ShowClosestToLiquidation),
            };

            List<List<InlineKeyboardButton>> inlineButtons = new()
            {
                row1,
                row2,
                row3,
                HomeButtonRow,
            };

            InlineKeyboardMarkup inlineButtonsMarkup = new InlineKeyboardMarkup(inlineButtons.ToArray());
            return inlineButtonsMarkup;
        }

        private static InlineKeyboardButton BuildSettingsButton(TelegramSettings settings, SettingsButtons buttonToBuild)
        {
            InlineKeyboardButton? button = null;
            switch (buttonToBuild)
            {
                case SettingsButtons.ShowPositionsActions:
                    var actionsEmoji = TelegramHelper.GetBoolEmoji(settings.Pos_ShowActionButtonsPerPosition);
                    button = new InlineKeyboardButton($"Show Actions {actionsEmoji}") { CallbackData = new CallbackQueryCallbackData("settings", SettingsButtons.ShowPositionsActions.ToString()).ToString() };
                    break;
                case SettingsButtons.AutoPing:
                    var pingAutoEmojie = TelegramHelper.GetBoolEmoji(settings.Ping_AutoPing);
                    button = new InlineKeyboardButton($"Auto Ping {pingAutoEmojie}") { CallbackData = new CallbackQueryCallbackData("settings", SettingsButtons.AutoPing.ToString()).ToString() };
                    break;
                case SettingsButtons.ShowUPnL:
                    var pingShowUPnLEmojie = TelegramHelper.GetBoolEmoji(settings.Ping_MetricToShow == PingMetric.UPnL);
                    button = new InlineKeyboardButton($"Show UPnL {pingShowUPnLEmojie}") { CallbackData = new CallbackQueryCallbackData("settings", SettingsButtons.ShowUPnL.ToString()).ToString() };
                    break;
                case SettingsButtons.ShowNAV:
                    var pingShowNAVEmojie = TelegramHelper.GetBoolEmoji(settings.Ping_MetricToShow == PingMetric.NAV);
                    button = new InlineKeyboardButton($"Show NAV {pingShowNAVEmojie}") { CallbackData = new CallbackQueryCallbackData("settings", SettingsButtons.ShowNAV.ToString()).ToString() };
                    break;
                case SettingsButtons.ShowClosestToLiquidation:
                    var pingShowCLosestToLiqEmojie = TelegramHelper.GetBoolEmoji(settings.Ping_MetricToShow == PingMetric.ClosestToLiq);
                    button = new InlineKeyboardButton($"Show Closest Liq% {pingShowCLosestToLiqEmojie}") { CallbackData = new CallbackQueryCallbackData("settings", SettingsButtons.ShowClosestToLiquidation.ToString()).ToString() };
                    break;
                default:
                    break;
            }
            return button;
        }
    }

    #endregion

    #region helper classes
    public enum PingMetric
    {
        None,
        UPnL,
        NAV,
        ClosestToLiq
    }
    public class TelegramSettings
    {
        public int? SettingsMessageId { get; set; }
        #region Pings
        public bool Ping_AutoPing { get; set; }
        public PingMetric Ping_MetricToShow { get; set; }
        public int? PingMessageId { get; set; }
        #endregion
        #region Positions
        public bool Pos_ShowActionButtonsPerPosition { get; set; }
        #endregion
    }

    public class MessageEventArgs : EventArgs
    {
        public string Type { get; init; }
        public string Message { get; init; }
        public string MessageId { get; init; }
        public long? ChatId { get; init; }

        public MessageEventArgs(string type, string message, string messageId, long? chatId = null)
        {
            Type = type;
            Message = message;
            MessageId = messageId;
            ChatId = chatId;
        }
    }
    public class TelegramBotSecrets
    {
        public string BotToken { get; set; }
        public List<long> UserIds { get; set; } = new();
    }

    public static class TelegramHelper
    {
        public static string GetBoolEmoji(bool val)
        {
            if (val)
                return "✅";
            else
                return "🟩";
        }
        public static string EscapeSpecialCharacters(string input)
        {
            return input.Replace("_", @"\_").Replace(".", @"\.").Replace("#", @"\#").Replace("-", @"\-").Replace("(", @"\(").Replace(")", @"\)");
        }
        public static SendMessageArgs BuildSendMessageArgs(long chatId, string messageText, List<List<InlineKeyboardButton>> keyboardButtons)
        {
            InlineKeyboardMarkup inlineButtonsMarkup = new InlineKeyboardMarkup(keyboardButtons.ToArray());
            SendMessageArgs args = new(chatId, EscapeSpecialCharacters(messageText))
            {
                ParseMode = ParseMode.MarkdownV2,
                ReplyMarkup = inlineButtonsMarkup
            };
            return args;
        }
    }

    public class CallbackQueryCallbackData
    {
        public CallbackQueryCallbackData(string data)
        {
            var tmp = data.Split('|');
            if (tmp.Length != 2)
                throw new ArgumentException("Must pass in data as <type>|<data> or use another constructor");

            Path = tmp[0];
            Data = tmp[1];
        }
        public CallbackQueryCallbackData(string path, string data)
        {
            Path = path; 
            Data = data;
        }
        public string Path { get; init; }
        public string Data { get; init; }
        public override string ToString()
        {
            return $"{Path}|{Data}";
        }
    }
    #endregion

    public enum ExchangeName
    {
        ByBIT,
        BitMEX,
        Binance
    }

    public static class TelegramDataProvider
    {
        public static List<TelegramSandwichKey> GetLoadedAccounts()
        {
            return new List<TelegramSandwichKey>()
            { 
                new() {AccountID = "abc123", AccountName = "Strat1_ByBIT", Exchange = ExchangeName.ByBIT },
                new() {AccountID = "abc456", AccountName = "Strat1_BitMEX", Exchange = ExchangeName.BitMEX },
                new() {AccountID = "abc789", AccountName = "Strat1_BINANANCE", Exchange = ExchangeName.Binance },
                new() {AccountID = "xyz123", AccountName = "PA_Trading_Bybit", Exchange = ExchangeName.ByBIT },
                new() {AccountID = "xyz456", AccountName = "PA_Trading_BitMEX", Exchange = ExchangeName.BitMEX },
            };
        }

        public static List<TelegramFavourite> GetFavourites()
        {
            return new List<TelegramFavourite>()
            {
                new() {SymbolSW = "BTCUSDT_Perp", SymbolExchange = "BTCUSDT_Perp", Exchange = ExchangeName.ByBIT },
                new() {SymbolSW = "ETHUSDT_Perp", SymbolExchange = "ETHUSDT_Perp", Exchange = ExchangeName.ByBIT },
                new() {SymbolSW = "SHIBUSDT_Perp", SymbolExchange = "SHIBUSDT_Perp", Exchange = ExchangeName.ByBIT },
                new() {SymbolSW = "BTCUSDT_Perp", SymbolExchange = "BTCUSDT_Perp", Exchange = ExchangeName.BitMEX },
                new() {SymbolSW = "ETHUSDT_Perp", SymbolExchange = "ETHUSDT_Perp", Exchange = ExchangeName.BitMEX },
                new() {SymbolSW = "BTCUSD_Perp", SymbolExchange = "XBTUSD", Exchange = ExchangeName.Binance },
                new() {SymbolSW = "ETHUSDT_Perp", SymbolExchange = "ETHUSD", Exchange = ExchangeName.Binance },
                new() {SymbolSW = "AAVEUSDC_Coin", SymbolExchange = "AAVEUSDC", Exchange = ExchangeName.Binance },
                new() {SymbolSW = "PEPE1000USD", SymbolExchange = "PEPE1000USD", Exchange = ExchangeName.Binance },
            };
        }

        internal static string? GetAccountName(string accountId)
        {
            return GetLoadedAccounts().Single(x => x.AccountID == accountId).AccountName;
        }

        internal static string? GetSymbolExchange(string accountId, string symbolSW)
        {
            var exchange = GetLoadedAccounts().Single(x => x.AccountID == accountId).Exchange;
            return GetFavourites().Single(x => x.Exchange == exchange && x.SymbolSW == symbolSW).SymbolExchange;
        }
    }

    public class TelegramSandwichKey
    {
        public string AccountID { get; set; }
        public string AccountName { get; set; }
        public ExchangeName Exchange { get; set; }
    }

    public class TelegramFavourite
    {
        public ExchangeName Exchange { get; set; }
        public string SymbolSW { get; set; }
        public string SymbolExchange { get; set; }
    }
}
