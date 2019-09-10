using System.Dynamic;

namespace netMIH
{
    /// <summary>
    /// Index query result.
    /// </summary>
    public class Result
    {
        /// <summary>
        /// Value of matching record
        /// </summary>
        public string Hash { get; set; }
        
        /// <summary>
        /// Hamming distance
        /// </summary>
        public int Distance { get; set; }
        
        /// <summary>
        /// Categories recorded against hash
        /// </summary>
        public string[] Categories { get; set; }
    }
}