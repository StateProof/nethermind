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

using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Discovery.Messages;
using Nethermind.Discovery.RoutingTable;
using Nethermind.Network;

namespace Nethermind.Discovery.Serializers
{
    public class DiscoveryMsgSerializersProvider : IDiscoveryMsgSerializersProvider
    {
        private readonly IMessageSerializationService _messageSerializationService;
        private readonly PingMessageSerializer _pingMessageSerializer;
        private readonly PongMessageSerializer _pongMessageSerializer;
        private readonly FindNodeMessageSerializer _findNodeMessageSerializer;
        private readonly NeighborsMessageSerializer _neighborsMessageSerializer;

        public DiscoveryMsgSerializersProvider(IMessageSerializationService messageSerializationService, ISigner signer, IPrivateKeyProvider privateKeyProvider, IDiscoveryMessageFactory messageFactory, INodeIdResolver nodeIdResolver, INodeFactory nodeFactory)
        {
            var pingSerializer = new PingMessageSerializer(signer, privateKeyProvider, messageFactory, nodeIdResolver, nodeFactory);
            var pongSerializer = new PongMessageSerializer(signer, privateKeyProvider, messageFactory, nodeIdResolver, nodeFactory);
            var findNodeSerializer = new FindNodeMessageSerializer(signer, privateKeyProvider, messageFactory, nodeIdResolver, nodeFactory);
            var neighborsSerializer = new NeighborsMessageSerializer(signer, privateKeyProvider, messageFactory, nodeIdResolver, nodeFactory);

            _messageSerializationService = messageSerializationService;
            _pingMessageSerializer = pingSerializer;
            _pongMessageSerializer = pongSerializer;
            _findNodeMessageSerializer = findNodeSerializer;
            _neighborsMessageSerializer = neighborsSerializer;
        }

        public void RegisterDiscoverySerializers()
        {
            _messageSerializationService.Register(_pingMessageSerializer);
            _messageSerializationService.Register(_pongMessageSerializer);
            _messageSerializationService.Register(_findNodeMessageSerializer);
            _messageSerializationService.Register(_neighborsMessageSerializer);
        }
    }
}