﻿/*
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
using System.Numerics;
using Nethermind.Core.Extensions;

namespace Nethermind.Evm.Abi
{
    public class AbiFixedLengthArray : AbiType
    {
        private readonly AbiType _elementType;

        public AbiFixedLengthArray(AbiType elementType, int length)
        {
            if (length <= 0)
            {
                throw new ArgumentException($"Length of {nameof(AbiFixedLengthArray)} has to be greater than 0", nameof(length));
            }

            _elementType = elementType;
            Length = length;
            CSharpType = _elementType.CSharpType.MakeArrayType();
        }

        public override bool IsDynamic => Length != 0 && _elementType.IsDynamic;

        public int Length { get; }

        public override string Name => $"{_elementType}[{Length}]";

        public override (object, int) Decode(byte[] data, int position)
        {
            Array result = Array.CreateInstance(_elementType.CSharpType, Length);

            if (_elementType.IsDynamic)
            {
                BigInteger currentOffset = (Length - 1) * UInt.LengthInBytes;
                int lengthsPosition = position;
                for (int i = 0; i < Length; i++)
                {
                    if (i != 0)
                    {    
                        (currentOffset, lengthsPosition) = UInt.DecodeUInt(data, lengthsPosition);
                    }

                    object element;
                    (element, currentOffset) = _elementType.Decode(data, position + (int)currentOffset);
                    result.SetValue(element, i);
                }

                position = (int)currentOffset;
            }
            else
            {
                for (int i = 0; i < Length; i++)
                {
                    (object element, int newPosition) = _elementType.Decode(data, position);
                    result.SetValue(element, i);
                    position = newPosition;
                }
            }

            return (result, position);
        }

        public override byte[] Encode(object arg)
        {
            if (arg is Array input)
            {
                if (input.Length != Length)
                {
                    throw new AbiException(AbiEncodingExceptionMessage);
                }

                if (_elementType.IsDynamic)
                {
                    byte[][] encodedItems = new byte[Length * 2 - 1][];
                    BigInteger currentOffset = (Length - 1) * UInt.LengthInBytes;
                    int i = 0;
                    foreach (object o in input)
                    {
                        encodedItems[Length + i - 1] = _elementType.Encode(o);
                        if (i != 0)
                        {
                            currentOffset += new BigInteger(encodedItems[Length + i - 1].Length);
                            encodedItems[i - 1] = UInt.Encode(currentOffset);
                        }

                        i++;
                    }

                    return Bytes.Concat(encodedItems);
                }
                else
                {
                    byte[][] encodedItems = new byte[Length][];
                    int i = 0;
                    foreach (object o in input)
                    {
                        encodedItems[i++] = _elementType.Encode(o);
                    }

                    return Bytes.Concat(encodedItems);
                }
            }

            throw new AbiException(AbiEncodingExceptionMessage);
        }

        public override Type CSharpType { get; }
    }
}