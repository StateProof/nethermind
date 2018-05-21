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
using Nethermind.Core;
using Nethermind.Core.Extensions;

namespace Nethermind.Store
{
    public class TreeOperation
    {
        private readonly PatriciaTree _tree;
        private readonly byte[] _updatePath;
        private readonly byte[] _updateValue;
        private readonly bool _isUpdate;
        private readonly bool _ignoreMissingDelete;

        private readonly Stack<StackedNode> _nodeStack = new Stack<StackedNode>();

        public TreeOperation(PatriciaTree tree, Nibble[] updatePath, byte[] updateValue, bool isUpdate, bool ignoreMissingDelete = true)
        {
            _tree = tree;
            _updatePath = updatePath.ToLooseByteArray();
            if (isUpdate)
            {
                _updateValue = updateValue.Length == 0 ? null : updateValue;
            }

            _isUpdate = isUpdate;
            _ignoreMissingDelete = ignoreMissingDelete;
        }

        private int _currentIndex;

        public byte[] Run()
        {
            if (_tree.Root == null)
            {
                if (!_isUpdate || _updateValue == null)
                {
                    return null;
                }

                LeafNode leafNode = new LeafNode(new HexPrefix(true, _updatePath), _updateValue);
                leafNode.IsDirty = true;
                _tree.Root = leafNode; 
                return _updateValue;
            }

            return TraverseNode(_tree.Root);
        }

        private byte[] TraverseNode(Node node)
        {
            if (node is KeccakOrRlp keccakOrRlp)
            {
                Node resolvedNode = _tree.GetNode(keccakOrRlp);
                return TraverseNode(resolvedNode);
            }

            if (node is LeafNode leaf)
            {
                return TraverseLeaf(leaf);
            }

            if (node is BranchNode branch)
            {
                return TraverseBranch(branch);
            }

            if (node is ExtensionNode extension)
            {
                return TraverseExtension(extension);
            }

            throw new NotImplementedException($"Unknown node type {node.GetType().Name}");
        }

        private int RemainingUpdatePathLength => _updatePath.Length - _currentIndex;

        private byte[] RemainingUpdatePath => _updatePath.Slice(_currentIndex, RemainingUpdatePathLength);

        private class StackedNode
        {
            public StackedNode(Node node, int pathIndex)
            {
                Node = node;
                PathIndex = pathIndex;
            }

            public Node Node { get; }
            public int PathIndex { get; }
        }

