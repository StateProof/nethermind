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
using System.IO;
using System.Linq;
using Nethermind.Config;
using Nethermind.Core;
using Nethermind.Core.Encoding;
using Nethermind.Core.Logging;
using Nethermind.Db;
using Nethermind.Network.Config;
using Nethermind.Network.Discovery.RoutingTable;
using Nethermind.Store;

namespace Nethermind.Network
{
    public class PeerStorage : IPeerStorage
    {
        private readonly INetworkConfig _configurationProvider;
        private readonly INodeFactory _nodeFactory;
        private readonly IPerfService _perfService;
        private readonly IFullDb _db;
        private readonly ILogger _logger;
        private long _updateCounter;
        private long _removeCounter;

        public PeerStorage(IConfigProvider configurationProvider, INodeFactory nodeFactory, ILogManager logManager, IPerfService perfService)
        {
            _logger = logManager?.GetClassLogger();
            _configurationProvider = configurationProvider.GetConfig<NetworkConfig>();
            _nodeFactory = nodeFactory;
            _perfService = perfService;
            _db = new FullDbOnTheRocks(Path.Combine(_configurationProvider.DbBasePath, FullDbOnTheRocks.PeersDbPath));
        }

        public (Node Node, long PersistedReputation)[] GetPersistedPeers()
        {
            return _db.Values.Select(GetNode).ToArray();
        }

        public void UpdatePeers(Peer[] peers)
        {
            for (var i = 0; i < peers.Length; i++)
            {
                var peer = peers[i];
                var node = peer.Node;
                var networkNode = new NetworkNode(node.Id.Bytes, node.Host, node.Port, node.Description, peer.NodeStats?.NewPersistedNodeReputation ?? 0);
                _db[networkNode.NodeId.Bytes] = Rlp.Encode(networkNode).Bytes;
                _updateCounter++;
            }
        }

        public void RemovePeers(Peer[] nodes)
        {
            for (var i = 0; i < nodes.Length; i++)
            {
                _db.Remove(nodes[i].Node.Id.Bytes);
                _removeCounter++;
            }
        }

        public void StartBatch()
        {
            _db.StartBatch();
            _updateCounter = 0;
            _removeCounter = 0;
        }

        public void Commit()
        {
            var key = _perfService.StartPerfCalc();
            if (_logger.IsInfoEnabled)
            {
                _logger.Info($"Commiting peers, updates: {_updateCounter}, removes: {_removeCounter}");
            }
            _db.CommitBatch();
            _perfService.EndPerfCalc(key, "PeerStorage commit");
        }

        public bool AnyPendingChange()
        {
            return _updateCounter > 0 || _removeCounter > 0;
        }

        private (Node, long) GetNode(byte[] networkNodeRaw)
        {
            var persistedNode = Rlp.Decode<NetworkNode>(networkNodeRaw);
            var node = _nodeFactory.CreateNode(persistedNode.NodeId, persistedNode.Host, persistedNode.Port);
            node.Description = persistedNode.Description;
            return (node, persistedNode.Reputation);
        }
    }
}