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

using Nethermind.Blockchain;
using Nethermind.Network.Discovery.Lifecycle;
using Nethermind.Network.Discovery.RoutingTable;
using Nethermind.Network.P2P;
using Nethermind.Network.Rlpx;
using Nethermind.Network.Stats;

namespace Nethermind.Network
{
    public class Peer
    {
        public Peer(Node node, INodeStats nodeStats)
        {
            Node = node;
            NodeStats = nodeStats;
        }

        public Peer(INodeLifecycleManager manager)
        {
            Node = manager.ManagedNode;
            NodeLifecycleManager = manager;
            NodeStats = manager.NodeStats;
        }

        public Node Node { get; }
        public INodeLifecycleManager NodeLifecycleManager { get; set; }
        public INodeStats NodeStats { get; }
        public IP2PSession Session { get; set; }
        public ISynchronizationPeer SynchronizationPeer { get; set; }
        public IP2PMessageSender P2PMessageSender { get; set; }
        public ClientConnectionType ClientConnectionType { get; set; }
    }
}