        private void UpdateHashes(Node node)
        {
//            Keccak previousRootHash = _tree.RootHash;

            bool isRoot = _nodeStack.Count == 0;
            Node nextNode = node;

            // nodes should immutable here I guess
            while (!isRoot)
            {
                StackedNode parentOnStack = _nodeStack.Pop();
                node = parentOnStack.Node;

                isRoot = _nodeStack.Count == 0;

                if (node is LeafNode leaf)
                {
                    throw new InvalidOperationException($"Leaf {leaf} cannot be a parent of another node");
                }

                if (node is BranchNode branch)
                {
//                    _tree.DeleteNode(branch.Nodes[parentOnStack.PathIndex], true);
                    branch.Nodes[parentOnStack.PathIndex] = nextNode;
                    branch.IsDirty = true;
                    if (branch.IsValid)
                    {
                        nextNode = branch;
                    }
                    else
                    {
                        if (branch.Value.Length != 0)
                        {
                            LeafNode leafFromBranch = new LeafNode(new HexPrefix(true), branch.Value);
                            leafFromBranch.IsDirty = true;
                            nextNode = leafFromBranch;
                        }
                        else
                        {
                            int childNodeIndex = Array.FindIndex(branch.Nodes, n => n != null);
                            Node childNode = branch.Nodes[childNodeIndex];
                            if (childNode == null)
                            {
                                throw new InvalidOperationException("Before updating branch should have had at least two non-empty children");
                            }

                            // need to restore this node now?
                            if (childNode is BranchNode)
                            {
                                ExtensionNode extensionFromBranch = new ExtensionNode(new HexPrefix(false, (byte)childNodeIndex), childNode);
                                extensionFromBranch.IsDirty = true;
                                nextNode = extensionFromBranch;
                            }
                            else if (childNode is ExtensionNode childExtension)
                            {
//                                _tree.DeleteNode(childNodeHash, true);
                                ExtensionNode extensionFromBranch = new ExtensionNode(new HexPrefix(false, Bytes.Concat((byte)childNodeIndex, childExtension.Path)), childExtension.NextNode);
                                extensionFromBranch.IsDirty = true;
                                nextNode = extensionFromBranch;
                            }
                            else if (childNode is LeafNode childLeaf)
                            {
//                                _tree.DeleteNode(childNodeHash, true);
                                LeafNode leafFromBranch = new LeafNode(new HexPrefix(true, Bytes.Concat((byte)childNodeIndex, childLeaf.Path)), childLeaf.Value);
                                leafFromBranch.IsDirty = true;
                                nextNode = leafFromBranch;
                            }
                            else
                            {
                                throw new InvalidOperationException($"Unknown node type {nextNode.GetType().Name}");
                            }
                        }
                    }
                }
                else if (node is ExtensionNode extension)
                {
//                    _tree.DeleteNode(extension.NextNode, true);
                    if (nextNode is LeafNode childLeaf)
                    {
                        LeafNode leafFromExtension = new LeafNode(new HexPrefix(true, Bytes.Concat(extension.Path, childLeaf.Path)), childLeaf.Value);
                        leafFromExtension.IsDirty = true;
                        nextNode = leafFromExtension;
                    }
                    else if (nextNode is ExtensionNode childExtension)
                    {
                        ExtensionNode extensionFromExtension = new ExtensionNode(new HexPrefix(false, Bytes.Concat(extension.Path, childExtension.Path)), childExtension.NextNode);
                        extensionFromExtension.IsDirty = true;
                        nextNode = extensionFromExtension;
                    }
                    else if (nextNode is BranchNode)
                    {
                        extension.NextNode = nextNode;
                        extension.IsDirty = true;
                        nextNode = extension;
                    }
                    else
                    {
                        throw new InvalidOperationException($"Unknown node type {nextNode.GetType().Name}");
                    }
                }
                else
                {
                    throw new InvalidOperationException($"Unknown node type {node.GetType().Name}");
                }
            }

            _tree.Root = nextNode;

//            _tree.DeleteNode(new KeccakOrRlp(previousRootHash), true);
        }

        private byte[] TraverseBranch(BranchNode node)
        {
            if (RemainingUpdatePathLength == 0)
            {
                if (!_isUpdate)
                {
                    return node.Value;
                }

                // or delete (null update)
                if (_updateValue == null)
                {
                    UpdateHashes(null);
                }
                else
                {
                    node.IsDirty = true;
                    node.Value = _updateValue;
                    UpdateHashes(node);
                }

                return _updateValue;
            }

            Node nextNode = node.Nodes[_updatePath[_currentIndex]];
            _nodeStack.Push(new StackedNode(node, _updatePath[_currentIndex]));
            _currentIndex++;

            if (nextNode == null)
            {
                if (!_isUpdate)
                {
                    return null;
                }

                if (_updateValue == null)
                {
                    if (_ignoreMissingDelete)
                    {
                        return null;
                    }

                    throw new InvalidOperationException($"Could not find the leaf node to delete: {Hex.FromBytes(_updatePath, false)}");
                }

                byte[] leafPath = _updatePath.Slice(_currentIndex, _updatePath.Length - _currentIndex);
                LeafNode leaf = new LeafNode(new HexPrefix(true, leafPath), _updateValue);
                leaf.IsDirty = true;
                UpdateHashes(leaf);

                return _updateValue;
            }

            return TraverseNode(nextNode);
        }

