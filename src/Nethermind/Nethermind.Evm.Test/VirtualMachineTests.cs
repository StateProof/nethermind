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
using System.Diagnostics;
using System.Numerics;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Logging;
using Nethermind.Core.Specs;
using Nethermind.Core.Test.Builders;
using Nethermind.Store;
using NUnit.Framework;

namespace Nethermind.Evm.Test
{
    [TestFixture]
    public class VirtualMachineTests : ITransactionTracer
    {
        public VirtualMachineTests()
        {
            _spec = RopstenSpecProvider.Instance;
            ILogManager logger = NullLogManager.Instance;
            IDb codeDb = new MemDb();
            _stateDb = new SnapshotableDb(new MemDb());
            StateTree stateTree = new StateTree(_stateDb);
            _stateProvider = new StateProvider(stateTree, codeDb, logger);
            _storageDbProvider = new MemDbProvider(logger);
            _storageProvider = new StorageProvider(_storageDbProvider, _stateProvider, logger);
            _ethereumSigner = new EthereumSigner(_spec, logger);
            IBlockhashProvider blockhashProvider = new TestBlockhashProvider();
            IVirtualMachine virtualMachine = new VirtualMachine(_stateProvider, _storageProvider, blockhashProvider, logger);

            _processor = new TransactionProcessor(_spec, _stateProvider, _storageProvider, virtualMachine, this, logger);
        }

        private const long GasLimit = 100000;
        private static BigInteger _blockNumber = 6000000;

        [SetUp]
        public void Setup()
        {
            IsTracingEnabled = false;
            _trace = null;

            _stateDbSnapshot = _stateDb.TakeSnapshot();
            _storageDbSnapshot = _storageDbProvider.TakeSnapshot();
            _stateRoot = _stateProvider.StateRoot;

            _stateProvider.CreateAccount(A, 100.Ether());
            _stateProvider.CreateAccount(B, 100.Ether());

            Metrics.EvmExceptions = 0;
        }

        private int _stateDbSnapshot;
        private int _storageDbSnapshot;
        private Keccak _stateRoot;

        [TearDown]
        public void TearDown()
        {
            _storageProvider.ClearCaches();
            _stateProvider.Reset();
            _stateProvider.StateRoot = _stateRoot;

            _storageDbProvider.Restore(_storageDbSnapshot);
            _stateDb.Restore(_stateDbSnapshot);
        }

        private readonly IEthereumSigner _ethereumSigner;
        private readonly ITransactionProcessor _processor;
        private readonly ISpecProvider _spec;
        private readonly ISnapshotableDb _stateDb;
        private readonly IDbProvider _storageDbProvider;
        private readonly IStateProvider _stateProvider;
        private readonly IStorageProvider _storageProvider;

        private void DeployContract(Address address, params byte[] code)
        {
            _stateProvider.CreateAccount(C, 100.Ether());
            Keccak keccak = _stateProvider.UpdateCode(code);
            _stateProvider.UpdateCodeHash(C, keccak, MainNetSpecProvider.Instance.GetSpec(_blockNumber));
            _stateProvider.Commit(_spec.GenesisSpec);
        }

        private TransactionReceipt Execute(params byte[] code)
        {
            Keccak codeHash = _stateProvider.UpdateCode(code);
            _stateProvider.UpdateCodeHash(TestObject.AddressB, codeHash, _spec.GenesisSpec);

            Transaction transaction = Build.A.Transaction
                .WithGasLimit(GasLimit)
                .WithGasPrice(1)
                .WithTo(TestObject.AddressB)
                .SignedAndResolved(_ethereumSigner, TestObject.PrivateKeyA, _blockNumber)
                .TestObject;

            Assert.AreEqual(A, _ethereumSigner.RecoverAddress(transaction, _blockNumber));

            Block block = Build.A.Block.WithNumber(_blockNumber).TestObject;
            TransactionReceipt receipt = _processor.Execute(transaction, block.Header);
            return receipt;
        }

        private TransactionReceipt[] Execute(params Transaction[] transactions)
        {
            Block block = Build.A.Block.WithNumber(_blockNumber).TestObject;
            TransactionReceipt[] receipts = new TransactionReceipt[transactions.Length];

            for (int i = 0; i < transactions.Length; i++)
            {
                Transaction transaction = transactions[i];
                TransactionReceipt receipt = _processor.Execute(transaction, block.Header);
                receipts[i] = receipt;
            }

            return receipts;
        }

        [Test]
        public void Stop()
        {
            TransactionReceipt receipt = Execute((byte)Instruction.STOP);
            Assert.AreEqual(GasCostOf.Transaction, receipt.GasUsed);
        }

