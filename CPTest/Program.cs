using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using CommandLineParser;

namespace CPTest
{
    class Program
    {
        public static void Main(string[] args)
        {
            var result = args.Parse<Tester>(1);
        }

        public class Tester
        {
            public int interations { get; set; }

            public string Output { get; set; }

            public string inputes { get; set; }
        }
    }
}
