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
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;

namespace Nethermind.Blockchain
{
    public class FakeSealEngine : ISealEngine
    {
        private readonly TimeSpan _miningDelay;
        private readonly bool _exact;

        public FakeSealEngine(TimeSpan miningDelay, bool exact = true)
        {
            _miningDelay = miningDelay;
            _exact = exact;
        }

        private static readonly Random Random = new Random();

        private TimeSpan RandomizeDelay()
        {
            return _miningDelay + TimeSpan.FromMilliseconds((_exact ? 0 : 1) * (Random.Next((int)_miningDelay.TotalMilliseconds) - (int)_miningDelay.TotalMilliseconds / 2));
        }

        public Task<Block> MineAsync(Block block, CancellationToken cancellationToken)
        {
            return Task.Factory.StartNew(() =>
            {
                BigInteger value = BigInteger.Parse(Console.ReadLine());
                
//                BigInteger value = block.Difficulty + 1;
//                do
//                {
//                    test += 1;
//                } while (!value.IsProbablePrime(1));

                block.Header.Timestamp = Timestamp.UnixUtcUntilNowSecs;
                block.Header.MixHash = new Keccak(value.ToByteArray(true, true).PadLeft(32));

//            block.Header.MixHash = Keccak.Zero;
                block.Header.Hash = BlockHeader.CalculateHash(block.Header);
                return block;
            }, cancellationToken);

//
//            return _miningDelay == TimeSpan.Zero
//                ? Task.FromResult(block)
//                : Task.Delay(RandomizeDelay(), cancellationToken)
//                    .ContinueWith(t => block, cancellationToken);
        }

        public bool Validate(BlockHeader header)
        {
            BigInteger value = header.MixHash.Bytes.ToUnsignedBigInteger();
            return value.IsProbablePrime(1);
        }

        public bool IsMining { get; set; }
    }
}