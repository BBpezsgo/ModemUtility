using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.IO.Ports;
using System.Text;

namespace ModemUtility.Modem;

public class ATModem : IDisposable
{
    bool IsDisposed;
    readonly SerialPort Port;

    public ATModem(string portName, int baudRate)
    {
        Port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
        {
            NewLine = "\r\n",
            ReadTimeout = 10000,
            WriteTimeout = 10000
        };
        Port.Open();

        while (Port.BytesToRead > 0)
        {
            Port.ReadByte();
        }
    }

    public string ATGet(string command, bool debug = false)
    {
        string result = Send($"AT{command}", debug);

        if (!result.StartsWith($"{command}: ", StringComparison.Ordinal)) throw new CommunicationException($"Invalid response");

        return result[(command.Length + 2)..];
    }

    public string ATQuery(string command, bool debug = false)
    {
        string result = Send($"AT{command}?", debug);

        if (!result.StartsWith($"{command}: ", StringComparison.Ordinal)) throw new CommunicationException($"Invalid response");

        return result[(command.Length + 2)..];
    }

    public string ATSet(string command, string value, bool debug = false)
    {
        string result = Send($"AT{command}={value}", debug);

        if (string.IsNullOrEmpty(result)) return result;
        if (!result.StartsWith($"{command}: ", StringComparison.Ordinal)) throw new CommunicationException($"Invalid response");

        return result[(command.Length + 2)..];
    }

    public string ATTest(string command, bool debug = false)
    {
        string result = Send($"AT{command}=?", debug);

        if (!result.StartsWith($"{command}: ", StringComparison.Ordinal)) throw new CommunicationException($"Invalid response");

        return result[(command.Length + 2)..];
    }

    public string Send(string command, bool debug = false)
    {
        string result = SendImp(command, debug);

        if (result.StartsWith("+CME ERROR:", StringComparison.Ordinal))
        {
            throw new CMEError(result.Trim());
        }

        int i = result.LastIndexOf('\n');

        string finalResult = result[(i + 1)..];
        if (finalResult != "OK" && finalResult != "0" && finalResult != string.Empty) throw new ModemException(finalResult);

        if (i == -1)
        {
            return string.Empty;
        }
        else
        {
            return result[..i];
        }
    }

    bool WaitForData(int timeoutMs)
    {
        long started = Stopwatch.GetTimestamp();
        while (Port.BytesToRead == 0)
        {
            if (Stopwatch.GetElapsedTime(started).TotalMilliseconds > timeoutMs)
            {
                return false;
            }
            Thread.Sleep(50);
        }
        return true;
    }

    void WaitForData()
    {
        while (Port.BytesToRead == 0)
        {
            Thread.Sleep(50);
        }
    }

    public void Write(string data) => Port.Write(data);

    public string SendImp(string command, bool debug = true)
    {
        if (debug)
        {
            Console.Write(">>>");
            Console.Write(' ');
            Console.WriteLine(command);
        }

        Port.WriteLine(command);

        string line = Port.ReadLine().Trim();
        if (line != command)
        { throw new InvalidOperationException($"Unexpected response: \"{line}\" != \"{command}\""); }

        if (!WaitForData(Port.ReadTimeout)) throw new TimeoutException();

        StringBuilder builder = new();
        while (Port.BytesToRead > 0)
        {
            line = Port.ReadLine().Trim();
            if (line.Length == 0) goto ok;
            if (debug)
            {
                Console.Write("<<<");
                Console.Write(' ');
                Console.WriteLine(line);
            }

            builder.AppendLine(line);

            if (line is "OK" or "NO CARRIER" or "ERROR") break;
            if (line.StartsWith("+CME ERROR:", StringComparison.Ordinal)) break;

            ok:

            WaitForData();
        }

        if (debug) Console.WriteLine();

        return builder.ToString().Trim();
    }

