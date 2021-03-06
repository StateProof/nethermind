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
using System.Diagnostics;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethermind.Blockchain.Difficulty;
using Nethermind.Blockchain.Validators;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Logging;
using Nethermind.Core.Model;
using Nethermind.Core.Specs;
using Nethermind.Core.Test.Builders;
using NSubstitute;
using NUnit.Framework;

namespace Nethermind.Blockchain.Test
{
    [TestFixture]
    public class SynchronizationManagerTests
    {
        [SetUp]
        public void Setup()
        {
            _genesisBlock = Build.A.Block.WithNumber(0).TestObject;
            _blockTree = Build.A.BlockTree(_genesisBlock).OfChainLength(1).TestObject;
            
            IHeaderValidator headerValidator = Build.A.HeaderValidator.ThatAlwaysReturnsTrue.TestObject;
            IBlockValidator blockValidator = Build.A.BlockValidator.ThatAlwaysReturnsTrue.TestObject;
            ITransactionValidator transactionValidator = Build.A.TransactionValidator.ThatAlwaysReturnsTrue.TestObject;
            
            _manager = new SynchronizationManager(_blockTree, blockValidator, headerValidator, new TransactionStore(), transactionValidator, NullLogger.Instance, new BlockchainConfig());
        }

        private IBlockTree _blockTree;
        private IBlockTree _remoteBlockTree;
        private Block _genesisBlock;
        private SynchronizationManager _manager;

        [Test]
        public async Task Retrieves_missing_blocks_in_batches()
        {
            _remoteBlockTree = Build.A.BlockTree(_genesisBlock).OfChainLength(SynchronizationManager.BatchSize * 2).TestObject;
            ISynchronizationPeer peer = new SynchronizationPeerMock(_remoteBlockTree);
            
            ManualResetEvent resetEvent = new ManualResetEvent(false);
            _manager.Synced += (sender, args) => { resetEvent.Set(); };
            Task addPeerTask = _manager.AddPeer(peer);
            Task firstToComplete = await Task.WhenAny(addPeerTask, Task.Delay(2000));
            Assert.AreSame(addPeerTask, firstToComplete);
            _manager.Start();
            resetEvent.WaitOne(TimeSpan.FromMilliseconds(2000));
            Assert.AreEqual(SynchronizationManager.BatchSize * 2 - 1, (int)_blockTree.BestSuggested.Number);
        }
        
        [Test]
        public async Task Syncs_with_empty_peer()
        {
            _remoteBlockTree = Build.A.BlockTree(_genesisBlock).OfChainLength(0).TestObject;
            ISynchronizationPeer peer = new SynchronizationPeerMock(_remoteBlockTree);
            
            ManualResetEvent resetEvent = new ManualResetEvent(false);
            _manager.Synced += (sender, args) => { resetEvent.Set(); };
            Task addPeerTask = _manager.AddPeer(peer);
            Task firstToComplete = await Task.WhenAny(addPeerTask, Task.Delay(2000));
            Assert.AreSame(addPeerTask, firstToComplete);
            _manager.Start();
            resetEvent.WaitOne(TimeSpan.FromMilliseconds(2000));
            Assert.AreEqual(0, (int)_blockTree.BestSuggested.Number);
        }
        
        [Test]
        public async Task Syncs_when_knows_more_blocks()
        {
            _blockTree = Build.A.BlockTree(_genesisBlock).OfChainLength(SynchronizationManager.BatchSize * 2).TestObject;
            _remoteBlockTree = Build.A.BlockTree(_genesisBlock).OfChainLength(0).TestObject;
            ISynchronizationPeer peer = new SynchronizationPeerMock(_remoteBlockTree);
            
            ManualResetEvent resetEvent = new ManualResetEvent(false);
            _manager.Synced += (sender, args) => { resetEvent.Set(); };
            Task addPeerTask = _manager.AddPeer(peer);
            Task firstToComplete = await Task.WhenAny(addPeerTask, Task.Delay(2000));
            Assert.AreSame(addPeerTask, firstToComplete);
            _manager.Start();
            resetEvent.WaitOne(TimeSpan.FromMilliseconds(2000));
            Assert.AreEqual(SynchronizationManager.BatchSize * 2 - 1, (int)_blockTree.BestSuggested.Number);
        }
        