        private static readonly Address A = TestObject.AddressA;
        private static readonly Address B = TestObject.AddressB;
        private static readonly Address C = TestObject.AddressC;

        [Test]
        public void Trace()
        {
            IsTracingEnabled = true;
            Execute(
                (byte)Instruction.PUSH1,
                0,
                (byte)Instruction.PUSH1,
                0,
                (byte)Instruction.ADD,
                (byte)Instruction.PUSH1,
                0,
                (byte)Instruction.SSTORE);

            Assert.AreEqual(5, _trace.Entries.Count, "number of entries");
            TransactionTraceEntry entry = _trace.Entries[1];
            Assert.AreEqual(0, entry.Depth, nameof(entry.Depth));
            Assert.AreEqual(GasLimit - 21000 - GasCostOf.VeryLow, entry.Gas, nameof(entry.Gas));
            Assert.AreEqual(GasCostOf.VeryLow, entry.GasCost, nameof(entry.GasCost));
            Assert.AreEqual(0, entry.Memory.Count, nameof(entry.Memory));
            Assert.AreEqual(1, entry.Stack.Count, nameof(entry.Stack));
            Assert.AreEqual(1, _trace.Entries[4].Storage.Count, nameof(entry.Storage));
            Assert.AreEqual(2, entry.Pc, nameof(entry.Pc));
            Assert.AreEqual("PUSH1", entry.Operation, nameof(entry.Operation));
        }

        [Test]
        public void Add_0_0()
        {
            TransactionReceipt receipt = Execute(
                (byte)Instruction.PUSH1,
                0,
                (byte)Instruction.PUSH1,
                0,
                (byte)Instruction.ADD,
                (byte)Instruction.PUSH1,
                0,
                (byte)Instruction.SSTORE);
            Assert.AreEqual(GasCostOf.Transaction + 4 * GasCostOf.VeryLow + GasCostOf.SReset, receipt.GasUsed, "gas");
            Assert.AreEqual(new byte[] {0}, _storageProvider.Get(new StorageAddress(B, 0)), "storage");
        }

        [Test]
        public void Add_0_1()
        {
            TransactionReceipt receipt = Execute(
                (byte)Instruction.PUSH1,
                0,
                (byte)Instruction.PUSH1,
                1,
                (byte)Instruction.ADD,
                (byte)Instruction.PUSH1,
                0,
                (byte)Instruction.SSTORE);
            Assert.AreEqual(GasCostOf.Transaction + 4 * GasCostOf.VeryLow + GasCostOf.SSet, receipt.GasUsed, "gas");
            Assert.AreEqual(new byte[] {1}, _storageProvider.Get(new StorageAddress(B, 0)), "storage");
        }

        [Test]
        public void Add_1_0()
        {
            TransactionReceipt receipt = Execute(
                (byte)Instruction.PUSH1,
                1,
                (byte)Instruction.PUSH1,
                0,
                (byte)Instruction.ADD,
                (byte)Instruction.PUSH1,
                0,
                (byte)Instruction.SSTORE);
            Assert.AreEqual(GasCostOf.Transaction + 4 * GasCostOf.VeryLow + GasCostOf.SSet, receipt.GasUsed, "gas");
            Assert.AreEqual(new byte[] {1}, _storageProvider.Get(new StorageAddress(B, 0)), "storage");
        }

        [Test]
        public void Mstore()
        {
            TransactionReceipt receipt = Execute(
                (byte)Instruction.PUSH1,
                96, // data
                (byte)Instruction.PUSH1,
                64, // position
                (byte)Instruction.MSTORE);
            Assert.AreEqual(GasCostOf.Transaction + GasCostOf.VeryLow * 3 + GasCostOf.Memory * 3, receipt.GasUsed, "gas");
        }

        [Test]
        public void Mstore_twice_same_location()
        {
            TransactionReceipt receipt = Execute(
                (byte)Instruction.PUSH1,
                96,
                (byte)Instruction.PUSH1,
                64,
                (byte)Instruction.MSTORE,
                (byte)Instruction.PUSH1,
                96,
                (byte)Instruction.PUSH1,
                64,
                (byte)Instruction.MSTORE);
            Assert.AreEqual(GasCostOf.Transaction + GasCostOf.VeryLow * 6 + GasCostOf.Memory * 3, receipt.GasUsed, "gas");
        }

        [Test]
        public void Mload()
        {
            TransactionReceipt receipt = Execute(
                (byte)Instruction.PUSH1,
                64, // position
                (byte)Instruction.MLOAD);
            Assert.AreEqual(GasCostOf.Transaction + GasCostOf.VeryLow * 2 + GasCostOf.Memory * 3, receipt.GasUsed, "gas");
        }

