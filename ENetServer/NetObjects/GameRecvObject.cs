﻿using ENetServer.Network;
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
        public RecvType RecvType { get; }
        public PeerParams PeerParams { get; }
        public uint Data { get; }
        public GameDataObject? GameDataObject { get; }

        public GameRecvObject(RecvType recvType, PeerParams peerParams, uint data)
        {
            RecvType = recvType;
            PeerParams = peerParams;
            Data = data;
            // GameDataObject remains null.
        }

        public GameRecvObject(RecvType recvType, PeerParams peerParams, GameDataObject gameDataObject)
        {
            RecvType = recvType;
            PeerParams = peerParams;
            GameDataObject = gameDataObject;
            // Data remains 0.
        }

    }
}
