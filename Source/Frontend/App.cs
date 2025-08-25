using System.Diagnostics;
using System.Globalization;
using System.Text;
using ModemUtility.Frontend.Interface;
using ModemUtility.Modem;
using PhoneNumbers;

namespace ModemUtility.Frontend;

sealed class App : IDisposable
{
    readonly ATModem? at;

    static readonly string CachePath = Path.Combine(Environment.CurrentDirectory, "cache");

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

        Border contactsPanel;
        Border messagesPanel;

        Label contactList;
        Label messageList;

        Label statusLabel;

        Container rootElement = new(ContainerDirection.Vertical)
        {
            MinHeight = Console.WindowHeight - 1,
        };
        statusLabel = rootElement.Add(new Label());
        Container e = rootElement.Add(new Container(ContainerDirection.Horizontal));
        Border<Label> contactsPanel_ = e.Add(new Border<Label>(new Label(), 0, new Sides(1, 1)) { Title = "Contacts" });
        Border<Label> messagesPanel_ = e.Add(new Border<Label>(new Label(), 0, new Sides(1, 1)) { Title = "Messages" });
        contactList = contactsPanel_.Child;
        messageList = messagesPanel_.Child;
        contactsPanel = contactsPanel_;
        messagesPanel = messagesPanel_;

        int selectedContact = 0;
        int selectedMessage = 0;
        int selectedMenu = 0;

        /*
        Table contactsTable = new()
        {
            Border = TableBorder.None,
            ShowHeaders = false,
        };
        contactsTable.AddColumn(string.Empty, v =>
        {
            v.NoWrap();
        });

        Table messagesTable = new()
        {
            Border = TableBorder.None,
            ShowHeaders = false,
        };
        messagesTable.AddColumn(string.Empty, v =>
        {
            v.NoWrap();
        });
        messagesTable.AddColumn(string.Empty);

        Layout layout = new Layout("Root")
            .SplitColumns(
                new Layout("Contacts", new Panel(contactsTable)
                {
                    Header = new PanelHeader("Contacts")
                }.Expand()),
                new Layout("Messages", new Panel(messagesTable)
                {
                    Header = new PanelHeader("Messages")
                }.Expand())
                .Ratio(3));
        */

        while (Run)
        {
            Wait(out ConsoleKeyInfo key);

            /*
            contactsTable.Rows.Clear();
            foreach (Contact v in Contacts.Flat())
            {
                contactsTable.Rows.Add([new Text($"{v.Name} {v.Address}").Ellipsis()]);
            }
            foreach (Contact v in ImplicitContacts)
            {
                contactsTable.Rows.Add([new Text(v.Address.ToString()).Ellipsis()]);
            }

            messagesTable.Rows.Clear();
            foreach (Message v in Messages.Flat())
            {
                Text? contactCell;
                if (v.Source is not null)
                {
                    contactCell = new Text(v.Source.Name ?? v.Source.Address.ToString()).Crop();
                }
                else if (v.Destination is not null)
                {
                    contactCell = new Text(v.Destination.Name ?? v.Destination.Address.ToString()).Crop();
                }
                else
                {
                    throw new UnreachableException();
                }
                messagesTable.Rows.Add([contactCell, new Text(v.Text).Ellipsis()]);
            }
            AnsiConsole.Write(layout);
            */

            contactList.ClearContent();
            messageList.ClearContent();

            statusLabel.ClearContent();
            if (ReadStatus is not null) statusLabel.WriteLine(ReadStatus);

            contactList.FlexBias = Console.WindowWidth / 3;

            using (FrontendLock.EnterScope())
            {
                IEnumerable<Contact> allContacts = Contacts.Flat().Append(ImplicitContacts);
                IEnumerable<Message> allMessages = Messages.Flat().OrderBy(v => v.Time);

                switch (key.Key)
                {
                    case ConsoleKey.LeftArrow:
                        selectedMenu = ((--selectedMenu) + 2) % 2;
                        break;
                    case ConsoleKey.RightArrow:
                        selectedMenu = ((++selectedMenu) + 2) % 2;
                        break;
                    case ConsoleKey.UpArrow:
                        if (selectedMenu == 0)
                        {
                            selectedContact = Math.Clamp(selectedContact - 1, 0, allContacts.Count() - 1);
                        }
                        else if (selectedMenu == 1)
                        {
                            selectedMessage = Math.Clamp(selectedMessage - 1, 0, allMessages.Count() - 1);
                        }
                        break;
                    case ConsoleKey.DownArrow:
                        if (selectedMenu == 0)
                        {
                            selectedContact = Math.Clamp(selectedContact + 1, 0, allContacts.Count() - 1);
                        }
                        else if (selectedMenu == 1)
                        {
                            selectedMessage = Math.Clamp(selectedMessage + 1, 0, allMessages.Count() - 1);
                        }
                        break;
                }

                contactsPanel.Color = selectedMenu == 0 ? AnsiColor.BrightRed : AnsiColor.Silver;
                messagesPanel.Color = selectedMenu == 1 ? AnsiColor.BrightRed : AnsiColor.Silver;

                Contact selectedContactItem = allContacts.GetItem(selectedContact);

                foreach (Contact contact in allContacts.Skip(Math.Max(0, selectedContact - 10)))
                {
                    bool selected = contact == selectedContactItem; ;
                    if (selected) contactList.Style("\e[30;41m");
                    if (string.IsNullOrEmpty(contact.Name))
                    {
                        contactList.WriteLine($"{contact.Address}");
                    }
                    else
                    {
                        contactList.WriteLine($"{contact.Name} {contact.Address}");
                    }
                    if (selected) contactList.Style("\e[0m");
                }

                foreach (Message message in allMessages)
                {
                    if (message.Source is not null)
                    {
                        if (message.Source != selectedContactItem) continue;
                        messageList.Style("\e[31m");
                        messageList.Write(">");
                        messageList.Style("\e[0m");
                        messageList.Write(" ");
                        messageList.WriteLine(message.Text);
                    }
                    else if (message.Destination is not null)
                    {
                        if (message.Destination != selectedContactItem) continue;
                        messageList.Style("\e[34m");
                        messageList.Write("<");
                        messageList.Style("\e[0m");
                        messageList.Write(" ");
                        messageList.WriteLine(message.Text);
                    }
                    else
                    {
                        throw new UnreachableException();
                    }
                }
            }

            rootElement.MaxWidth = Console.WindowWidth;
            rootElement.MaxHeight = Math.Max(0, Console.WindowHeight - 1);

            rootElement.MinWidth = Console.WindowWidth;
            rootElement.MinHeight = Math.Max(0, Console.WindowHeight - 1);

            rootElement.RecalculateLayout();

            Console.Write("\e[H");
            InterfaceRenderer.Render(rootElement);
        }