        [Test]
        public void Mload_after_mstore()
        {
            TransactionReceipt receipt = Execute(
                (byte)Instruction.PUSH1,
                96,
                (byte)Instruction.PUSH1,
                64,
                (byte)Instruction.MSTORE,
                (byte)Instruction.PUSH1,
                64,
                (byte)Instruction.MLOAD);
            Assert.AreEqual(GasCostOf.Transaction + GasCostOf.VeryLow * 5 + GasCostOf.Memory * 3, receipt.GasUsed, "gas");
        }

        [Test]
        public void Dup1()
        {
            TransactionReceipt receipt = Execute(
                (byte)Instruction.PUSH1,
                0,
                (byte)Instruction.DUP1);
            Assert.AreEqual(GasCostOf.Transaction + GasCostOf.VeryLow * 2, receipt.GasUsed, "gas");
        }

        [Test]
        public void Codecopy()
        {
            TransactionReceipt receipt = Execute(
                (byte)Instruction.PUSH1,
                32, // length
                (byte)Instruction.PUSH1,
                0, // src
                (byte)Instruction.PUSH1,
                32, // dest
                (byte)Instruction.CODECOPY);
            Assert.AreEqual(GasCostOf.Transaction + GasCostOf.VeryLow * 4 + GasCostOf.Memory * 3, receipt.GasUsed, "gas");
        }

        [Test]
        public void Swap()
        {
            TransactionReceipt receipt = Execute(
                (byte)Instruction.PUSH1,
                32, // length
                (byte)Instruction.PUSH1,
                0, // src
                (byte)Instruction.SWAP1);
            Assert.AreEqual(GasCostOf.Transaction + GasCostOf.VeryLow * 3, receipt.GasUsed, "gas");
        }

        [Test]
        public void Sload()
        {
            TransactionReceipt receipt = Execute(
                (byte)Instruction.PUSH1,
                0, // index
                (byte)Instruction.SLOAD);
            Assert.AreEqual(GasCostOf.Transaction + GasCostOf.VeryLow * 1 + GasCostOf.SLoadEip150, receipt.GasUsed, "gas");
        }

        [Test]
        public void Exp_2_160()
        {
            TransactionReceipt receipt = Execute(
                (byte)Instruction.PUSH1,
                160,
                (byte)Instruction.PUSH1,
                2,
                (byte)Instruction.EXP,
                (byte)Instruction.PUSH1,
                0,
                (byte)Instruction.SSTORE);
            Assert.AreEqual(GasCostOf.Transaction + GasCostOf.VeryLow * 3 + GasCostOf.SSet + GasCostOf.Exp + GasCostOf.ExpByteEip160, receipt.GasUsed, "gas");
            Assert.AreEqual(BigInteger.Pow(2, 160).ToBigEndianByteArray(), _storageProvider.Get(new StorageAddress(B, 0)), "storage");
        }

        [Test]
        public void Exp_0_0()
        {
            TransactionReceipt receipt = Execute(
                (byte)Instruction.PUSH1,
                0,
                (byte)Instruction.PUSH1,
                0,
                (byte)Instruction.EXP,
                (byte)Instruction.PUSH1,
                0,
                (byte)Instruction.SSTORE);
            Assert.AreEqual(GasCostOf.Transaction + GasCostOf.VeryLow * 3 + GasCostOf.Exp + GasCostOf.SSet, receipt.GasUsed, "gas");
            Assert.AreEqual(BigInteger.One.ToBigEndianByteArray(), _storageProvider.Get(new StorageAddress(B, 0)), "storage");
        }

        [Test]
        public void Exp_0_160()
        {
            TransactionReceipt receipt = Execute(
                (byte)Instruction.PUSH1,
                160,
                (byte)Instruction.PUSH1,
                0,
                (byte)Instruction.EXP,
                (byte)Instruction.PUSH1,
                0,
                (byte)Instruction.SSTORE);
            Assert.AreEqual(GasCostOf.Transaction + GasCostOf.VeryLow * 3 + GasCostOf.Exp + GasCostOf.ExpByteEip160 + GasCostOf.SReset, receipt.GasUsed, "gas");
            Assert.AreEqual(BigInteger.Zero.ToBigEndianByteArray(), _storageProvider.Get(new StorageAddress(B, 0)), "storage");
        }

