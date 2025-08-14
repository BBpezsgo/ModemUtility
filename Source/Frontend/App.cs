using System.Globalization;
using ModemUtility.Frontend.Interface;
using ModemUtility.Modem;

namespace ModemUtility.Frontend;

class App : IDisposable
{
    readonly ATModem? at;

    const string CachePath = "/home/BB/Projects/ModemUtility/cache";

    readonly List<Storage<Contact>> Contacts = [];
    readonly List<Contact> ImplicitContacts = [];
    readonly List<Storage<Message>> Messages = [];

    readonly Lock FrontendLock = new();

    bool IsDirty;
    bool Run;
    string? ReadStatus;

    public App()
    {
        if (Priviliges.IsCurrentProcessElevated())
        {
            at = new("/dev/ttyUSB0", 115200);

            at.SetVerbose(true);
            at.SetEcho(true);
            at.SuppressResultCodes(false);
            at.MobileEquipmentReporting = 2;
        }

        IsDirty = true;
        Run = true;
    }

    public void Start()
    {
        CancellationTokenSource cts = new();
        ReadMessagesLocal();
        ReadContactsLocal();
        Task task = Task.Run(() =>
        {
            ReadMessages(cts.Token);
            ReadContacts(cts.Token);
        });

        Container rootElement;
        Label contactList;
        Label messageList;
        Label statusLabel;

        {
            rootElement = new(ContainerDirection.Vertical);
            statusLabel = rootElement.Add(new Border<Label>(new Label(), new Sides(0, 1), 0)).Child;
            Container e = rootElement.Add(new Container(ContainerDirection.Horizontal));
            contactList = e.Add(new Border<Label>(new Label(), new Sides(0, 1), new Sides(1, 1))).Child;
            messageList = e.Add(new Border<Label>(new Label(), new Sides(0, 1), new Sides(1, 1))).Child;
        }

        while (Run)
        {
            Wait(out ConsoleKeyInfo key);

            contactList.Clear();
            messageList.Clear();

            statusLabel.Clear();
            statusLabel.WriteLine(ReadStatus);

            using (FrontendLock.EnterScope())
            {
                foreach (Storage<Contact> storage in Contacts)
                {
                    foreach (Contact contact in storage)
                    {
                        contactList.WriteLine($"{contact.Name} {contact.Address}");
                    }
                }
                foreach (Contact contact in ImplicitContacts)
                {
                    contactList.WriteLine($"{contact.Address}");
                }

                foreach (Storage<Message> storage in Messages)
                {
                    foreach (Message message in storage)
                    {
                        messageList.WriteLine($"{message.Source?.Name ?? message.Destination?.Name} {message.Text}");
                    }
                }
            }

            rootElement.MaxWidth = Console.WindowWidth;
            rootElement.MaxHeight = Math.Max(0, Console.WindowHeight - 1);

            rootElement.RecalculateLayout();

            Console.Clear();
            InterfaceRenderer.Render(rootElement);
        }

        cts.Cancel();
        task.Wait();
    }

    Contact GetOrCreateContact(string address)
    {
        foreach (Storage<Contact> a in Contacts)
        {
            foreach (Contact contact in a)
            {
                if (contact.Address == address) return contact;
            }
        }

        foreach (Contact contact in ImplicitContacts)
        {
            if (contact.Address == address) return contact;
        }

        Contact result = new(-1, address, address);
        ImplicitContacts.Add(result);
        IsDirty = true;
        return result;
    }

    void OnMessage(Storage<Message> storageFrontend, int index, SmsMessage message)
    {
        if (storageFrontend.Any(v => v.Index == index)) return;

        Contact? source = message.Sender is null ? null : GetOrCreateContact(message.Sender);
        Contact? destination = message.Destination is null ? null : GetOrCreateContact(message.Destination);

        if (message.Concat is not null)
        {
            Message? messageFrontend = storageFrontend.FirstOrDefault(v => v.Reference.HasValue && v.Reference.Value == message.Concat.Reference);
            messageFrontend ??= new Message(index, source, destination, message.Concat.Reference);
            messageFrontend.InsertPart(message.Concat.Sequence, message.Text ?? string.Empty);
        }
        else
        {
            Message messageFrontend = new(index, source, destination, message.Text ?? string.Empty);
            storageFrontend.Add(messageFrontend);
        }
        IsDirty = true;
    }

