namespace CPTest
{
    using System;

    using CommandLineParser;

    /// <summary>
    /// The program.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// The main.
        /// </summary>
        /// <param name="args">
        /// The args.
        /// </param>
        public static void Main(string[] args)
        {
            var result = args.Parse<Tester>();
            if (!result.IsValidInstance())
            {

            }
            Console.WriteLine(result.Time.ToSimpleString(CommandLineParser.TimeSpanTypes.Days));
        }

        /// <summary>
        /// The tester.
        /// </summary>
        public class Tester
        {
            /// <summary>
            /// Gets or sets the iterations.
            /// </summary>
            [Required]
            public int Iterations { get; set; }

            /// <summary>
            /// Gets or sets the output.
            /// </summary>
            [Required]
            public string Output { get; set; }

            /// <summary>
            /// Gets or sets the inputs.
            /// </summary>
            [Required]
            public string Inputs { get; set; }

            /// <summary>
            /// Gets or sets the time.
            /// </summary>
            public TimeSpan Time { get; set; }

            /// <summary>
            /// Gets or sets the script.
            /// </summary>
            public string[] Script { get; set; }
        }
    }
}