    public string SendImpWithInput(string command, string input, bool debug = true)
    {
        if (debug)
        {
            Console.Write(">>>");
            Console.Write(' ');
            Console.WriteLine(command);
        }
        Port.WriteLine(command);

        string line = Port.ReadLine().Trim();

        if (line != command)
        { throw new InvalidOperationException($"Unexpected response: \"{line}\" != \"{command}\""); }

        Port.Write(input);

        StringBuilder builder = new();
        while (Port.BytesToRead > 0)
        {
            line = Port.ReadLine().Trim();
            if (line.Length == 0) goto ok;
            if (debug)
            {
                Console.Write("<<<");
                Console.Write(' ');
                Console.WriteLine(line);
            }

            builder.AppendLine(line);

            if (line is "OK" or "NO CARRIER" or "ERROR") break;
            if (line.StartsWith("+CME ERROR:", StringComparison.Ordinal)) break;

            ok:

            WaitForData();
        }

        if (debug) Console.WriteLine();

        return builder.ToString().Trim();
    }

    public string SendImpRaw(string command, bool debug = true)
    {
        if (debug)
        {
            Console.Write(">>>");
            Console.Write(' ');
            Console.WriteLine(command);
        }
        Port.Write(command);

        if (!WaitForData(Port.ReadTimeout)) throw new TimeoutException();

        bool isFirstLine = true;

        StringBuilder builder = new();
        while (Port.BytesToRead > 0)
        {
            string line = Port.ReadLine().Trim();
            if (debug && line.Length > 0)
            {
                Console.Write("<<<");
                Console.Write(' ');
                Console.WriteLine(line);
            }

            if (isFirstLine)
            {
                if (line != command)
                { throw new InvalidOperationException($"Unexpected response: \"{line}\" != \"{command}\""); }

                isFirstLine = false;
                goto ok;
            }
            if (line.Length == 0) goto ok;

            builder.AppendLine(line);

            if (line is "OK" or "NO CARRIER" or "ERROR") break;
            if (line.StartsWith("+CME ERROR:", StringComparison.Ordinal)) break;

            ok:;

            WaitForData();
        }

        if (debug) Console.WriteLine();

        return builder.ToString().Trim();
    }

    public void Dispose()
    {
        if (!IsDisposed)
        {
            Port.Dispose();
            IsDisposed = true;
        }

        GC.SuppressFinalize(this);
    }

    public void SetEcho(bool value)
    {
        if (value) SendImp("ATE1", false);
        else SendImp("ATE0", false);
    }

    public void SuppressResultCodes(bool value)
    {
        if (value) Send("ATQ1");
        else Send("ATQ0");
    }

    public void SetVerbose(bool value)
    {
        if (value) Send("ATV1");
        else Send("ATV0");
    }

    public string ModelId => ATGet("+CGMM");

    public string RevisionId => ATGet("+CGMR");

    public int MobileEquipmentReporting
    {
        get => int.Parse(ATQuery("+CMEE"), CultureInfo.InvariantCulture);
        set => ATSet("+CMEE", value.ToString(CultureInfo.InvariantCulture));
    }

    public void SetNetworkRegistration(int value)
    {
        ATSet("+CREG", value.ToString(CultureInfo.InvariantCulture));
    }

    public string GetNetworkRegistration()
    {
        return ATQuery("+CREG");
    }

    public int MessagePresentationFormat
    {
        get => int.Parse(ATQuery("+CMGF"), CultureInfo.InvariantCulture);
        set => ATSet("+CMGF", value.ToString(CultureInfo.InvariantCulture));
    }

    public string InternationalMobileSubscriberIdentityNumber
    {
        get => ATQuery("+CIMI");
    }

    public string MessageServiceCentreNumber
    {
        get => ATQuery("+CSCA");
    }

