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

using System.Threading;

namespace Nethermind.Core.Specs
{
    public class Byzantium : IReleaseSpec
    {
        private static IReleaseSpec _instance;

        private Byzantium()
        {
        }

        public static IReleaseSpec Instance => LazyInitializer.EnsureInitialized(ref _instance, () => new Byzantium());
        
        public bool IsTimeAdjustmentPostOlympic => true;
        public bool AreJumpDestinationsUsed => false;
        public bool IsEip2Enabled => true;
        public bool IsEip7Enabled => true;
        public bool IsEip100Enabled => true;
        public bool IsEip140Enabled => true;
        public bool IsEip150Enabled => true;
        public bool IsEip155Enabled => true;
        public bool IsEip158Enabled => true;
        public bool IsEip160Enabled => true;
        public bool IsEip170Enabled => true;
        public bool IsEip196Enabled => true;
        public bool IsEip197Enabled => true;
        public bool IsEip198Enabled => true;
        public bool IsEip211Enabled => true;
        public bool IsEip214Enabled => true;
        public bool IsEip649Enabled => true;
        public bool IsEip658Enabled => true;
    }
}