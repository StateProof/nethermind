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

using System.Linq;
using System.Net;
using System.Threading;
using Nethermind.Config;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Logging;
using Nethermind.KeyStore;
using Nethermind.Network.Config;
using Nethermind.Network.Discovery;
using Nethermind.Network.Discovery.Lifecycle;
using Nethermind.Network.Discovery.Messages;
using Nethermind.Network.Discovery.RoutingTable;
using Nethermind.Network.Stats;
using NSubstitute;
using NUnit.Framework;

namespace Nethermind.Network.Test.Discovery
{
    [TestFixture]
    public class DiscoveryManagerTests
    {
        private const string TestPrivateKeyHex = "0x3a1076bf45ab87712ad64ccb3b10217737f7faacbf2872e88fdd9a537d8fe266";
        
        private IDiscoveryManager _discoveryManager;
        private IMessageSender _messageSender;
        private INodeTable _nodeTable;
        private INodeFactory _nodeFactory;
        private int _port = 1;
        private string _host = "192.168.1.17";
        private Node[] _nodes;
        private PublicKey _publicKey;

        [SetUp]
        public void Initialize()
        {
            var privateKey = new PrivateKey(new Hex(TestPrivateKeyHex));
            _publicKey = privateKey.PublicKey;
            var logManager = NullLogManager.Instance;
            //var config = new NetworkConfigurationProvider(new NetworkHelper(logger)) { PongTimeout = 100 };
            var config = new JsonConfigProvider();
            config.GetConfig<NetworkConfig>().PongTimeout = 100;

            _messageSender = Substitute.For<IMessageSender>();
            _nodeFactory = new NodeFactory();
            var calculator = new NodeDistanceCalculator(config);

            _nodeTable = new NodeTable(_nodeFactory, new FileKeyStore(config, new JsonSerializer(logManager), new AesEncrypter(config, logManager), new CryptoRandom(), logManager), calculator, config, logManager);
            _nodeTable.Initialize();

            var evictionManager = new EvictionManager(_nodeTable, logManager);
            var lifecycleFactory = new NodeLifecycleManagerFactory(_nodeFactory, _nodeTable, new DiscoveryMessageFactory(config), evictionManager, new NodeStatsProvider(config), config, logManager);

            _nodes = new[] { _nodeFactory.CreateNode("192.168.1.18", 1), _nodeFactory.CreateNode("192.168.1.19", 2) };

            _discoveryManager = new DiscoveryManager(lifecycleFactory, _nodeFactory, _nodeTable, new DiscoveryStorage(config, _nodeFactory, logManager, new PerfService(logManager)), config, logManager);
            _discoveryManager.MessageSender = _messageSender;
        }

        [Test]
        public void OnPingMessageTest()
        {
            //receiving ping
            var address = new IPEndPoint(IPAddress.Parse(_host), _port);
            _discoveryManager.OnIncomingMessage(new PingMessage{ FarAddress = address, FarPublicKey = _publicKey, DestinationAddress = _nodeTable.MasterNode.Address, SourceAddress = address });
            Thread.Sleep(400);

            //expecting to send pong
            _messageSender.Received(1).SendMessage(Arg.Is<PongMessage>(m => m.FarAddress.Address.ToString() == _host && m.FarAddress.Port == _port));

            //expecting to send 3 pings for every new node
            _messageSender.Received(3).SendMessage(Arg.Is<PingMessage>(m => m.FarAddress.Address.ToString() == _host && m.FarAddress.Port == _port));
        }

        [Test]
        public void OnPongMessageTest()
        {
            //receiving pong
            _discoveryManager.OnIncomingMessage(new PongMessage{ FarAddress = new IPEndPoint(IPAddress.Parse(_host), _port), FarPublicKey = _publicKey });
            
            //expecting to activate node as valid peer
            var nodes = _nodeTable.GetClosestNodes();
            Assert.AreEqual(1, nodes.Length);
            var node = nodes.First();
            Assert.AreEqual(_host, node.Host);
            Assert.AreEqual(_port, node.Port);
            var manager = _discoveryManager.GetNodeLifecycleManager(node);
            Assert.AreEqual(NodeLifecycleState.Active, manager.State);
        }

        [Test]
        public void OnFindNodeMessageTest()
        {
            //receiving pong to have a node in the system
            _discoveryManager.OnIncomingMessage(new PongMessage{ FarAddress = new IPEndPoint(IPAddress.Parse(_host), _port), FarPublicKey = _publicKey });

            //expecting to activate node as valid peer
            var nodes = _nodeTable.GetClosestNodes();
            Assert.AreEqual(1, nodes.Length);
            var node = nodes.First();
            Assert.AreEqual(_host, node.Host);
            Assert.AreEqual(_port, node.Port);
            var manager = _discoveryManager.GetNodeLifecycleManager(node);
            Assert.AreEqual(NodeLifecycleState.Active, manager.State);

            //receiving findNode
            _discoveryManager.OnIncomingMessage(new FindNodeMessage{ FarAddress = new IPEndPoint(IPAddress.Parse(_host), _port), FarPublicKey = _publicKey, SearchedNodeId = new PrivateKey(new CryptoRandom().GenerateRandomBytes(32)).PublicKey.Bytes});

            //expecting to respond with sending Neighbors
            _messageSender.Received(1).SendMessage(Arg.Is<NeighborsMessage>(m => m.FarAddress.Address.ToString() == _host && m.FarAddress.Port == _port));
        }

        [Test]
        public void OnNeighborsMessageTest()
        {
            //receiving pong to have a node in the system
            _discoveryManager.OnIncomingMessage(new PongMessage{ FarAddress = new IPEndPoint(IPAddress.Parse(_host), _port), FarPublicKey = _publicKey });

            //expecting to activate node as valid peer
            var nodes = _nodeTable.GetClosestNodes();
            Assert.AreEqual(1, nodes.Length);
            var node = nodes.First();
            Assert.AreEqual(_host, node.Host);
            Assert.AreEqual(_port, node.Port);
            var manager = _discoveryManager.GetNodeLifecycleManager(node);
            Assert.AreEqual(NodeLifecycleState.Active, manager.State);

            //sending FindNode to expect Neighbors
            manager.SendFindNode(_nodeTable.MasterNode.Id.Bytes);
            _messageSender.Received(1).SendMessage(Arg.Is<FindNodeMessage>(m => m.FarAddress.Address.ToString() == _host && m.FarAddress.Port == _port));

            //receiving findNode
            _discoveryManager.OnIncomingMessage(new NeighborsMessage{ FarAddress = new IPEndPoint(IPAddress.Parse(_host), _port), FarPublicKey = _publicKey, Nodes = _nodes});

            //expecting to send 3 pings to both nodes
            Thread.Sleep(600);
            _messageSender.Received(3).SendMessage(Arg.Is<PingMessage>(m => m.FarAddress.Address.ToString() == _nodes[0].Host && m.FarAddress.Port == _nodes[0].Port));
            _messageSender.Received(3).SendMessage(Arg.Is<PingMessage>(m => m.FarAddress.Address.ToString() == _nodes[1].Host && m.FarAddress.Port == _nodes[1].Port));
        }
    }
}