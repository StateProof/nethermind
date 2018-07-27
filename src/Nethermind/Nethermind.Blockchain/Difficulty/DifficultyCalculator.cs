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
using Nethermind.Core;
using Nethermind.Core.Specs;

namespace Nethermind.Blockchain.Difficulty
{
    public class DifficultyCalculator : IDifficultyCalculator
    {
        private readonly ISpecProvider _specProvider;

        public DifficultyCalculator(ISpecProvider specProvider)
        {
            _specProvider = specProvider;
        }

        private const long OfGenesisBlock = 256;

        public BigInteger Calculate(
            BigInteger parentDifficulty,
            BigInteger parentTimestamp,
            BigInteger currentTimestamp,
            BigInteger blockNumber,
            bool parentHasUncles)
        {
            IReleaseSpec spec = _specProvider.GetSpec(blockNumber);
            BigInteger baseIncrease = BigInteger.Divide(parentDifficulty, OfGenesisBlock / 64);
            BigInteger timeAdjustment = TimeAdjustment(spec, parentTimestamp, currentTimestamp, parentHasUncles);
            BigInteger timeBomb = TimeBomb(spec, blockNumber);
            BigInteger difficulty = BigInteger.Max(
                OfGenesisBlock,
                parentDifficulty +
                timeAdjustment * baseIncrease +
                timeBomb);
            Console.WriteLine($"DIFFICULTY: {difficulty}");
            return difficulty;
        }

        protected internal virtual BigInteger TimeAdjustment(
            IReleaseSpec spec,
            BigInteger parentTimestamp,
            BigInteger currentTimestamp,
            bool parentHasUncles)
        {
            if (spec.IsEip100Enabled)
            {
                return BigInteger.Max((parentHasUncles ? 2 : BigInteger.One) - BigInteger.Divide(currentTimestamp - parentTimestamp, 9), -99);
            }
            
            if(spec.IsEip2Enabled)
            {
                return BigInteger.Max(BigInteger.One - BigInteger.Divide(currentTimestamp - parentTimestamp, 10), -99);    
            }
            
            if (spec.IsTimeAdjustmentPostOlympic)
            {
                return currentTimestamp < parentTimestamp + 13 ? BigInteger.One : BigInteger.MinusOne;                
            }
            
            return currentTimestamp < parentTimestamp + 7 ? BigInteger.One : BigInteger.MinusOne;
        }

        private BigInteger TimeBomb(IReleaseSpec spec, BigInteger blockNumber)
        {   
            if (spec.IsEip649Enabled)
            {
                blockNumber = blockNumber - 3000000;
            }

            return blockNumber < 200000 ? BigInteger.Zero : BigInteger.Pow(2, (int)(BigInteger.Divide(blockNumber, 100000) - 2));
        }
    }
}