        [Test]
        public async Task Can_resync_if_missed_a_block()
        {
            _remoteBlockTree = Build.A.BlockTree(_genesisBlock).OfChainLength(SynchronizationManager.BatchSize).TestObject;
            ISynchronizationPeer peer = new SynchronizationPeerMock(_remoteBlockTree);
            
            ManualResetEvent resetEvent = new ManualResetEvent(false);
            _manager.Synced += (sender, args) => { resetEvent.Set(); };
            Task addPeerTask = _manager.AddPeer(peer);
            Task firstToComplete = await Task.WhenAny(addPeerTask, Task.Delay(2000));
            Assert.AreSame(addPeerTask, firstToComplete);
            _manager.Start();
            resetEvent.WaitOne(TimeSpan.FromMilliseconds(2000));

            BlockTreeBuilder.ExtendTree(_remoteBlockTree, SynchronizationManager.BatchSize * 2);
            _manager.AddNewBlock(_remoteBlockTree.RetrieveHeadBlock(), peer.NodeId);
            
            Assert.AreEqual(SynchronizationManager.BatchSize * 2 - 1, (int)_blockTree.BestSuggested.Number);
        }
        
        [Test]
        public async Task Can_add_new_block()
        {
            _remoteBlockTree = Build.A.BlockTree(_genesisBlock).OfChainLength(SynchronizationManager.BatchSize).TestObject;
            ISynchronizationPeer peer = new SynchronizationPeerMock(_remoteBlockTree);
            
            ManualResetEvent resetEvent = new ManualResetEvent(false);
            _manager.Synced += (sender, args) => { resetEvent.Set(); };
            Task addPeerTask = _manager.AddPeer(peer);
            Task firstToComplete = await Task.WhenAny(addPeerTask, Task.Delay(2000));
            Assert.AreSame(addPeerTask, firstToComplete);
            _manager.Start();
            resetEvent.WaitOne(TimeSpan.FromMilliseconds(2000));

            Block block = Build.A.Block.WithParent(_remoteBlockTree.Head).TestObject;
            _manager.AddNewBlock(block, peer.NodeId);
            
            Assert.AreEqual(SynchronizationManager.BatchSize, (int)_blockTree.BestSuggested.Number);
        }
        
        [Test]
        public async Task Can_sync_on_split_of_length_1()
        {
            BlockTree miner1Tree = Build.A.BlockTree(_genesisBlock).OfChainLength(6).TestObject;
            ISynchronizationPeer miner1 = new SynchronizationPeerMock(miner1Tree);
            
            ManualResetEvent resetEvent = new ManualResetEvent(false);
            _manager.Synced += (sender, args) => { resetEvent.Set(); };
            
            Task addMiner1Task = _manager.AddPeer(miner1);
            
            await Task.WhenAll(addMiner1Task);

            resetEvent.WaitOne(TimeSpan.FromSeconds(1));

            Assert.AreEqual(miner1Tree.BestSuggested.Hash, _manager.BlockTree.BestSuggested.Hash, "client agrees with miner before split");
            
            Block splitBlock = Build.A.Block.WithParent(miner1Tree.FindParent(miner1Tree.Head)).WithDifficulty(miner1Tree.Head.Difficulty - 1).TestObject;
            Block splitBlockChild = Build.A.Block.WithParent(splitBlock).TestObject;

            miner1Tree.SuggestBlock(splitBlock);
            miner1Tree.MarkAsProcessed(splitBlock.Hash);
            miner1Tree.MoveToMain(splitBlock.Hash);
            miner1Tree.SuggestBlock(splitBlockChild);
            miner1Tree.MarkAsProcessed(splitBlockChild.Hash);
            miner1Tree.MoveToMain(splitBlockChild.Hash);

            Assert.AreEqual(splitBlockChild.Hash, miner1Tree.BestSuggested.Hash, "split as expected");
            
            resetEvent.Reset();
            
            _manager.AddNewBlock(splitBlockChild, miner1.NodeId);
            
            resetEvent.WaitOne(TimeSpan.FromSeconds(1));
            
            Assert.AreEqual(miner1Tree.BestSuggested.Hash, _manager.BlockTree.BestSuggested.Hash, "client agrees with miner after split");
        }
        
