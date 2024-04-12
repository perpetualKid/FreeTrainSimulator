using System.Threading.Tasks;

using MagicOnion;

namespace MultiPlayer.Shared
{
    public interface IMultiPlayerHub: IStreamingHub<IMultiPlayerHub, IMultiPlayerClient>
    {
        ValueTask SendMessageAsync(MultiPlayerMessage message);

        ValueTask JoinGameAsync(string userName, string route, string accessCode);
    }
}
