﻿using System.Numerics;
using Nevermind.Core.Sugar;

namespace Nevermind.Evm.Abi
{
    public class AbiDynamicBytes : AbiType
    {
        public override bool IsDynamic => true;

        public override string Name => "bytes";

        public override (byte[], int) Decode(byte[] data, int position)
        {
            (BigInteger length, int currentPosition) = AbiUInt.DecodeLength(data, position);
            return (data.Slice(currentPosition, (int) length), currentPosition + (int) length);
        }
    }
}