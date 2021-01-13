using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace USBInterface
{
    public class ArrayHelpers
    {
        public void StreamWriteChar(char c, byte[] stream, int index)
        {
            if (index < stream.Length)
            {
                stream[index] = (byte)c;
                index++;
            }
        }

        public void StreamWriteInt16(ushort num, byte[] stream, int index)
        {
            if (index + 1 < stream.Length)
            {
                stream[index] = (byte)((num >> 8) & 0xff);
                index++;
                stream[index] = (byte)(num & 0xff);
                index++;
            }
        }

        public void StreamWriteInt32(uint num, byte[] stream, int index)
        {
            if (index + 3 < stream.Length)
            {
                stream[index] = (byte)((num >> 24) & 0xff);
                index++;
                stream[index] = (byte)((num >> 16) & 0xff);
                index++;
                stream[index] = (byte)((num >> 8) & 0xff);
                index++;
                stream[index] = (byte)(num&0xff);
                index++; 
            }
        }

        public char StreamReadChar(byte[] stream, int index)
        {
            char ret = '\0';
            if (index < stream.Length)
            {
                ret = (char)stream[index];
                index++;
            }
            return ret;
        }

        public ushort StreamReadInt16(byte[] stream, int index)
        {
            ushort ret = 0;
            if (index + 1 < stream.Length)
            {
                ret |= (ushort)(stream[index] << 8);
                index++;
                ret |= (ushort)(stream[index]);
                index++;
            }
            return ret;
        }

        public uint StreamReadInt32(byte[] stream, int index)
        {
            uint ret = 0;
            if (index + 3 < stream.Length)
            {
                ret |= (uint)(stream[index] << 24);
                index++;
                ret |= (uint)(stream[index] << 16);
                index++;
                ret |= (uint)(stream[index] << 8);
                index++;
                ret |= (uint)(stream[index]);
                index++;
            }
            return ret;
        }

    }
}