    void ReadMessagesLocal()
    {
        using (FrontendLock.EnterScope())
        {
            string[] storages = ["ME", "SM_P"];
            foreach (string storage in storages)
            {
                Storage<Message> storageFrontend = new(storage, 0, 0);
                Messages.Add(storageFrontend);

                if (File.Exists(Path.Combine(CachePath, "messages", storage)))
                {
                    foreach (string[]? line in File.ReadAllLines(Path.Combine(CachePath, "messages", storage)).Select(v => v.Split(' ')))
                    {
                        OnMessage(storageFrontend, int.Parse(line[0], CultureInfo.InvariantCulture), PduDecoder.Decode(Convert.FromHexString(line[2])));
                    }
                }
            }
        }
    }

    void ReadContactsLocal()
    {
        // TODO
    }

    void ReadMessages(CancellationToken ct)
    {
        if (at is null) return;

        string[] storages = ["ME", "SM_P"];
        foreach (string storage in storages)
        {
            MessageStorage storageInfo = at.SelectMessageStorage(storage);
            Storage<Message>? storageFrontend = null;

            ReadStatus = $"Reading messages from {storage} ...";

            using (FrontendLock.EnterScope())
            {
                foreach (Storage<Message> item in Messages)
                {
                    if (item.Name != storageInfo.Name) continue;
                    item.Used = storageInfo.Used;
                    item.Total = storageInfo.Total;
                    IsDirty = true;
                    break;
                }

                if (storageFrontend is null)
                {
                    storageFrontend = new Storage<Message>(storageInfo.Name, storageInfo.Used, storageInfo.Total);
                    Messages.Add(storageFrontend);
                }
            }

            at.MessagePresentationFormat = 0;
            for (int i = 0; i <= storageInfo.Total; i++)
            {
                if (ct.IsCancellationRequested) break;

                ReadStatus = $"Reading messages from {storage} ... ({i} / {storageInfo.Total})";

                (MessageStatus Status, int Length, string PDU)? raw = at.GetMessagePdu(i);
                if (!raw.HasValue) continue;
                OnMessage(storageFrontend, i, PduDecoder.Decode(Convert.FromHexString(raw.Value.PDU)));
                Directory.CreateDirectory(Path.Combine(CachePath, "messages"));
                File.AppendAllLines(Path.Combine(CachePath, "messages", storageInfo.Name), [$"{i} {(int)raw.Value.Status} {raw.Value.PDU}"]);
            }

            ReadStatus = null;
        }

        IsDirty = true;
    }

    static bool AddressEquals(string a, string b)
    {
        // TODO
        return a.Equals(b, StringComparison.Ordinal);
    }

