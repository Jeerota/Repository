using Microsoft.Extensions.Logging;

namespace Repository.Models
{
    public class ResultResponse<TResult>
        where TResult : class
    {
        public Dictionary<LogLevel, HashSet<string>> Messages { get; set; } = [];

        public Exception? Exception { get; set; }

        public TResult? Record { get; set; }

        public bool Successful 
        { 
            get
            {
                return !Messages.Keys.Any(x => x == LogLevel.Error || x == LogLevel.Critical) 
                    || Exception != null;
            }
        }

        public void Trace(string message)
        {
            if(!Messages.ContainsKey(LogLevel.Trace))
                Messages.Add(LogLevel.Trace, []);

            Messages[LogLevel.Trace].Add(message);
        }

        public void Debug(string message)
        {
            if (!Messages.ContainsKey(LogLevel.Debug))
                Messages.Add(LogLevel.Debug, []);

            Messages[LogLevel.Debug].Add(message);
        }

        public void Information(string message)
        {
            if (!Messages.ContainsKey(LogLevel.Information))
                Messages.Add(LogLevel.Information, []);

            Messages[LogLevel.Information].Add(message);
        }

        public void Warning(string message)
        {
            if (!Messages.ContainsKey(LogLevel.Warning))
                Messages.Add(LogLevel.Warning, []);

            Messages[LogLevel.Warning].Add(message);
        }

        public void Error(string message)
        {
            if (!Messages.ContainsKey(LogLevel.Error))
                Messages.Add(LogLevel.Error, []);

            Messages[LogLevel.Error].Add(message);
        }

        public void Critical(string message)
        {
            if (!Messages.ContainsKey(LogLevel.Critical))
                Messages.Add(LogLevel.Critical, []);

            Messages[LogLevel.Critical].Add(message);
        }

        public void CombineResponse(Dictionary<LogLevel, HashSet<string>> messages, Exception? exception = null, TResult? record = null)
        {
            foreach (var entry in messages)
                if (!this.Messages.TryGetValue(entry.Key, out HashSet<string>? value))
                    this.Messages.Add(entry.Key, entry.Value);
                else
                    foreach(string message in messages[entry.Key])
                        value.Add(message);

            if(exception != null)
                this.Exception = exception;

            if (record != null)
                this.Record = record;
        }
    }
}