        cts.Cancel();
        task.Wait();
    }

    Contact GetOrCreateContact(string address)
    {
        PossiblePhoneNumber parsed = ParseAddress(address);

        foreach (Storage<Contact> a in Contacts)
        {
            foreach (Contact contact in a)
            {
                if (contact.Address.Equals(parsed)) return contact;
            }
        }

        foreach (Contact contact in ImplicitContacts)
        {
            if (contact.Address.Equals(parsed)) return contact;
        }

        Contact result = new(-1, parsed, null);
        ImplicitContacts.Add(result);
        IsDirty = true;
        return result;
    }

    static PossiblePhoneNumber ParseAddress(string address)
    {
        PhoneNumberUtil phoneNumberUtil = PhoneNumberUtil.GetInstance();

        if (address.StartsWith("06", StringComparison.Ordinal))
        {
            address = "+36" + address[2..];
        }

        try
        {
            if (address.StartsWith('+'))
            {
                return new PossiblePhoneNumber(phoneNumberUtil.Parse(address, null), address);
            }
            else if (address.Length > 4)
            {
                string regionCode = phoneNumberUtil.GetRegionCodeForCountryCode(int.Parse(address[..Math.Min(3, address.Length)], CultureInfo.InvariantCulture));
                if (regionCode == "ZZ") regionCode = phoneNumberUtil.GetRegionCodeForCountryCode(int.Parse(address[..Math.Min(2, address.Length)], CultureInfo.InvariantCulture));
                return new PossiblePhoneNumber(phoneNumberUtil.Parse(address, regionCode), address);
            }
        }
        catch (NumberParseException)
        {
            Debug.WriteLine($"Invalid number {address}");
        }
        return new PossiblePhoneNumber(null, address);
    }

    void OnMessage(Storage<Message> storage, int index, SmsMessage message)
    {
        if (index != -1 && storage.Any(v => v.Index == index)) return;

        Contact? source = message.Sender is null ? null : GetOrCreateContact(message.Sender);
        Contact? destination = message.Destination is null ? null : GetOrCreateContact(message.Destination);

        if (message.Concat is not null)
        {
            Message? messageFrontend = storage.FirstOrDefault(v => v.Reference.HasValue && v.Reference.Value == message.Concat.Reference);
            messageFrontend ??= new Message(index, source, destination, message.ServiceCenterTimestamp ?? DateTimeOffset.UnixEpoch, message.Concat.Reference);
            messageFrontend.InsertPart(message.Concat.Sequence, message.Text);
        }
        else
        {
            Message messageFrontend = new(index, source, destination, message.ServiceCenterTimestamp ?? DateTimeOffset.UnixEpoch, message.Text);
            storage.Add(messageFrontend);
        }
        IsDirty = true;
    }

    void OnContact(Storage<Contact> storage, Modem.Contact contact)
    {
        if (contact.Index != -1 && storage.Any(v => v.Index == contact.Index)) return;

        PossiblePhoneNumber address = ParseAddress(contact.Address);

        foreach (Contact item in ImplicitContacts)
        {
            if (!item.Address.Equals(address)) continue;
            item.Index = contact.Index;
            item.Name = contact.Name;
            ImplicitContacts.Remove(item);
            storage.Add(item);
            IsDirty = true;
            return;
        }

        storage.Add(new Contact(contact.Index, address, contact.Name));
        IsDirty = true;
    }

    void ReadMessagesLocal()
    {
        using (FrontendLock.EnterScope())
        {
            string[] storages = ["ME", "SM_P"];
            foreach (string storage in storages)
            {
                Storage<Message>? storageFrontend = Messages.EnsureStorage(storage, 0, 0);

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
        if (!Directory.Exists(Path.Combine(CachePath, "phonebook"))) return;
        using (FrontendLock.EnterScope())
        {
            foreach (string file in Directory.GetFiles(Path.Combine(CachePath, "phonebook")))
            {
                string storage = Path.GetFileName(file);
                Storage<Contact>? storageFrontend = Contacts.EnsureStorage(storage, 0, 0);

                foreach (string[] line in File.ReadAllLines(file).Select(v => v.Split(' ')))
                {
                    OnContact(storageFrontend, new Modem.Contact(int.Parse(line[0], CultureInfo.InvariantCulture), line[2], int.Parse(line[1], CultureInfo.InvariantCulture), Encoding.UTF8.GetString(Convert.FromHexString(line[3]))));
                }
            }
        }
    }

    void ReadMessages(CancellationToken ct)
    {
        if (at is null) return;

        string[] storages = ["ME", "SM_P"];
        foreach (string storage in storages)
        {
            ReadStatus = $"Reading messages from {storage} ...";
            MessageStorage storageInfo = at.SelectMessageStorage(storage);

            Storage<Message> storageFrontend;
            using (FrontendLock.EnterScope())
            {
                storageFrontend = Messages.EnsureStorage(storageInfo.Name, storageInfo.Used, storageInfo.Total);
            }

            at.MessagePresentationFormat = 0;
            for (int i = 0; i <= storageInfo.Total; i++)
            {
                if (ct.IsCancellationRequested) break;

                ReadStatus = $"Reading messages from {storage} ... ({i} / {storageInfo.Total})";
                IsDirty = true;

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

    void ReadContacts(CancellationToken ct)
    {
        if (at is null) return;

        string[] storages = ["ME"];
        foreach (string storage in storages)
        {
            ReadStatus = $"Reading contacts from {storage} ...";
            at.SelectPhonebookStorage(storage);

            PhonebookStorage storageInfo = at.GetCurrentPhonebookStorage();

            Storage<Contact> storageFrontend;
            using (FrontendLock.EnterScope())
            {
                storageFrontend = Contacts.EnsureStorage(storageInfo.Name, storageInfo.Used, storageInfo.Total);
            }

            for (int i = 1; i <= storageInfo.Total; i++)
            {
                if (ct.IsCancellationRequested) break;

                ReadStatus = $"Reading contacts from {storageInfo.Name} ... ({i} / {storageInfo.Total})";
                IsDirty = true;

                Modem.Contact? raw = at.GetContact(i);
                if (!raw.HasValue) continue;
                OnContact(storageFrontend, raw.Value);
                Directory.CreateDirectory(Path.Combine(CachePath, "phonebook"));
#pragma warning disable CA2201
                File.WriteAllLines(Path.Combine(CachePath, "phonebook", storageInfo.Name),
                    storageFrontend.Select(v => $"{v.Index} {v.Type} {v.Address} {Convert.ToHexString(Encoding.UTF8.GetBytes(v.Name ?? throw new NullReferenceException()))}")
                );
#pragma warning restore CA2201
            }

            ReadStatus = null;
        }

        IsDirty = true;
    }

    void Wait(out ConsoleKeyInfo key)
    {
        int w = Console.WindowWidth;
        int h = Console.WindowHeight;
        long started = Stopwatch.GetTimestamp();

        key = default;
        while (true)
        {
            if (!Console.IsOutputRedirected && Console.KeyAvailable)
            {
                key = Console.ReadKey(true);
                return;
            }

            if (w != Console.WindowWidth || h != Console.WindowHeight)
            {
                IsDirty = true;
                return;
            }

            if (IsDirty && (Stopwatch.GetTimestamp() - started) * 10 / Stopwatch.Frequency > 0)
            {
                break;
            }

            Thread.Sleep(50);
        }
        IsDirty = false;
    }

    public void Dispose() => at?.Dispose();
}