        [Test]
        public void Exp_1_160()
        {
            TransactionReceipt receipt = Execute(
                (byte)Instruction.PUSH1,
                160,
                (byte)Instruction.PUSH1,
                1,
                (byte)Instruction.EXP,
                (byte)Instruction.PUSH1,
                0,
                (byte)Instruction.SSTORE);
            Assert.AreEqual(GasCostOf.Transaction + GasCostOf.VeryLow * 3 + GasCostOf.Exp + GasCostOf.ExpByteEip160 + GasCostOf.SSet, receipt.GasUsed, "gas");
            Assert.AreEqual(BigInteger.One.ToBigEndianByteArray(), _storageProvider.Get(new StorageAddress(B, 0)), "storage");
        }

        [Test]
        public void Sub_0_0()
        {
            TransactionReceipt receipt = Execute(
                (byte)Instruction.PUSH1,
                0,
                (byte)Instruction.PUSH1,
                0,
                (byte)Instruction.SUB,
                (byte)Instruction.PUSH1,
                0,
                (byte)Instruction.SSTORE);
            Assert.AreEqual(GasCostOf.Transaction + GasCostOf.VeryLow * 4 + GasCostOf.SReset, receipt.GasUsed, "gas");
            Assert.AreEqual(new byte[] {0}, _storageProvider.Get(new StorageAddress(B, 0)), "storage");
        }

        [Test]
        public void Not_0()
        {
            TransactionReceipt receipt = Execute(
                (byte)Instruction.PUSH1,
                0,
                (byte)Instruction.NOT,
                (byte)Instruction.PUSH1,
                0,
                (byte)Instruction.SSTORE);
            Assert.AreEqual(GasCostOf.Transaction + GasCostOf.VeryLow * 3 + GasCostOf.SSet, receipt.GasUsed, "gas");
            Assert.AreEqual((BigInteger.Pow(2, 256) - 1).ToBigEndianByteArray(), _storageProvider.Get(new StorageAddress(B, 0)), "storage");
        }

        [Test]
        public void Or_0_0()
        {
            TransactionReceipt receipt = Execute(
                (byte)Instruction.PUSH1,
                0,
                (byte)Instruction.PUSH1,
                0,
                (byte)Instruction.OR,
                (byte)Instruction.PUSH1,
                0,
                (byte)Instruction.SSTORE);
            Assert.AreEqual(GasCostOf.Transaction + GasCostOf.VeryLow * 4 + GasCostOf.SReset, receipt.GasUsed, "gas");
            Assert.AreEqual(BigInteger.Zero.ToBigEndianByteArray(), _storageProvider.Get(new StorageAddress(B, 0)), "storage");
        }

        [Test]
        public void Sstore_twice_0_same_storage_should_refund_only_once()
        {
            TransactionReceipt receipt = Execute(
                (byte)Instruction.PUSH1,
                0,
                (byte)Instruction.PUSH1,
                0,
                (byte)Instruction.SSTORE);
            Assert.AreEqual(GasCostOf.Transaction + GasCostOf.VeryLow * 2 + GasCostOf.SReset, receipt.GasUsed, "gas");
            Assert.AreEqual(BigInteger.Zero.ToBigEndianByteArray(), _storageProvider.Get(new StorageAddress(B, 0)), "storage");
        }

        // Steo - modified at the workshop
        [Test]
        public void Stack_underflow()
        {
            IsTracingEnabled = true;  // tracing
            TransactionReceipt rcp = Execute((byte)Instruction.POP);
            Assert.AreEqual(StatusCode.Failure, rcp.StatusCode);
            Assert.AreEqual(GasLimit, rcp.GasUsed);
            OutputTrace(); // tracing 
            // if the transaction fails, all the gas is gone.. 
        }

        // Steo - modified at the workshop
        [Test]
        public void Stack_overflow()
        {
            IsTracingEnabled = true;
            TransactionReceipt rcp = Execute(
                (byte)Instruction.JUMPDEST,
                (byte)Instruction.PUSH1,
                0,
                (byte)Instruction.PUSH1,
                0,
                (byte)Instruction.JUMP
            );
            OutputTrace();
            Assert.AreEqual(StatusCode.Failure, rcp.StatusCode);
            Assert.AreEqual(GasLimit, rcp.GasUsed);
        }

        [Test]
        public void Invalid_jump()
        {
            throw new NotImplementedException();
        }

        [Test]
        public void Invalid_op()
        {
            throw new NotImplementedException();
        }

