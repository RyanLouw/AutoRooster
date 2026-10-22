using System.Text.Json;
using Microsoft.Extensions.Logging;
namespace SchoolExamScheduler.Infrastructure;
public sealed class JsonFileLoggerProvider(string directory) : ILoggerProvider
{
    private readonly object sync=new();
    private readonly string logDirectory=directory;
    public ILogger CreateLogger(string categoryName)=>new FileLogger(this,categoryName);
    public void Dispose(){ }
    private sealed class FileLogger(JsonFileLoggerProvider owner,string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState:notnull=>null;
        public bool IsEnabled(LogLevel logLevel)=>logLevel>=LogLevel.Information;
        public void Log<TState>(LogLevel logLevel,EventId eventId,TState state,Exception? exception,Func<TState,Exception?,string> formatter)
        {
            if(!IsEnabled(logLevel))return;
            var properties=state is IEnumerable<KeyValuePair<string,object?>> values?values.ToDictionary(x=>x.Key,x=>x.Value?.ToString()):null;
            var line=JsonSerializer.Serialize(new {timestamp=DateTimeOffset.UtcNow,level=logLevel.ToString(),category,eventId=eventId.Id,message=formatter(state,exception),properties,exception=exception?.ToString()});
            try{lock(owner.sync){Directory.CreateDirectory(owner.logDirectory);File.AppendAllText(Path.Combine(owner.logDirectory,$"scheduler-{DateTime.UtcNow:yyyy-MM-dd}.jsonl"),line+Environment.NewLine);}}
            catch(Exception error) when(error is IOException or UnauthorizedAccessException){Console.Error.WriteLine($"Logging failed: {error.Message}");}
        }
    }
}
