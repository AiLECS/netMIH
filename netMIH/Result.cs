using System.Dynamic;

namespace netMIH
{
    /// <summary>
    /// Index query result.
    /// </summary>
    /// <remarks>Use of category could theoretically be replaced by a unique identifier for candidate file (ie pointer to a dt</remarks>
    public class Result
    {
        /// <summary>
        /// Value of matching record
        /// </summary>
        /// <remarks>Example provided is for PDQ hash (64 character hex string)</remarks>
        /// <example>358c86641a5269ab5b0db5f1b2315c1642cef9652c39b6ced9f646d91f071927</example>
        public string Hash { get; set; }
        
        /// <summary>
        /// Hamming (i.e. edit) distance between subject hash and returned (this) hash. 
        /// </summary>
        /// <remarks>Can be used by client for sorting</remarks>
        /// <example>2</example>
        public int Distance { get; set; }
        
        /// <summary>
        /// Categories recorded against hash
        /// </summary>
        /// <example>["Ignorable"]</example>
        public string[] Categories { get; set; }
    }
}