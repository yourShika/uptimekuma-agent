namespace UptimeKumaTrayAgent.Services;

public interface IAgentPaths
{
    string ConfigPath { get; }
    string DataDirectory { get; }
    string LogDirectory { get; }
}