        private byte[] TraverseLeaf(LeafNode node)
        {
            (byte[] shorterPath, byte[] longerPath) = RemainingUpdatePath.Length - node.Path.Length < 0
                ? (RemainingUpdatePath, node.Path)
                : (node.Path, RemainingUpdatePath);

            byte[] shorterPathValue;
            byte[] longerPathValue;

            if (Bytes.UnsafeCompare(shorterPath, node.Path))
            {
                shorterPathValue = node.Value;
                longerPathValue = _updateValue;
            }
            else
            {
                shorterPathValue = _updateValue;
                longerPathValue = node.Value;
            }

            int extensionLength = 0;
            for (int i = 0; i < Math.Min(shorterPath.Length, longerPath.Length) && shorterPath[i] == longerPath[i]; i++, extensionLength++)
            {
            }

            if (extensionLength == shorterPath.Length && extensionLength == longerPath.Length)
            {
                if (!_isUpdate)
                {
                    return node.Value;
                }

                if (_updateValue == null)
                {
                    UpdateHashes(null);
                    return _updateValue;
                }

                if (!Bytes.UnsafeCompare(node.Value, _updateValue))
                {
                    LeafNode newLeaf = new LeafNode(new HexPrefix(true, RemainingUpdatePath), _updateValue);
                    newLeaf.IsDirty = true;
                    UpdateHashes(newLeaf);
                    return _updateValue;
                }

                return _updateValue;
            }

            if (!_isUpdate)
            {
                return null;
            }

            if (_updateValue == null)
            {
                if (_ignoreMissingDelete)
                {
                    return null;
                }

                throw new InvalidOperationException($"Could not find the leaf node to delete: {Hex.FromBytes(_updatePath, false)}");
            }

            if (extensionLength != 0)
            {
                ExtensionNode extension = new ExtensionNode();
                extension.IsDirty = true;
                byte[] extensionPath = longerPath.Slice(0, extensionLength);
                extension.Key = new HexPrefix(false, extensionPath);
                _nodeStack.Push(new StackedNode(extension, 0));
            }

            BranchNode branch = new BranchNode();
            branch.IsDirty = true;

            if (extensionLength == shorterPath.Length)
            {
                branch.Value = shorterPathValue;
            }
            else
            {
                byte[] shortLeafPath = shorterPath.Slice(extensionLength + 1, shorterPath.Length - extensionLength - 1);
                LeafNode shortLeaf = new LeafNode(new HexPrefix(true, shortLeafPath), shorterPathValue);
                shortLeaf.IsDirty = true;
                branch.Nodes[shorterPath[extensionLength]] = shortLeaf;
            }

            byte[] leafPath = longerPath.Slice(extensionLength + 1, longerPath.Length - extensionLength - 1);
            LeafNode leaf = new LeafNode(new HexPrefix(true, leafPath), longerPathValue);
            leaf.IsDirty = true;
            _nodeStack.Push(new StackedNode(branch, longerPath[extensionLength]));
            UpdateHashes(leaf);

            return _updateValue;
        }

        private byte[] TraverseExtension(ExtensionNode node)
        {
            int extensionLength = 0;
            for (int i = 0; i < Math.Min(RemainingUpdatePath.Length, node.Path.Length) && RemainingUpdatePath[i] == node.Path[i]; i++, extensionLength++)
            {
            }

            if (extensionLength == node.Path.Length)
            {
                _currentIndex += extensionLength;
                _nodeStack.Push(new StackedNode(node, 0));
                Node nextNode = node.NextNode;
                return TraverseNode(nextNode);
            }

            if (!_isUpdate)
            {
                return null;
            }

            if (_updateValue == null)
            {
                if (_ignoreMissingDelete)
                {
                    return null;
                }

                throw new InvalidOperationException("Could find the leaf node to delete: {Hex.FromBytes(_updatePath, false)}");
            }

            if (extensionLength != 0)
            {
                ExtensionNode extension = new ExtensionNode();
                extension.IsDirty = true;
                byte[] extensionPath = node.Path.Slice(0, extensionLength);
                extension.Key = new HexPrefix(false, extensionPath);
                _nodeStack.Push(new StackedNode(extension, 0));
            }

            BranchNode branch = new BranchNode();
            branch.IsDirty = true;
            if (extensionLength == RemainingUpdatePath.Length)
            {
                branch.Value = _updateValue;
            }
            else
            {
                byte[] path = RemainingUpdatePath.Slice(extensionLength + 1, RemainingUpdatePath.Length - extensionLength - 1);
                LeafNode shortLeaf = new LeafNode(new HexPrefix(true, path), _updateValue);
                shortLeaf.IsDirty = true;
                branch.Nodes[RemainingUpdatePath[extensionLength]] = shortLeaf;
            }

            if (node.Path.Length - extensionLength > 1)
            {
                byte[] extensionPath = node.Path.Slice(extensionLength + 1, node.Path.Length - extensionLength - 1);
                ExtensionNode secondExtension = new ExtensionNode(new HexPrefix(false, extensionPath), node.NextNode);
                secondExtension.IsDirty = true;
                branch.Nodes[node.Path[extensionLength]] = secondExtension;
            }
            else
            {
                branch.Nodes[node.Path[extensionLength]] = node.NextNode;
            }

            UpdateHashes(branch);
            return _updateValue;
        }
    }
}