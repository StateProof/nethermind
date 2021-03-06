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

using Nethermind.Core.Encoding;
using NUnit.Framework;
using System.Collections.Generic;

namespace Nethermind.Core.Test
{
    [TestFixture]
    public class RlpTests
    {
        [Test]
        public void Serialized_form_is_same_as_input_when_input_length_is_1_and_value_is_less_than_128()
        {
            Assert.AreEqual(0, Rlp.Encode(new byte[] { 0 })[0], "0");
            Assert.AreEqual(127, Rlp.Encode(new byte[] { 127 })[0], "127");
            Assert.AreEqual(128, Rlp.Encode(new byte[] { })[0], "128");
            Assert.AreEqual(1, Rlp.Encode(new byte[] { 1 })[0], "1");
        }

        [Test]
        public void Serialized_form_is_128_when_input_is_empty()
        {
            Assert.AreEqual(128, Rlp.Encode(new byte[] { })[0]);
        }

        [Test]
        public void
            Serialized_form_is_input_prefixed_with_128_plus_length_of_input_when_input_length_is_between_1_and_56()
        {
            byte[] input = new byte[55];
            input[0] = 255;
            input[1] = 128;
            input[2] = 1;

            Assert.AreEqual(183, Rlp.Encode(input)[0]);
            Assert.AreEqual(56, Rlp.Encode(input).Length);
            for (int i = 0; i < 55; i++)
            {
                Assert.AreEqual(input[i], Rlp.Encode(input)[i + 1]);
            }
        }

        [TestCase(128L, (byte)(1 + 183), (byte)128)]
        [TestCase(256L, (byte)(1 + 1 + 183), (byte)1)]
        [TestCase(256L * 256L, (byte)(1 + 2 + 183), (byte)1)]
        [TestCase(256L * 256L * 256L, (byte)(1 + 3 + 183), (byte)1)]
        //[TestCase(256L * 256L * 256L * 256L, (byte)(1 + 4 + 183), (byte)1)]
        public void Serialized_form_is_input_prefixed_with_big_endian_length_and_prefixed_with_length_of_it_plus_183(
            long inputLength, byte expectedFirstByte, byte expectedSecondByte)
        {
            byte[] input = new byte[inputLength];
            input[0] = 255;
            input[1] = 128;
            input[2] = 1;

            Assert.AreEqual(expectedFirstByte, Rlp.Encode(input)[0]);
            Assert.AreEqual(expectedSecondByte, Rlp.Encode(input)[1]);

            if (inputLength < 256)
            {
                for (int i = 0; i < 128; i++)
                {
                    Assert.AreEqual(input[i], Rlp.Encode(input)[i + 1 + expectedFirstByte - 183]);
                }
            }
        }

        [Test]
        public void Serializing_sequences()
        {
            Rlp output = Rlp.Encode(
                Rlp.Encode(255L),
                Rlp.Encode(new byte[] { 255 }));
            Assert.AreEqual(new byte[] { 196, 129, 255, 129, 255 }, output.Bytes);
        }

        [Test]
        public void Serializing_empty_sequence()
        {
            Rlp output = Rlp.Encode(new Rlp[] { });
            Assert.AreEqual(new byte[] { 192 }, output.Bytes);
        }

        [Test]
        public void Serializing_sequence_with_one_int_regression()
        {
            Rlp output = Rlp.Encode(new[] { Rlp.Encode(1) });
            Assert.AreEqual(new byte[] { 193, 1 }, output.Bytes);
        }

        [Test]
        public void Serializing_object_int_regression()
        {
            Rlp output = Rlp.Encode(new Rlp[] { Rlp.Encode(1) });
            Assert.AreEqual(new byte[] { 1 }, output.Bytes);
        }
    }
}