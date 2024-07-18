using MagicOnion;

namespace FreeTrainSimulator.Online
{
    public interface IConnectivity: IService<IConnectivity>
    {
        UnaryResult<long> Connect();
    }
}
