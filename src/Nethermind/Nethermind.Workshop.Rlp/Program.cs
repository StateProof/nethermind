using System;
using Nethermind.Core;
using Nethermind.Core.Crypto;

namespace Nethermind.Workshop.Rlp
{
    class Program
    {
        static void Main(string[] args)
        {
            Address address = new Address(Keccak.Zero);
            Console.WriteLine(CoreRlp.Encode(address));
        }
    }
}