        [Test]
        public async Task Can_sync_on_split_of_length_6()
        {
            BlockTree miner1Tree = Build.A.BlockTree(_genesisBlock).OfChainLength(6).TestObject;
            ISynchronizationPeer miner1 = new SynchronizationPeerMock(miner1Tree);
            
            ManualResetEvent resetEvent = new ManualResetEvent(false);
            _manager.Synced += (sender, args) => { resetEvent.Set(); };
            
            Task addMiner1Task = _manager.AddPeer(miner1);
            
            await Task.WhenAll(addMiner1Task);

            resetEvent.WaitOne(TimeSpan.FromSeconds(1));

            Assert.AreEqual(miner1Tree.BestSuggested.Hash, _manager.BlockTree.BestSuggested.Hash, "client agrees with miner before split");
            
            miner1Tree.AddBranch(7, 0, 1);
            
            Assert.AreNotEqual(miner1Tree.BestSuggested.Hash, _manager.BlockTree.BestSuggested.Hash, "client does not agree with miner after split");
            
            resetEvent.Reset();
            
            _manager.AddNewBlock(miner1Tree.RetrieveHeadBlock(), miner1.NodeId);
            
            resetEvent.WaitOne(TimeSpan.FromSeconds(1));
            
            Assert.AreEqual(miner1Tree.BestSuggested.Hash, _manager.BlockTree.BestSuggested.Hash, "client agrees with miner after split");
        }
        
        [Test]
        public async Task Does_not_do_full_sync_when_not_needed()
        {
            BlockTree minerTree = Build.A.BlockTree(_genesisBlock).OfChainLength(6).TestObject;
            ISynchronizationPeer miner1 = new SynchronizationPeerMock(minerTree);
            
            ManualResetEvent resetEvent = new ManualResetEvent(false);
            _manager.Synced += (sender, args) => { resetEvent.Set(); };
            
            Task addMiner1Task = _manager.AddPeer(miner1);
            
            await Task.WhenAll(addMiner1Task);

            resetEvent.WaitOne(TimeSpan.FromSeconds(1));

            Assert.AreEqual(minerTree.BestSuggested.Hash, _manager.BlockTree.BestSuggested.Hash, "client agrees with miner before split");

            Block newBlock = Build.A.Block.WithParent(minerTree.Head).TestObject;
            minerTree.SuggestBlock(newBlock);
            minerTree.MarkAsProcessed(newBlock.Hash);
            minerTree.MoveToMain(newBlock.Hash);
            
            ISynchronizationPeer miner2 = Substitute.For<ISynchronizationPeer>();
            miner2.GetHeadBlockNumber(Arg.Any<CancellationToken>()).Returns(miner1.GetHeadBlockNumber(Arg.Any<CancellationToken>()));
            miner2.GetHeadBlockHash().Returns(miner1.GetHeadBlockHash());
            miner2.NodeId.Returns(new NodeId(TestObject.PublicKeyB));
            
            Assert.AreEqual(newBlock.Number, await miner2.GetHeadBlockNumber(Arg.Any<CancellationToken>()), "number as expected");
            Assert.AreEqual(newBlock.Hash, await miner2.GetHeadBlockHash(), "hash as expected");
            
            await _manager.AddPeer(miner2);

            await miner2.Received().GetBlockHeaders(6, 1, 0, default(CancellationToken));
        }
        
        [Test]
        public async Task Does_not_do_full_sync_when_not_needed_with_split()
        {
            BlockTree minerTree = Build.A.BlockTree(_genesisBlock).OfChainLength(6).TestObject;
            ISynchronizationPeer miner1 = new SynchronizationPeerMock(minerTree);
            
            ManualResetEvent resetEvent = new ManualResetEvent(false);
            _manager.Synced += (sender, args) => { resetEvent.Set(); };
            
            Task addMiner1Task = _manager.AddPeer(miner1);
            
            await Task.WhenAll(addMiner1Task);

            resetEvent.WaitOne(TimeSpan.FromSeconds(1));

            Assert.AreEqual(minerTree.BestSuggested.Hash, _manager.BlockTree.BestSuggested.Hash, "client agrees with miner before split");

            Block newBlock = Build.A.Block.WithParent(minerTree.Head).TestObject;
            minerTree.SuggestBlock(newBlock);
            minerTree.MarkAsProcessed(newBlock.Hash);
            minerTree.MoveToMain(newBlock.Hash);
            
            ISynchronizationPeer miner2 = Substitute.For<ISynchronizationPeer>();
            miner2.GetHeadBlockNumber(Arg.Any<CancellationToken>()).Returns(miner1.GetHeadBlockNumber(Arg.Any<CancellationToken>()));
            miner2.GetHeadBlockHash().Returns(miner1.GetHeadBlockHash());
            miner2.NodeId.Returns(new NodeId(TestObject.PublicKeyB));
            
            Assert.AreEqual(newBlock.Number, await miner2.GetHeadBlockNumber(Arg.Any<CancellationToken>()), "number as expected");
            Assert.AreEqual(newBlock.Hash, await miner2.GetHeadBlockHash(), "hash as expected");
            
            await _manager.AddPeer(miner2);

            await miner2.Received().GetBlockHeaders(6, 1, 0, default(CancellationToken));
        }
    }
}