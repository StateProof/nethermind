using System;
using Nethermind.Core;
using Nethermind.Core.Crypto;

namespace Rlp
{
    class Program
    {
        static void Main(string[] args)
        {
            Address address = new Address(Keccak.Zero);
            Console.WriteLine(Rlp.Encode(address));
            
        }
    }
}