    void ReadContacts(CancellationToken ct)
    {
        if (at is null) return;

        string[] storages = ["ME"];
        foreach (string storage in storages)
        {
            at.SelectPhonebookStorage(storage);
            PhonebookStorage storageInfo = at.GetCurrentPhonebookStorage();

            ReadStatus = $"Reading contacts from {storageInfo.Name} ...";

            Storage<Contact>? storageFrontend = null;

            using (FrontendLock.EnterScope())
            {
                foreach (Storage<Contact> item in Contacts)
                {
                    if (item.Name != storageInfo.Name) continue;
                    item.Used = storageInfo.Used;
                    item.Total = storageInfo.Total;
                    break;
                }
                if (storageFrontend is null)
                {
                    storageFrontend = new Storage<Contact>(storageInfo.Name, storageInfo.Used, storageInfo.Total);
                    Contacts.Add(storageFrontend);
                }
                IsDirty = true;
            }

            void OnContact(Modem.Contact contact)
            {
                if (storageFrontend.Any(v => v.Index == contact.Index)) return;

                foreach (Contact item in ImplicitContacts)
                {
                    if (!AddressEquals(item.Address, contact.Address)) continue;
                    item.Index = contact.Index;
                    item.Name = contact.Name;
                    ImplicitContacts.Remove(item);
                    storageFrontend.Add(item);
                    IsDirty = true;
                    return;
                }

                storageFrontend.Add(new Contact(contact.Index, contact.Address, contact.Name));
                IsDirty = true;
            }

            for (int i = 1; i <= storageInfo.Total; i++)
            {
                if (ct.IsCancellationRequested) break;

                ReadStatus = $"Reading contacts from {storageInfo.Name} ... ({i} / {storageInfo.Total})";

                Modem.Contact? raw = at.GetContact(i);
                if (!raw.HasValue) continue;
                OnContact(raw.Value);
                Directory.CreateDirectory(Path.Combine(CachePath, "phonebook"));
                File.AppendAllLines(Path.Combine(CachePath, "phonebook", storageInfo.Name), [$"{raw.Value.Index} {raw.Value.Type} {raw.Value.Address} {raw.Value.Name.Replace('\r', ' ').Replace('\n', ' ')}"]);
            }

            ReadStatus = null;
        }

        IsDirty = true;
    }

    void Wait(out ConsoleKeyInfo key)
    {
        key = default;
        while (!IsDirty)
        {
            if (!Console.IsOutputRedirected && Console.KeyAvailable)
            {
                key = Console.ReadKey(true);
                IsDirty = true;
                return;
            }
            Thread.Sleep(50);
        }
        IsDirty = false;
    }

