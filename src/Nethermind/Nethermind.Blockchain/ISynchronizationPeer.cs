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

using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Model;

namespace Nethermind.Blockchain
{
    public interface ISynchronizationPeer
    {
        NodeId NodeId { get; }
        string ClientId { get; }
        Task<Block[]> GetBlocks(Keccak[] blockHashes, CancellationToken token);
        Task<BlockHeader[]> GetBlockHeaders(Keccak blockHash, int maxBlocks, int skip, CancellationToken token);
        Task<BlockHeader[]> GetBlockHeaders(BigInteger number, int maxBlocks, int skip, CancellationToken token);
        Task<Keccak> GetHeadBlockHash();
        Task<BigInteger> GetHeadBlockNumber(CancellationToken token);
        void SendNewBlock(Block block);
        void SendNewTransaction(Transaction transaction);
    }
}