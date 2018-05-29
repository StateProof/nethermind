/*
 * Copyright (c) 2018 Demerzel Solutions Limited
 * This file is part of the Nethermind library.
 *
 * The Nethermind library is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * The Nethermind library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with the Nethermind. If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
using System.Linq;

namespace Nethermind.Core.Encoding
{
    /// <summary>
    ///     https://github.com/ethereum/wiki/wiki/RLP
    /// </summary>
    //[DebuggerStepThrough]
    public class OldRlp
    {
        [Obsolete("to be removed")]
        public static DecodedRlp Decode(Rlp rlp, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
        {
            return Decode(new DecoderContext(rlp.Bytes), rlpBehaviors.HasFlag(RlpBehaviors.AllowExtraData));
        }

        [Obsolete("to be removed")]
        public static T Decode<T>(Rlp rlp, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
        {
            DecodedRlp decodedRlp = Decode(rlp);
            return Decode<T>(decodedRlp, rlpBehaviors);
        }

        [Obsolete("to be removed")]
        public static T Decode<T>(DecodedRlp rlp, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
        {
            return rlp.As<T>();
        }

        [Obsolete("to be removed")]
        public static T[] DecodeArray<T>(Rlp rlp, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
        {
            DecodedRlp decodedRlp = Decode(rlp);
            return DecodeArray<T>(decodedRlp, rlpBehaviors);
        }

        [Obsolete("to be removed")]
        public static T[] DecodeArray<T>(DecodedRlp rlp, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
        {
            T[] array = new T[rlp.Items.Count];
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = (T)rlp.Items[i];
            }

            return array;
        }

        [Obsolete("to be removed")]
        public static Rlp[] ExtractRlpList(Rlp rlp)
        {
            return ExtractRlpList(new DecoderContext(rlp.Bytes));
        }

        [Obsolete("to be removed")]
        private static Rlp[] ExtractRlpList(DecoderContext context)
        {
            List<Rlp> result = new List<Rlp>();

            while (context.CurrentIndex < context.MaxIndex)
            {
                byte prefix = context.Pop();
                byte[] lenghtBytes = null;

                int concatenationLength;

                if (prefix == 0)
                {
                    result.Add(new Rlp(new byte[] { 0 }));
                    continue;
                }

                if (prefix < 128)
                {
                    result.Add(new Rlp(new[] { prefix }));
                    continue;
                }

                if (prefix == 128)
                {
                    result.Add(new Rlp(new byte[] { }));
                    continue;
                }

                if (prefix <= 183)
                {
                    int length = prefix - 128;
                    byte[] content = context.Pop(length);
                    if (content.Length == 1 && content[0] < 128)
                    {
                        throw new RlpException($"Unexpected byte value {content[0]}");
                    }

                    result.Add(new Rlp(new[] { prefix }.Concat(content).ToArray()));
                    continue;
                }

                if (prefix <= 247)
                {
                    concatenationLength = prefix - 192;
                }
                else
                {
                    int lengthOfConcatenationLength = prefix - 247;
                    if (lengthOfConcatenationLength > 4)
                    {
                        // strange but needed to pass tests -seems that spec gives int64 length and tests int32 length
                        throw new RlpException("Expected length of lenth less or equal 4");
                    }

                    lenghtBytes = context.Pop(lengthOfConcatenationLength);
                    concatenationLength = DeserializeLength(lenghtBytes);
                    if (concatenationLength < 56)
                    {
                        throw new RlpException("Expected length greater or equal 56");
                    }
                }

                byte[] data = context.Pop(concatenationLength);
                byte[] itemBytes = { prefix };
                if (lenghtBytes != null)
                {
                    itemBytes = itemBytes.Concat(lenghtBytes).ToArray();
                }

                result.Add(new Rlp(itemBytes.Concat(data).ToArray()));
            }

            return result.ToArray();
        }

        [Obsolete("to be removed")]
        private static DecodedRlp Decode(DecoderContext context, bool allowExtraData)
        {
            DecodedRlp CheckAndReturnSingle(object singleItem, DecoderContext contextToCheck)
            {
                if (!allowExtraData && contextToCheck.CurrentIndex != contextToCheck.MaxIndex)
                {
                    throw new RlpException("Invalid RLP length");
                }

                return new DecodedRlp(singleItem);
            }

            DecodedRlp CheckAndReturn(List<object> resultToCollapse, DecoderContext contextToCheck)
            {
                if (!allowExtraData && contextToCheck.CurrentIndex != contextToCheck.MaxIndex)
                {
                    throw new RlpException("Invalid RLP length");
                }

                return new DecodedRlp(resultToCollapse);
            }

            byte prefix = context.Pop();

            if (prefix == 0)
            {
                return CheckAndReturnSingle(new byte[] { 0 }, context);
            }

            if (prefix < 128)
            {
                return CheckAndReturnSingle(new[] { prefix }, context);
            }

            if (prefix == 128)
            {
                return CheckAndReturnSingle(new byte[] { }, context);
            }

            if (prefix <= 183)
            {
                int length = prefix - 128;
                byte[] data = context.Pop(length);
                if (data.Length == 1 && data[0] < 128)
                {
                    throw new RlpException($"Unexpected byte value {data[0]}");
                }

                return CheckAndReturnSingle(data, context);
            }

            if (prefix < 192)
            {
                int lengthOfLength = prefix - 183;
                if (lengthOfLength > 4)
                {
                    // strange but needed to pass tests -seems that spec gives int64 length and tests int32 length
                    throw new RlpException("Expected length of lenth less or equal 4");
                }

                int length = DeserializeLength(context.Pop(lengthOfLength));
                if (length < 56)
                {
                    throw new RlpException("Expected length greater or equal 56");
                }

                byte[] data = context.Pop(length);
                return CheckAndReturnSingle(data, context);
            }

            int concatenationLength;
            if (prefix <= 247)
            {
                concatenationLength = prefix - 192;
            }
            else
            {
                int lengthOfConcatenationLength = prefix - 247;
                if (lengthOfConcatenationLength > 4)
                {
                    // strange but needed to pass tests -seems that spec gives int64 length and tests int32 length
                    throw new RlpException("Expected length of lenth less or equal 4");
                }

                concatenationLength = DeserializeLength(context.Pop(lengthOfConcatenationLength));
                if (concatenationLength < 56)
                {
                    throw new RlpException("Expected length greater or equal 56");
                }
            }

            long startIndex = context.CurrentIndex;
            List<object> nestedList = new List<object>();
            while (context.CurrentIndex < startIndex + concatenationLength)
            {
                DecodedRlp decodedRlp = Decode(context, true);
                nestedList.Add(decodedRlp.IsSequence ? decodedRlp : decodedRlp.SingleItem);
            }

            return CheckAndReturn(nestedList, context);
        }

        [Obsolete("to be removed")]
        private static int DeserializeLength(byte[] bytes)
        {
            if (bytes[0] == 0)
            {
                throw new RlpException("Length starts with 0");
            }

            const int size = sizeof(int);
            byte[] padded = new byte[size];
            Buffer.BlockCopy(bytes, 0, padded, size - bytes.Length, bytes.Length);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(padded);
            }

            return BitConverter.ToInt32(padded, 0);
        }

        public class DecoderContext
        {
            public DecoderContext(byte[] data)
            {
                Data = data;
                MaxIndex = Data.Length;
            }

            public byte[] Data { get; }
            public int CurrentIndex { get; set; }
            public int MaxIndex { get; set; }

            public byte Pop()
            {
                return Data[CurrentIndex++];
            }

            public byte[] Pop(int n)
            {
                byte[] bytes = new byte[n];
                Buffer.BlockCopy(Data, CurrentIndex, bytes, 0, n);
                CurrentIndex += n;
                return bytes;
            }
        }
    }
}