    public ImmutableArray<MessageStorage> GetMessageStorage()
    {
        ReadOnlySpan<char> res = ATQuery("+CPMS", true);
        if (res.IsEmpty) throw new CommunicationException($"Empty response");

        ImmutableArray<MessageStorage>.Builder result = ImmutableArray.CreateBuilder<MessageStorage>();
        Reader reader = new(res);

        while (!reader.IsEnd)
        {
            result.Add(MessageStorage.Parse(ref reader));
        }

        return result.ToImmutable();
    }

    public MessageStorage SelectMessageStorage(string name)
    {
        ReadOnlySpan<char> res = ATSet("+CPMS", $"\"{name}\"");
        if (res.IsEmpty) throw new CommunicationException($"Empty response");

        Span<Range> ranges = stackalloc Range[3];
        res.Split(ranges, ',', StringSplitOptions.TrimEntries);

        int used = int.Parse(res[ranges[0]], CultureInfo.InvariantCulture);
        int total = int.Parse(res[ranges[1]], CultureInfo.InvariantCulture);

        return new MessageStorage(name, used, total);
    }

    public void SelectPhonebookStorage(string name)
    {
        ATSet("+CPBS", $"\"{name}\"");
    }

    public PhonebookStorage GetCurrentPhonebookStorage()
    {
        ReadOnlySpan<char> res = ATQuery("+CPBS");
        if (res.IsEmpty) throw new CommunicationException($"Empty response");

        Reader reader = new(res);
        return PhonebookStorage.Parse(ref reader);
    }

    public Contact? GetContact(int index)
    {
        ReadOnlySpan<char> res;
        try
        {
            res = ATSet("+CPBR", index.ToString(CultureInfo.InvariantCulture));
        }
        catch (ModemException ex) when (ex.Message == "ERROR")
        {
            return null;
        }

        if (res.IsEmpty) return null;

        Reader reader = new(res);
        if (reader.IsEnd) throw new CommunicationException($"Invalid response");

        return Contact.Parse(ref reader);
    }

    public ImmutableArray<Contact> GetContacts(int start, int end)
    {
        ReadOnlySpan<char> res = ATSet("+CPBR", $"{start}, {end}", true);
        if (res.IsEmpty) throw new CommunicationException($"Empty response");

        ImmutableArray<Contact>.Builder result = ImmutableArray.CreateBuilder<Contact>();
        Reader reader = new(res);

        while (!reader.IsEnd)
        {
            result.Add(Contact.Parse(ref reader));
        }

        return result.ToImmutable();
    }

    public ImmutableArray<Operator> GetOperators()
    {
        ReadOnlySpan<char> res = ATQuery("+COPS");
        if (res.IsEmpty) throw new CommunicationException($"Empty response");

        ImmutableArray<Operator>.Builder result = ImmutableArray.CreateBuilder<Operator>();
        Reader reader = new(res);

        while (!reader.IsEnd)
        {
            result.Add(Operator.Parse(ref reader));
        }

        return result.ToImmutable();
    }

    public Message GetMessageText(int index)
    {
        ReadOnlySpan<char> res = ATSet("+CMGR", index.ToString(CultureInfo.InvariantCulture));
        if (res.IsEmpty) throw new CommunicationException($"Empty response");

        Reader reader = new(res);
        if (reader.IsEnd) throw new CommunicationException($"Invalid response");

        return Message.ParseText(ref reader);
    }

    public (MessageStatus Status, int Length, string PDU)? GetMessagePdu(int index)
    {
        ReadOnlySpan<char> res;

        try
        {
            res = ATSet("+CMGR", index.ToString(CultureInfo.InvariantCulture));
        }
        catch (ModemException ex) when (ex.Message == "ERROR")
        {
            return null;
        }

        if (res.IsEmpty) throw new CommunicationException($"Empty response");

        Reader reader = new(res);
        if (reader.IsEnd) throw new CommunicationException($"Invalid response");

        return Message.ParsePdu(ref reader);
    }

    public void DeleteMessage(int index)
    {
        ATSet("+CMGD", index.ToString(CultureInfo.InvariantCulture));
    }
}
