﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Terraria;
using TerrariaAPI.Hooks;

namespace TShockAPI
{
    public class PacketBufferer : IDisposable
    {
        /// <summary>
        /// Maximum number of bytes to send per update per socket
        /// </summary>
        public int BytesPerUpdate { get; set; }

        PacketBuffer[] buffers = new PacketBuffer[Netplay.serverSock.Length];

        public PacketBufferer()
        {
            BytesPerUpdate = 0xFFFF;
            buffers.ForEach(p => p = new PacketBuffer());

            ServerHooks.SendBytes += ServerHooks_SendBytes;
            ServerHooks.SocketReset += ServerHooks_SocketReset;
            GameHooks.PostUpdate += GameHooks_Update;
        }

        public void Dispose()
        {
            GameHooks.PostUpdate -= GameHooks_Update;
            ServerHooks.SendBytes -= ServerHooks_SendBytes;
            ServerHooks.SocketReset -= ServerHooks_SocketReset;
        }


        void GameHooks_Update(GameTime obj)
        {
            for (int i = 0; i < Netplay.serverSock.Length; i++)
            {
                if (Netplay.serverSock[i] == null || !Netplay.serverSock[i].active)
                    continue;

                if (!Netplay.serverSock[i].tcpClient.Client.Poll(0, SelectMode.SelectWrite))
                    continue;

                byte[] buff = buffers[i].GetBytes(BytesPerUpdate);
                Netplay.serverSock[i].networkStream.Write(buff, 0, buff.Length);
            }
        }


        void ServerHooks_SocketReset(ServerSock socket)
        {
            buffers[socket.whoAmI] = new PacketBuffer();
        }

        void ServerHooks_SendBytes(ServerSock socket, byte[] buffer, int offset, int count, HandledEventArgs e)
        {
            e.Handled = true;
            lock (buffers[socket.whoAmI])
            {
                buffers[socket.whoAmI].AddRange(new MemoryStream(buffer, offset, count).ToArray());
            }
        }


    }

    public class PacketBuffer : List<byte>
    {
        public byte[] GetBytes(int max)
        {
            lock (this)
            {
                if (this.Count < 1)
                    return null;

                var ret = new byte[Math.Min(max, this.Count)];
                this.CopyTo(0, ret, 0, ret.Length);
                this.RemoveRange(0, ret.Length);
                return ret;
            }
        }
    }
}