    /*
    void Menu2()
    {
        IsDirty = true;

        while (Run && menu == 2 && selectedStorage is null)
        {
            Wait(out ConsoleKeyInfo key);
            Console.Clear();

            Console.WriteLine($"╭╮ Messages - Loading ... ╭────────");
            Console.WriteLine($"│ ");
            break;
        }

        if (!Run || menu != 2 || selectedStorage is null) return;

        List<(MessageStatus Status, SmsMessage Message, int Index)> messages = new(selectedStorage.Value.Used);
        if (File.Exists(Path.Combine(CachePath, "messages", selectedStorage.Value.Name)))
        {
            foreach (var line in File.ReadAllLines(Path.Combine(CachePath, "messages", selectedStorage.Value.Name)).Select(v => v.Split(' ')))
            {
                messages.Add((
                    (MessageStatus)int.Parse(line[1]),
                    PduDecoder.Decode(line[2]),
                    int.Parse(line[0])
                ));
            }
        }

        int selectedMessage = messages.Count - 1;

        CancellationTokenSource cancel = new();
        Task.Run(() =>
        {
            at.MessagePresentationFormat = 0;
            int lastIndex = -1;
            for (int i = 0; i <= selectedStorage.Value.Total; i++)
            {
                if (cancel.Token.IsCancellationRequested) break;
                if (messages.Any(v => v.Index == i)) continue;
                var raw = at.GetMessagePdu(i);
                if (!raw.HasValue) continue;
                if (selectedMessage == lastIndex) selectedMessage = i;
                lastIndex = i;
                messages.Add((raw.Value.Status, PduDecoder.Decode(raw.Value.PDU), i));
                Directory.CreateDirectory(Path.Combine(CachePath, "messages"));
                File.AppendAllLines(Path.Combine(CachePath, "messages", selectedStorage.Value.Name), [$"{i} {(int)raw.Value.Status} {raw.Value.PDU}"]);
                IsDirty = true;
            }
        }, cancel.Token);

        IsDirty = true;
        while (Run && menu == 2)
        {
            Wait(out ConsoleKeyInfo key);
            Console.Clear();

            Console.WriteLine($"╭╮ Messages - {selectedStorage!.Value.Name} ({selectedStorage.Value.Used}/{selectedStorage.Value.Total}) ╭────────");
            Console.WriteLine($"│ ");

            int k = messages.Index().FirstOrDefault(v => v.Item.Index == selectedMessage).Index;

            int end = Math.Min(messages.Count, k + 5);
            int start = Math.Max(0, k - 5);

            for (int j = start; j < end; j++)
            {
                (MessageStatus Status, SmsMessage Message, int Index) item = messages[j];
                string text;
                if (item.Message.Concat is not null)
                {
                    if (item.Message.Concat.Sequence != 1) continue;
                    var sequence = messages
                        .Where(v => v.Message.Sender == item.Message.Sender && v.Message.Destination == item.Message.Destination && v.Message.Concat is not null && v.Message.Concat.Reference == v.Message.Concat.Reference)
                        .Select(v => (v.Message.Text, v.Message.Concat!.Sequence))
                        .ToArray();
                    Array.Sort(sequence, (a, b) => a.Sequence - b.Sequence);
                    text = string.Join(null, sequence.Select(v => v.Text));
                }
                else
                {
                    text = item.Message.Text ?? string.Empty;
                }
                text = text.Trim();
                int i = text.IndexOfAny(['\r', '\n']);
                if (i != -1) text = text[..i];
                if (text.Length > 30) text = text[..30] + "...";

                Console.Write("│ ");
                if (selectedMessage == item.Index)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                }
                Console.WriteLine($"{item.Status switch
                {
                    MessageStatus.RecRead => "<",
                    MessageStatus.StoUnsent => ">",
                    MessageStatus.StoSent => ">",
                    _ => $"{item.Status}",
                }} {item.Message.Sender ?? item.Message.Destination} :: {text}");
                Console.ResetColor();
            }

            Console.WriteLine($"│ ");
            Console.WriteLine($"│ B - Back");

            if (key.Key == ConsoleKey.B)
            {
                menu = 1;
                selectedStorage = null;
                cancel.Cancel();
            }
            else if (key.Key == ConsoleKey.S)
            {
                var v = messages
                    .Where(v => v.Message.Concat is null || v.Message.Concat.Sequence == 1)
                    .Select(v => v.Index)
                    .Where(v => v > selectedMessage);
                if (v.Any()) selectedMessage = v.Min();
            }
            else if (key.Key == ConsoleKey.W)
            {
                var v = messages
                    .Where(v => v.Message.Concat is null || v.Message.Concat.Sequence == 1)
                    .Select(v => v.Index)
                    .Where(v => v < selectedMessage);
                if (v.Any()) selectedMessage = v.Max();
            }
        }
    }

    void Menu1()
    {
        IsDirty = true;
        while (Run && menu == 1)
        {
            Wait(out ConsoleKeyInfo key);
            Console.Clear();

            Console.WriteLine($"╭╮ Messages ╭────────");
            Console.WriteLine($"│ ");

            for (int i = 0; i < MessageStorages.Length; i++)
            {
                Console.WriteLine($"│ {i} > {MessageStorages[i]}");
            }
            Console.WriteLine($"│ B > Back");

            if (key.Key >= ConsoleKey.D0 && key.Key <= ConsoleKey.D9 &&
                key.Key - ConsoleKey.D0 < MessageStorages.Length)
            {
                Task.Run(() =>
                {
                    selectedStorage = at.SelectMessageStorage(MessageStorages[key.Key - ConsoleKey.D0]);
                    IsDirty = true;
                });
                menu = 2;
            }
            else if (key.Key == ConsoleKey.B)
            {
                menu = 0;
            }
        }
    }

    void Menu0()
    {
        IsDirty = true;
        while (Run && menu == 0)
        {
            Wait(out ConsoleKeyInfo key);

            Console.Clear();

            Console.WriteLine($"╭╮ CAT B25 ╭────────");
            Console.WriteLine($"│ ");
            Console.WriteLine($"│ M > Messages");
            Console.WriteLine($"│ C > Contacts");
            Console.WriteLine($"│ B > Exit");

            if (key.Key == ConsoleKey.M)
            {
                menu = 1;
            }
            else if (key.Key == ConsoleKey.B)
            {
                Run = false;
            }
        }
    }
    */

    public void Dispose() => at?.Dispose();
}
