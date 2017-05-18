using NullGuard;
using SharpCaster.Models;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCaster.Channels
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal abstract class ChromecastChannel
    {
        protected ChromeCastClient Client { get; }
        public string Namespace { get; }

        protected ChromecastChannel(ChromeCastClient client, string ns)
        {
            Namespace = ns;
            Client = client;
        }

        public async Task Write(CastMessage message, CancellationToken token, bool includeNameSpace = true)
        {
            if (includeNameSpace)
            {
                message.Namespace = Namespace;
            }
            var bytes = message.ToProto();
            await Client.ChromecastSocketService.Write(bytes, token).ConfigureAwait(false);
        }

        public abstract Task Abort();

        internal abstract void OnMessageReceived(CastMessage castMessage);
    }
}