using System;
using Ddxy.GameServer.Core;
using Ddxy.Protocol;
using Google.Protobuf;
using Orleans.Concurrency;

namespace Ddxy.GameServer.Logic.SectWar
{
    public class SectWarSectPair : IDisposable
    {
        public SectWarSect Sect1 { get; set; }

        public SectWarSect Sect2 { get; set; }

        public SectWarArena Arena { get; private set; }

        public SectWarCannon Cannon { get; private set; }

        public SectWarSectPair(SectWarSect sect1, SectWarSect sect2)
        {
            Sect1 = sect1;
            Sect2 = sect2;

            // 绑定好敌对关系
            Sect1.Camp = 1;
            Sect1.Enemy = Sect2;
            if (Sect2 != null)
            {
                Sect2.Camp = 2;
                Sect2.Enemy = Sect1;
            }

            Arena = new SectWarArena();
            Cannon = new SectWarCannon();
        }

        public void Broadcast(GameCmd cmd, IMessage msg)
        {
            Broadcast(new Immutable<byte[]>(Packet.Serialize(cmd, msg)));
        }

        public void Broadcast(Immutable<byte[]> bytes, uint ignore = 0)
        {
            Sect1.Broadcast(bytes, ignore);
            Sect2?.Broadcast(bytes, ignore);
        }

        public void Dispose()
        {
            Arena?.Dispose();
            Arena = null;
            Cannon?.Dispose();
            Cannon = null;
            Sect1?.Dispose();
            Sect1 = null;
            Sect2?.Dispose();
            Sect2 = null;
        }
    }
}