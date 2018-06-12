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

namespace Nethermind.Store
{
    internal static class TreeNodeFactory
    {
        public static Node CreateBranch()
        {
            return new Node(NodeType.Branch);
        }

        public static Node CreateLeaf(HexPrefix key, byte[] value)
        {
            Node node = new Node(NodeType.Leaf);
            node.Key = key;
            node.Value = value;
            return node;
        }

        public static Node CreateExtension(HexPrefix key)
        {
            Node node = new Node(NodeType.Extension);
            node.Key = key;
            return node;
        }

        public static Node CreateExtension(HexPrefix key, Node child)
        {
            Node node = new Node(NodeType.Extension);
            node.Children[0] = child;
            node.Key = key;
            return node;
        }
    }
}