        // Steo - modified at the workshop
        [Test]
        public void Deploy_contract_call_static_call_violation()
        {
            IsTracingEnabled = true;
            Transaction a = Build.A.Transaction
                .WithInit(new byte[]  // Init a transaction, run the code, give back the code that will be 
                {
                    // fill it
                    (byte)Instruction.PUSH5,   // our code    (5 byte)
                    // actual code of the smart contract
                    (byte)Instruction.PUSH1,   // the address (1 byte)
                    1,
                    (byte)Instruction.PUSH1,
                    1,
                    (byte)Instruction.SSTORE,    // take 2 values, address and value and store 
                    // end of our smart contract code (push 1 push 1, store)
                    (byte)Instruction.PUSH1,
                    0,                          // at this point I have 2 elem in the stack: the code (result of SSTORE) and an address (=0)
                    (byte)Instruction.MSTORE,    // store in memory
                    (byte)Instruction.PUSH1,
                    1,
                    (byte)Instruction.RETURN
                })
                .SignedAndResolved(_ethereumSigner, TestObject.PrivateKeyA, _blockNumber)
                .WithGasLimit(GasLimit)
                .TestObject;

            var receiptsA = Execute(a);
            OutputTrace();
            
            Assert.AreEqual(StatusCode.Success, receiptsA[0].StatusCode, "A");
            Assert.True(_stateProvider.AccountExists(_expectedContractAddressA), "deployed");
            Assert.AreNotEqual(Keccak.OfAnEmptyString, _stateProvider.GetCodeHash(_expectedContractAddressA), "code A");
            
            Transaction b = Build.A.Transaction
                .WithInit(new byte[]
                {
                    // fill it
                })
                .SignedAndResolved(_ethereumSigner, TestObject.PrivateKeyA, _blockNumber)
                .WithGasLimit(GasLimit)
                .WithNonce(1)
                .TestObject;

            var receiptsB = Execute(b);
            OutputTrace();
            
            Assert.AreEqual(StatusCode.Success, receiptsB[0].StatusCode, "B");
            Assert.True(_stateProvider.AccountExists(_expectedContractAddressB), "deployed B");
            Assert.AreNotEqual(Keccak.OfAnEmptyString, _stateProvider.GetCodeHash(_expectedContractAddressB), "code B");
            
            Transaction c = Build.A.Transaction
                .WithTo(_expectedContractAddressB)
                .WithData(new byte[]{})
                .SignedAndResolved(_ethereumSigner, TestObject.PrivateKeyA, _blockNumber)
                .WithGasLimit(GasLimit)
                .WithNonce(2)
                .TestObject;
            
            var receiptsC = Execute(c);
            OutputTrace();
            
            Assert.AreEqual(StatusCode.Success, receiptsC[0].StatusCode, "C");
            Assert.Greater(receiptsC[0].GasUsed, GasCostOf.Transaction + 20000, "gas used C");
        }

        private Address _expectedContractAddressA = new Address("0x24cd2edba056b7c654a50e8201b619d4f624fdda");
        private Address _expectedContractAddressB = new Address("0xdc98b4d0af603b4fb5ccdd840406a0210e5deff8");

        private TransactionTrace _trace;

        public bool IsTracingEnabled { get; private set; }

        public void SaveTrace(Keccak hash, TransactionTrace trace)
        {
            _trace = trace;
        }

        private void OutputTrace()
        {
            string text = new UnforgivingJsonSerializer().Serialize(_trace, true);
            Console.WriteLine(text);
        }

        [Test]
        public void Ropsten_attack_contract_test()
        {
            throw new NotImplementedException();
//PUSH1 0x60
//PUSH1 0x40
//MSTORE
//PUSH4 0xffffffff
//PUSH1 0xe0
//PUSH1 0x02
//EXP
//PUSH1 0x00
//CALLDATALOAD
//DIV
//AND
//PUSH4 0x9fe12a6a
//DUP2
//EQ
//PUSH1 0x22
//JUMPI
//JUMPDEST
//PUSH1 0x00
//JUMP
//JUMPDEST
//CALLVALUE
//PUSH1 0x00
//JUMPI
//PUSH1 0x38
//PUSH1 0x04
//CALLDATALOAD
//PUSH1 0x24
//CALLDATALOAD
//PUSH1 0xff
//PUSH1 0x44
//CALLDATALOAD
//AND
//PUSH1 0x3a
//JUMP
//JUMPDEST
//STOP
//JUMPDEST
//PUSH1 0x40
//DUP1
//MLOAD
//PUSH1 0xff
//DUP4
//AND
//DUP2
//MSTORE
//SWAP1
//MLOAD
//DUP4
//SWAP2
//DUP6
//SWAP2
//PUSH32 0x2f554056349a3530a4cabe3891d711b94a109411500421e48fc5256d660d7a79
//SWAP2
//DUP2
//SWAP1
//SUB
//PUSH1 0x20
//ADD
//SWAP1
//LOG3
//JUMPDEST
//POP
//POP
//POP
//JUMP
//STOP
        }
    }
}