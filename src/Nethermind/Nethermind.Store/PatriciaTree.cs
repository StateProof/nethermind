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
using System.Diagnostics;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Encoding;
using Nethermind.Core.Extensions;

namespace Nethermind.Store
{
    // TODO: I guess it is a very slow and Keccak-heavy implementation, the first one to pass tests
    [DebuggerDisplay("{RootHash}")]
    public class PatriciaTree
    {
        /// <summary>
        ///     0x56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421
        /// </summary>
        public static readonly Keccak EmptyTreeHash = Keccak.EmptyTreeHash;

        private readonly IDb _db;

        internal Node Root;

        public PatriciaTree()
            : this(new MemDb(), EmptyTreeHash)
        {
        }

        public PatriciaTree(IDb db)
            : this(db, EmptyTreeHash)
        {
        }

        public PatriciaTree(IDb db, Keccak rootHash)
        {
            _db = db;
            RootHash = rootHash;
        }

        private Keccak _rootHash;

        public Keccak RootHash
        {
            get => _rootHash;
            set
            {
                _rootHash = value;
                if (_rootHash == Keccak.EmptyTreeHash)
                {
                    Root = null;
                }
                else
                {
                    Rlp rootRlp = new Rlp(_db[_rootHash.Bytes]);
                    Root = RlpDecode(rootRlp); // TODO: needed?
                }
            }
        }

        private static Rlp RlpEncode(KeccakOrRlp keccakOrRlp)
        {
            return keccakOrRlp == null ? Rlp.OfEmptyByteArray : keccakOrRlp.GetOrEncodeRlp();
        }

        private static Rlp RlpEncode(Node node)
        {
            StoreMetrics.TreeNodeRlpEncodings++;

            if (node is LeafNode leaf)
            {
                Rlp result = Rlp.Encode(leaf.Key.ToBytes(), leaf.Value);
                return result;
            }

            if (node is BranchNode branch)
            {
                // Geth encoded a structure of nodes so child nodes are actual objects and not RLP of items,
                // hence when RLP encoding nodes are not byte arrays but actual objects of format byte[][2] or their Keccak
                Rlp result = Rlp.Encode(
                    RlpEncode(branch.Nodes[0x0]),
                    RlpEncode(branch.Nodes[0x1]),
                    RlpEncode(branch.Nodes[0x2]),
                    RlpEncode(branch.Nodes[0x3]),
                    RlpEncode(branch.Nodes[0x4]),
                    RlpEncode(branch.Nodes[0x5]),
                    RlpEncode(branch.Nodes[0x6]),
                    RlpEncode(branch.Nodes[0x7]),
                    RlpEncode(branch.Nodes[0x8]),
                    RlpEncode(branch.Nodes[0x9]),
                    RlpEncode(branch.Nodes[0xa]),
                    RlpEncode(branch.Nodes[0xb]),
                    RlpEncode(branch.Nodes[0xc]),
                    RlpEncode(branch.Nodes[0xd]),
                    RlpEncode(branch.Nodes[0xe]),
                    RlpEncode(branch.Nodes[0xf]),
                    Rlp.Encode(branch.Value ?? new byte[0]));
                return result;
            }

            if (node is ExtensionNode extension)
            {
                return Rlp.Encode(extension.Key.ToBytes(), RlpEncode(extension.NextNode));
            }

            throw new InvalidOperationException("Unknown node type");
        }
        
