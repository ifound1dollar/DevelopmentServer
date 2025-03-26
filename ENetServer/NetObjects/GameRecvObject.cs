using ENetServer.Network;
using ENetServer.NetObjects.DataObjects;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ENetServer.NetStatics;

namespace ENetServer.NetObjects
{
    /// <summary>
    /// Net object containing DESERIALIZED network data JUST RECEIVED over the network.
    ///  Use GameRecvObject.Factory to create objects.
    /// </summary>
    public class GameRecvObject
    {
        public RecvType RecvType { get; private set; }
        public Connection Connection { get; private set; }
        public GameDataObject? GameDataObject { get; private set; }

        public GameRecvObject(RecvType recvType, Connection connection)
        {
            RecvType = recvType;
            Connection = connection;
            // GameDataObject remains null.
        }

        public GameRecvObject(RecvType recvType, Connection connection, GameDataObject gameDataObject)
        {
            RecvType = recvType;
            Connection = connection;
            GameDataObject = gameDataObject;
        }

    }
}
