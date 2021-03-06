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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nethermind.Config;
using Nethermind.Core;
using Nethermind.Core.Logging;
using Nethermind.Core.Model;
using Nethermind.Network.Config;
using Nethermind.Network.Discovery.Messages;
using Nethermind.Network.Discovery.RoutingTable;

namespace Nethermind.Network.Discovery
{
    public class NodesLocator : INodesLocator
    {
        private readonly ILogger _logger;
        private readonly INodeTable _nodeTable;
        private readonly IDiscoveryManager _discoveryManager;
        private readonly INetworkConfig _configurationProvider;
        private Node _masterNode;

        public NodesLocator(INodeTable nodeTable, IDiscoveryManager discoveryManager, IConfigProvider configurationProvider, ILogManager logManager)
        {
            _logger = logManager?.GetClassLogger();
            _configurationProvider = configurationProvider.GetConfig<NetworkConfig>();
            _nodeTable = nodeTable;
            _discoveryManager = discoveryManager;
        }

        public void Initialize(Node masterNode)
        {
            _masterNode = masterNode;
        }

        public async Task LocateNodesAsync()
        {
            await LocateNodesAsync(null);
        }

        public async Task LocateNodesAsync(byte[] searchedNodeId)
        {
            var alreadyTriedNodes = new List<string>();
            
            _logger.Info($"Starting location process for node: {(searchedNodeId != null ? new Hex(searchedNodeId).ToString() : "masterNode: " + _masterNode.Id)}");

            for (var i = 0; i < _configurationProvider.MaxDiscoveryRounds; i++)
            {
                Node[] tryCandidates;
                var candTryIndex = 0;
                while (true)
                {
                    //if searched node is not specified master node is used
                    var closestNodes = searchedNodeId != null ? _nodeTable.GetClosestNodes(searchedNodeId) : _nodeTable.GetClosestNodes();
                    tryCandidates = closestNodes.Where(node => !alreadyTriedNodes.Contains(node.IdHashText)).ToArray();
                    if (tryCandidates.Any())
                    {
                        break;
                    }
                    if (candTryIndex > 20)
                    {
                        break;
                    }
                    candTryIndex = candTryIndex + 1;

                    _logger.Debug($"Waiting {_configurationProvider.DiscoveryNewCycleWaitTime} for new nodes");
                    //we need to wait some time for pong messages received from new nodes we reached out to    
                    await Task.Delay(_configurationProvider.DiscoveryNewCycleWaitTime);
                    //Thread.Sleep(_configurationProvider.DiscoveryNewCycleWaitTime);
                }

                if (!tryCandidates.Any())
                {
                    _logger.Debug("No more closer candidates");
                    break;
                }

                var successRequestsCount = 0;
                var failRequestCount = 0;
                var nodesTriedCount = 0;
                while (true)
                {
                    var count = failRequestCount > 0 ? failRequestCount : _configurationProvider.Concurrency;
                    var nodesToSend = tryCandidates.Skip(nodesTriedCount).Take(count).ToArray();
                    if (!nodesToSend.Any())
                    {
                        _logger.Info($"No more nodes to send, sent {successRequestsCount} successfull requests, failedRequestCounter: {failRequestCount}, nodesTriedCounter: {nodesTriedCount}");
                        break;
                    }

                    nodesTriedCount += nodesToSend.Length;
                    alreadyTriedNodes.AddRange(nodesToSend.Select(x => x.IdHashText));

                    var results = await SendFindNode(nodesToSend, searchedNodeId);
                    
                    foreach (var result in results)
                    {
                        if (result.ResultType == ResultType.Failure)
                        {
                            failRequestCount++;
                        }
                        else
                        {
                            successRequestsCount++;
                        }
                    }

                    if (successRequestsCount >= _configurationProvider.Concurrency)
                    {
                        _logger.Info($"Sent {successRequestsCount} successfull requests, failedRequestCounter: {failRequestCount}, nodesTriedCounter: {nodesTriedCount}");
                        break;
                    }
                }
            }
            _logger.Info($"Finished locating nodes, triedNodesCount: {alreadyTriedNodes.Count}");

            LogNodeTable();
        }

        private void LogNodeTable()
        {
            var nonEmptyBuckets = _nodeTable.Buckets.Where(x => x.Items.Any()).ToArray();
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine($"NodeTable, non-empty bucket count: {nonEmptyBuckets.Length}, total items count: {nonEmptyBuckets.Sum(x => x.Items.Count)}");

            foreach (var nodeBucket in nonEmptyBuckets)
            {
                sb.AppendLine($"Bucket: {nodeBucket.Distance}, count: {nodeBucket.Items.Count}");
                foreach (var bucketItem in nodeBucket.Items)
                {
                    sb.AppendLine($"{bucketItem.Node}, LastContactTime: {bucketItem.LastContactTime:yyyy-MM-dd HH:mm:ss:000}");
                }
            }

            sb.AppendLine();
            sb.AppendLine();
            _logger.Info(sb.ToString());
        }

        private async Task<Result[]> SendFindNode(Node[] nodesToSend, byte[] searchedNodeId)
        {
            var sendFindNodeTasks = new List<Task<Result>>();
            foreach (var node in nodesToSend)
            {
                var task = SendFindNode(node, searchedNodeId);
                sendFindNodeTasks.Add(task);
            }

            return await Task.WhenAll(sendFindNodeTasks);
        }

        private async Task<Result> SendFindNode(Node destinationNode, byte[] searchedNodeId)
        {
            return await Task.Run(() => SendFindNodeSync(destinationNode, searchedNodeId));
        }

        private Result SendFindNodeSync(Node destinationNode, byte[] searchedNodeId)
        {
            var nodeManager = _discoveryManager.GetNodeLifecycleManager(destinationNode);
            nodeManager.SendFindNode(searchedNodeId ?? _masterNode.Id.Bytes);

            if (_discoveryManager.WasMessageReceived(destinationNode.IdHashText, MessageType.Neighbors, _configurationProvider.SendNodeTimeout))
            {
                return Result.Success();
            }
            return Result.Fail($"Did not receive Neighbors reponse in time from: {destinationNode.Host}");
        }
    }
}