        internal static Node RlpDecode(Rlp bytes)
        {
            StoreMetrics.TreeNodeRlpDecodings++;
            NewRlp.DecoderContext context = bytes.Bytes.AsRlpContext();

            context.ReadSequenceLength();
            int numberOfItems = context.ReadNumberOfItemsRemaining();

            Node result;
            if (numberOfItems == 17)
            {
                BranchNode branch = new BranchNode();
                for (int i = 0; i < 16; i++)
                {
                    branch.Nodes[i] = DecodeChildNode(context);
                }

                branch.Value = context.ReadByteArray();
                result = branch;
            }
            else if (numberOfItems == 2)
            {
                HexPrefix key = HexPrefix.FromBytes(context.ReadByteArray());
                bool isExtension = key.IsExtension;
                if (isExtension)
                {
                    ExtensionNode extension = new ExtensionNode();
                    extension.Key = key;
                    extension.NextNode = DecodeChildNode(context);
                    result = extension;
                }
                else
                {
                    LeafNode leaf = new LeafNode();
                    leaf.Key = key;
                    leaf.Value = context.ReadByteArray();
                    result = leaf;
                }
            }
            else
            {
                throw new InvalidOperationException($"Unexpected number of items = {numberOfItems} when decoding a node");
            }

            return result;
        }

        private static KeccakOrRlp DecodeChildNode(NewRlp.DecoderContext decoderContext)
        {
            if (decoderContext.IsSequenceNext())
            {
                return new KeccakOrRlp(new Rlp(decoderContext.ReadSequenceRlp()));
            }

            byte[] bytes = decoderContext.ReadByteArray();
            return bytes.Length == 0 ? null : new KeccakOrRlp(new Keccak(bytes));
        }

        public void Set(Nibble[] nibbles, Rlp rlp)
        {
            Set(nibbles, rlp.Bytes);
        }

        public virtual void Set(Nibble[] nibbles, byte[] value)
        {
            new TreeOperation(this, nibbles, value, true).Run();
        }

        public byte[] Get(byte[] rawKey)
        {
            return new TreeOperation(this, Nibbles.FromBytes(rawKey), null, false).Run();
        }

        public void Set(byte[] rawKey, byte[] value)
        {
            Set(Nibbles.FromBytes(rawKey), value);
        }

        public void Set(byte[] rawKey, Rlp value)
        {
            Set(Nibbles.FromBytes(rawKey), value == null ? new byte[0] : value.Bytes);
        }

        internal Node GetNode(KeccakOrRlp keccakOrRlp)
        {
            Rlp rlp = null;
            try
            {
                rlp = new Rlp(keccakOrRlp.IsKeccak ? _db[keccakOrRlp.GetOrComputeKeccak().Bytes] : keccakOrRlp.Bytes);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            return RlpDecode(rlp);
        }

        // TODO: this would only be needed with pruning?
        internal void DeleteNode(KeccakOrRlp hash, bool ignoreChildren = false)
        {
//            if (hash == null || !hash.IsKeccak)
//            {
//                return;
//            }
//
//            Keccak thisNodeKeccak = hash.GetOrComputeKeccak();
//            Node node = ignoreChildren ? null : RlpDecode(new Rlp(_db[thisNodeKeccak]));
//            _db.Remove(thisNodeKeccak);
//
//            if (ignoreChildren)
//            {
//                return;
//            }
//
//            if (node is ExtensionNode extension)
//            {
//                DeleteNode(extension.NextNode, true);
//                _db.Remove(hash.GetOrComputeKeccak());
//            }
//
//            if (node is BranchNode branch)
//            {
//                foreach (KeccakOrRlp subnode in branch.Nodes)
//                {
//                    DeleteNode(subnode, true);
//                }
//            }
        }

        internal KeccakOrRlp StoreNode(Node node, bool isRoot = false)
        {
            if (isRoot && node == null)
            {
//                DeleteNode(new KeccakOrRlp(RootHash));
                Root = null;
//                _db.Remove(RootHash);
                RootHash = EmptyTreeHash;
                return new KeccakOrRlp(EmptyTreeHash);
            }

            if (node == null)
            {
                return null;
            }

            Rlp rlp = RlpEncode(node);
            KeccakOrRlp key = new KeccakOrRlp(rlp);
            if (key.IsKeccak || isRoot)
            {
                Keccak keyKeccak = key.GetOrComputeKeccak();
                _db[keyKeccak.Bytes] = rlp.Bytes;

                if (isRoot)
                {
                    Root = node;
                    RootHash = keyKeccak;
                }
            }

            return key;
        }
    }
}