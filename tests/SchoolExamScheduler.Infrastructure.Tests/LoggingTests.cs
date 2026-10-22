using Microsoft.Extensions.Logging;
using SchoolExamScheduler.Infrastructure;
using Xunit;
public sealed class LoggingTests
{
    [Fact]
    public void StructuredLogRetainsCategoryLevelAndNamedProperties()
    {
        var directory=Path.Combine(Path.GetTempPath(),"log-tests-"+Guid.NewGuid());
        try
        {
            using var provider=new JsonFileLoggerProvider(directory);provider.CreateLogger("Test").LogWarning("Roster {RosterId} failed",42);
            using var json=System.Text.Json.JsonDocument.Parse(File.ReadAllText(Assert.Single(Directory.GetFiles(directory))));
            Assert.Equal("Warning",json.RootElement.GetProperty("level").GetString());Assert.Equal("42",json.RootElement.GetProperty("properties").GetProperty("RosterId").GetString());
        }
        finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
}
