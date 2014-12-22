/*
 * csharp-bcp-rpc
 * Copyright 2014 深圳岂凡网络有限公司 (Shenzhen QiFun Network Corp., LTD)
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *     http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Qifun.BcpRpc
{
    internal class ArraySegmentOutput
    {
        private static int PageSize = 128;

        public IList<ArraySegment<byte>> Buffers = new System.Collections.Generic.List<ArraySegment<byte>>();

        public void WriteByte(int c)
        {
            ArraySegment<byte> current;
            if (Buffers.Count == 0)
            {
                current = new ArraySegment<byte>(new byte[PageSize], 0, 0);
                Buffers.Add(current);
            }
            else
            {
                var last = Buffers.Last();
                if(last.Count < PageSize)
                {
                    current = last;
                }
                else
                {
                    current = new ArraySegment<byte>(new byte[PageSize], 0, 0);
                    Buffers.Add(current);
                }
            }
            current.Array[current.Count] = Convert.ToByte(c);
            var newLast = new ArraySegment<byte>(current.Array, 0, current.Count + 1);
            Buffers.RemoveAt(Buffers.Count - 1);
            Buffers.Add(newLast);
        }

        public void WriteInt(int i)
        {
            byte[] intBytes = BitConverter.GetBytes(i);
            WriteBytes(intBytes, 0, intBytes.Length);
        }

        public int WriteBytes(byte[] s, int pos, int len)
        {
            ArraySegment<byte> current;
            if (Buffers.Count == 0)
            {
                current = new ArraySegment<byte>(new byte[PageSize], 0, 0);
                Buffers.Add(current);
            }
            else
            {
                var last = Buffers.Last();
                if (last.Count < PageSize)
                {
                    current = last;
                }
                else
                {
                    current = new ArraySegment<byte>(new byte[PageSize], 0, 0);
                    Buffers.Add(current);
                }
            }
            if (len <= PageSize - current.Count)
            {
                System.Array.Copy(s, pos, current.Array, current.Count, len);
                current = new ArraySegment<byte>(current.Array, 0, current.Count + len);
                Buffers.RemoveAt(Buffers.Count - 1);
                Buffers.Add(current);
                return len;
            }
            else
            {
                int result = PageSize - current.Count;
                System.Array.Copy(s, pos, current.Array, current.Count, result);
                current = new ArraySegment<byte>(current.Array, 0, current.Count + result);
                Buffers.RemoveAt(Buffers.Count - 1);
                Buffers.Add(current);
                return result;
            }
        }

    }
}
