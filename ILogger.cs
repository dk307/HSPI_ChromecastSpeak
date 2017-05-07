namespace Hspi
{
    public interface ILogger
    {
        void DebugLog(string message);

        void LogInfo(string message);

        void LogWarning(string message);

        void LogError(string message);
    }
}