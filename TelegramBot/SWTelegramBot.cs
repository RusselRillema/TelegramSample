using Microsoft.VisualBasic;
using Sandwich;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using System.Windows.Input;
using Telegram.BotAPI;
using Telegram.BotAPI.AvailableMethods;
using Telegram.BotAPI.AvailableMethods.FormattingOptions;
using Telegram.BotAPI.AvailableTypes;
using Telegram.BotAPI.GettingUpdates;
using Telegram.BotAPI.UpdatingMessages;
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
            Configure();
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

                            if (await RunCommandMessage(update.Message.Chat, update.Message.Text))
                                break;
                            if (!_lastUsedControllerPerChat.TryGetValue(update.Message.Chat.Id, out controller) || _lastUsedControllerPerChat == null)
                                await _botClient.DeleteMessageAsync(update.Message.Chat.Id, update.Message.MessageId);
                            else
                                if (!await controller.HandleMessage(update.Message))
                                    await _botClient.DeleteMessageAsync(update.Message.Chat.Id, update.Message.MessageId);
                            break;
                        case UpdateType.CallbackQuery:
                            if (update?.CallbackQuery?.Data == null || update?.CallbackQuery?.Message?.Chat == null)
                                break;

                            if (await RunCommandMessage(update.CallbackQuery.Message.Chat, update.CallbackQuery.Data))
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

        private async Task<bool> RunCommandMessage(Chat chat, string command)
        {
            command = command.ToLower();
            if (command == "/start")
                command = "/home";
            if (command.StartsWith("/"))
                command = command.Remove(0, 1);

            if (!command.EndsWith("telegramcontroller"))
                command += "telegramcontroller";
            if (_controllersByType.TryGetValue(command, out BaseTelegramController controller))
            {
                UpdateLastUsedController(chat, controller);
                try
                {
                    await controller.RunInitCommand(chat);
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
    }


    #region Controllers
    public abstract class BaseTelegramController : IComparable<BaseTelegramController>
    {
        protected BotClient Client;
        protected static InlineKeyboardButton HomeButton = new InlineKeyboardButton("🏡 Home") { CallbackData = "/home" };
        protected static List<InlineKeyboardButton> HomeButtonRow = new() { HomeButton };
        protected InlineKeyboardButton TradeButton = new InlineKeyboardButton("💸 Trade") { CallbackData = "/trade" };
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
            InlineKeyboardButton settingsButton = new InlineKeyboardButton("⚙ Settings")
            {
                CallbackData = "/settings"
            };
            List<InlineKeyboardButton> row3 = new()
            {
                accountsButton, settingsButton
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
    }

    public class BalancesTelegramController : BaseTelegramController
    {
        ConcurrentDictionary<long, ConcurrentDictionary<string, int>> _chatIdToViewByToMessageId = new();

        #region buttons
        private const string _coinCallBackData = "coin";
        private const string _exchangeCallBackData = "exchange";
        private const string _accountCallBackData = "account";
        private InlineKeyboardButton _byCoinButton = new InlineKeyboardButton("💰 Coin") { CallbackData = new CallbackQueryCallbackData("balances", _coinCallBackData).ToString() };
        private InlineKeyboardButton _byExchangeButton = new InlineKeyboardButton("💰 Exchange") { CallbackData = new CallbackQueryCallbackData("balances", _exchangeCallBackData).ToString() };
        private InlineKeyboardButton _byAccountButton = new InlineKeyboardButton("💰 Account") { CallbackData = new CallbackQueryCallbackData("balances", _accountCallBackData).ToString() };
        #endregion

        public BalancesTelegramController(BotClient client) : base(client)
        {
        }

        public override async Task HandleCallbackQuery(CallbackQuery update, string path, CallbackQueryCallbackData data)
        {
            if (update.Message?.Chat == null)
                return;
            try
            {
                if (_chatIdToViewByToMessageId.TryGetValue(update.Message.Chat.Id, out var viewByToMessageId_int))
                {
                    if (viewByToMessageId_int.TryRemove(data.Data, out var messageId))
                        await Client.DeleteMessageAsync(update.Message.Chat.Id, messageId);
                }
            }
            catch (Exception ex)
            {
                //Log here
            }
            string messageText = "";
            List <InlineKeyboardButton> row1 = new();
            switch (data.Data)
            {
                case _coinCallBackData:
                    row1.Add(_byExchangeButton);
                    row1.Add(_byAccountButton);
                    messageText = await GetBalanceByCoinMessageText();
                    break;
                case _exchangeCallBackData:
                    row1.Add(_byCoinButton);
                    row1.Add(_byAccountButton);
                    messageText = await GetBalanceByExchangeMessageText();
                    break;
                case _accountCallBackData:
                    row1.Add(_byCoinButton);
                    row1.Add(_byExchangeButton);
                    messageText = await GetBalanceByAccountMessageText();
                    break;
                default:
                    break;
            }

            List<List<InlineKeyboardButton>> inlineButtons = new()
            {
                row1,
                HomeButtonRow,
            };

            InlineKeyboardMarkup inlineButtonsMarkup = new InlineKeyboardMarkup(inlineButtons.ToArray());
            SendMessageArgs args = new(update.Message.Chat.Id, messageText);
            args.ParseMode = ParseMode.MarkdownV2;
            args.ReplyMarkup = inlineButtonsMarkup;
            var message = await Client.SendMessageAsync(args);
            var viewByToMessageId = _chatIdToViewByToMessageId.GetOrAdd(update.Message.Chat.Id, new ConcurrentDictionary<string, int>());
            viewByToMessageId.TryAdd(data.Data, message.MessageId);
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

View breakdown by:";
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

View breakdown by:";
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

View breakdown by:";
        }

        public override async Task<bool> HandleMessage(Telegram.BotAPI.AvailableTypes.Message update)
        {
            return false;
            //Log message here
        }

        public override async Task RunInitCommand(Chat chat)
        {
            string messageText =
@"*Balances*
`
Total:        $4,348,152
Top Exchange: Binance
Top Coin:     BTC`

View breakdown by:";


            List<InlineKeyboardButton> row1 = new()
            {
                _byCoinButton, _byExchangeButton, _byAccountButton
            };


            List<InlineKeyboardButton> row2 = new()
            {
                HomeButton
            };

            List<List<InlineKeyboardButton>> inlineButtons = new()
            {
                row1,
                row2,
            };

            InlineKeyboardMarkup inlineButtonsMarkup = new InlineKeyboardMarkup(inlineButtons.ToArray());
            SendMessageArgs args = new(chat.Id, messageText);
            args.ParseMode = ParseMode.MarkdownV2;
            args.ReplyMarkup = inlineButtonsMarkup;
            var message = await Client.SendMessageAsync(args);
        }
    }

    public class PositionsTelegramController : BaseTelegramController
    {
        ConcurrentDictionary<long, ConcurrentDictionary<string, int>> _chatIdToViewByToMessageId = new();

        #region buttons
        private const string _coinCallBackData = "coin";
        private const string _exchangeCallBackData = "exchange";
        private const string _accountCallBackData = "account";
        private InlineKeyboardButton _byCoinButton = new InlineKeyboardButton("💰 Coin") { CallbackData = new CallbackQueryCallbackData("balances", _coinCallBackData).ToString() };
        private InlineKeyboardButton _byExchangeButton = new InlineKeyboardButton("💰 Exchange") { CallbackData = new CallbackQueryCallbackData("balances", _exchangeCallBackData).ToString() };
        private InlineKeyboardButton _byAccountButton = new InlineKeyboardButton("💰 Account") { CallbackData = new CallbackQueryCallbackData("balances", _accountCallBackData).ToString() };
        #endregion

        public PositionsTelegramController(BotClient client) : base(client) {}

        public override Task HandleCallbackQuery(CallbackQuery update, string path, CallbackQueryCallbackData data)
        {
            throw new NotImplementedException();
        }

        public override async Task<bool> HandleMessage(Telegram.BotAPI.AvailableTypes.Message update)
        {
            return false;
            //Log message here
        }

        public override Task RunInitCommand(Chat chat)
        {
            throw new NotImplementedException();
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

        public override async Task HandleCallbackQuery(CallbackQuery update, string path, CallbackQueryCallbackData data)
        {
            if (update?.Message?.Chat?.Id == null)
                return;


            if (!_chatIdToMessageIdToTradePoco.TryGetValue(update.Message.Chat.Id, out ConcurrentDictionary<int, TelegramTradePoco> chatTrades))
                return;

            if (!chatTrades.TryGetValue(update.Message.MessageId, out TelegramTradePoco tradePoco))
                return;

            var message = await MoveMessageToBottomIfNecessary(update);

            UpdateLastUsedTradePocoPerChatId(message.Chat.Id, tradePoco);

            await DeletePromptMessage(message.Chat.Id, tradePoco);

            if (path == "trade/inst")
            {
                TradeInstrumentSelectionCallBackData callbackData = new(data.Data);
                tradePoco.AccountId = callbackData.AccountId;
                tradePoco.AccountName = TelegramDataProvider.GetAccountName(tradePoco.AccountId);
                tradePoco.SymbolSW = callbackData.SymbolSW;
                tradePoco.SymbolExchange = TelegramDataProvider.GetSymbolExchange(tradePoco.AccountId, tradePoco.SymbolSW);

                string messageText = GetBaseTelegramTradeMessageText(tradePoco);
                messageText += $"{Environment.NewLine}Select Quantity:";
                InlineKeyboardMarkup inlineButtonsMarkup = CreateQuantityInputButtons();

                await UpdateMessage(message, messageText, inlineButtonsMarkup);

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

                string messageText = GetBaseTelegramTradeMessageText(tradePoco);
                messageText += $"{Environment.NewLine}Select Price:";
                InlineKeyboardMarkup inlineButtonsMarkup = GetPriceInputButtons();

                //EditMessageTextArgs textArgs = CreateEnterPriceEditMessage(update.Message.Chat.Id, tradePoco.MainMessageId, tradePoco);
                //await Client.EditMessageTextAsync(textArgs);

                await UpdateMessage(message, messageText, inlineButtonsMarkup);

                return;
            }

            if (path == "trade/price")
            {
                await Client.SendMessageAsync(update.Message.Chat.Id, "Not implemented yet");
            }
        }

        private async Task<Telegram.BotAPI.AvailableTypes.Message> MoveMessageToBottomIfNecessary(CallbackQuery update)
        {
            if (!SWTelegramBot.IsLastUsedControllerForChat(update.Message.Chat.Id, this))
            {
                //await TryDeleteMessageAndRemoveFromDictionary(update.Message.Chat.Id, update.Message.MessageId);
                //await Client.DeleteMessageAsync(update.Message.Chat.Id, update.Message.MessageId);
                SendMessageArgs args = new(update.Message.Chat.Id, TelegramTextHelper.EscapeSpecialCharacters(update.Message.Text));
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
                if (_chatIdToMessageIdToTradePoco.TryGetValue(update.Message.Chat.Id, out var messageToTradePoco))
                {
                    if (messageToTradePoco.TryRemove(update.Message.MessageId, out var poco))
                        messageToTradePoco.TryAdd(message.MessageId, poco);
                }
                return message;
            }
            return update.Message;
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
                    HomeButtonRow
                };


            InlineKeyboardMarkup inlineButtonsMarkup = new InlineKeyboardMarkup(inlineButtons.ToArray());
            return inlineButtonsMarkup;
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

                string messageText = GetBaseTelegramTradeMessageText(tradePoco);
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
                string messageText = GetBaseTelegramTradeMessageText(tradePoco);
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
                    HomeButtonRow
                };


            InlineKeyboardMarkup inlineButtonsMarkup = new InlineKeyboardMarkup(inlineButtons.ToArray());
            return inlineButtonsMarkup;
        }

        private void UpdateLastUsedTradePocoPerChatId(long chatId, TelegramTradePoco tradePoco)
        {
            if (_chatIdToLastUsedTradePoco.TryGetValue(chatId, out var oldPoco))
                _chatIdToLastUsedTradePoco.TryUpdate(chatId, tradePoco, oldPoco);
            else
                _chatIdToLastUsedTradePoco.TryAdd(chatId, tradePoco);
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

            string messageText = GetBaseTelegramTradeMessageText(tradePoco);
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


            InlineKeyboardMarkup inlineButtonsMarkup = new InlineKeyboardMarkup(inlineButtons.ToArray());
            SendMessageArgs args = new(chat.Id, messageText);
            args.ParseMode = ParseMode.MarkdownV2;
            args.ReplyMarkup = inlineButtonsMarkup;
            var message = await Client.SendMessageAsync(args);
            trades.TryAdd(message.MessageId, tradePoco);
            tradePoco.MainMessageId = message.MessageId;
            UpdateLastUsedTradePocoPerChatId(chat.Id, tradePoco);
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

        private string GetBaseTelegramTradeMessageText(TelegramTradePoco tradePoco)
        {
            return
$@"*Trade*

Use the buttons below to setup your order\. We'll show you the final summary before placing the order\.

Account: {TelegramTextHelper.EscapeSpecialCharacters(tradePoco.AccountName ?? "")}
Symbol: {TelegramTextHelper.EscapeSpecialCharacters(tradePoco.SymbolExchange ?? "")}
Quantity: {TelegramTextHelper.EscapeSpecialCharacters(tradePoco.Quantity.ToString() ?? "")}
Price: {TelegramTextHelper.EscapeSpecialCharacters(tradePoco.Price.ToString() ?? "")}
";
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
    }

    public class SettingsTelegramController : BaseTelegramController
    {
        ConcurrentDictionary<long, int> _chatIdToSettingsMessageId = new();

        #region buttons
        private const string _posShowActionsCallBackData = "posShowActions";
        private const string _posSummaryViewCallBackData = "posSummaryView";
        #endregion

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

            switch (data.Data)
            {
                case _posShowActionsCallBackData:
                    settings.Pos_ShowActionButtonsPerPosition = !settings.Pos_ShowActionButtonsPerPosition;
                    break;
                case _posSummaryViewCallBackData:
                    settings.Pos_SummaryView = !settings.Pos_SummaryView;
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

        public override async Task<bool> HandleMessage(Telegram.BotAPI.AvailableTypes.Message update)
        {
            return false;
            //Log message here
        }

        public override async Task RunInitCommand(Chat chat)
        {
            try
            {
                if (_chatIdToSettingsMessageId.TryRemove(chat.Id, out int messageId))
                    await Client.DeleteMessageAsync(chat.Id, messageId);
            }
            catch (Exception ex)
            {
                //Log here
            }
            string messageText =
@"⚙ *Settings*

Use the bottons below to toggle your settings";

            var settings = SWTelegramBot.TelegramSettingsPerChat.GetOrAdd(chat.Id, new TelegramSettings());
            InlineKeyboardMarkup inlineButtonsMarkup = BuildSettingsButtons(settings);
            SendMessageArgs args = new(chat.Id, messageText);
            args.ParseMode = ParseMode.MarkdownV2;
            args.ReplyMarkup = inlineButtonsMarkup;
            var message = await Client.SendMessageAsync(args);
            _chatIdToSettingsMessageId.TryAdd(message.Chat.Id, message.MessageId);
        }

        private InlineKeyboardMarkup BuildSettingsButtons(TelegramSettings settings)
        {
            var actionsEmoji = TelegramTextHelper.GetBoolEmoji(settings.Pos_ShowActionButtonsPerPosition);
            var summaryEmoji = TelegramTextHelper.GetBoolEmoji(settings.Pos_SummaryView);
            var posShowActions = new InlineKeyboardButton($"Show Actions {actionsEmoji}") { CallbackData = new CallbackQueryCallbackData("settings", _posShowActionsCallBackData).ToString() };
            var posSummaryView = new InlineKeyboardButton($"Summary View {summaryEmoji}") { CallbackData = new CallbackQueryCallbackData("settings", _posSummaryViewCallBackData).ToString() };

            List<InlineKeyboardButton> row1 = new()
            {
                posShowActions, posSummaryView
            };

            List<List<InlineKeyboardButton>> inlineButtons = new()
            {
                row1,
                HomeButtonRow,
            };

            InlineKeyboardMarkup inlineButtonsMarkup = new InlineKeyboardMarkup(inlineButtons.ToArray());
            return inlineButtonsMarkup;
        }
    }

    #endregion

    #region helper classes
    public class TelegramSettings
    {
        #region positions
        public bool Pos_ShowActionButtonsPerPosition { get; set; }
        public bool Pos_SummaryView { get; set; }
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

    public static class TelegramTextHelper
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
            return input.Replace("_", @"\_").Replace(".", @"\.");
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
