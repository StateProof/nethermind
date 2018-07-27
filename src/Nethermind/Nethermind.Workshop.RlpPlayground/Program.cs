using System;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Encoding;

namespace Nethermind.Workshop.RlpPlayground
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            byte[] byteArray = new byte[2];
            Console.WriteLine(Rlp.Encode(byteArray).ToString(true));
            Console.WriteLine(Rlp.Encode(0).ToString(true));
            Console.WriteLine(Rlp.Encode(new byte[]{}).ToString(true));
            Console.WriteLine(Rlp.Encode(new Address(Keccak.Zero)).ToString(true));
            Console.WriteLine(Rlp.Encode(Keccak.OfAnEmptyString).ToString(true));
            Console.WriteLine(Rlp.Encode(Bloom.Empty).ToString(true));
            Console.WriteLine(Rlp.Encode(new Rlp[] {}).ToString(true));
            Console.WriteLine(Rlp.Encode(new Keccak[2] {Keccak.Zero, Keccak.Zero}).ToString(true));
            Console.ReadKey();
        }
    }
}