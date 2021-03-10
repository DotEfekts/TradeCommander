using System;
using System.Threading;
using System.Threading.Tasks;

namespace TradeCommander
{
    public class ConsoleOutput
    {
        public string Output { get; private set; } = "";
        public event EventHandler<string> OutputUpdated;

        public ConsoleOutput() { }

        public void WriteLine(string output)
        {
            Output += "\r\n" + (output ?? "\u00A0");
            OutputUpdated?.Invoke(this, output);
        }
        public async Task WriteLine(string output, int delay)
        {
            await Task.Delay(delay);
            WriteLine(output);
        }

        public void Write(string output)
        {
            Output += output;
            OutputUpdated?.Invoke(this, output);
        }

        public async Task Write(string output, int delay)
        {
            await Task.Delay(delay);
            Write(output);
        }

        public void Clear()
        {
            Output = "";
            OutputUpdated?.Invoke(this, null);
        }
    }
}
