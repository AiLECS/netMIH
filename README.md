# netMIH
A .NET implementation of multiple index hashing by [Norouzi et al](https://www.cs.toronto.edu/~norouzi/research/papers/multi_index_hashing.pdf), based on a description in the [threatexchange repository](https://github.com/facebook/ThreatExchange/blob/master/hashing/hashing.pdf)

***

### Description
Multiple Index Hashing (MIH) is a relatively lightweight means for accelerating lookups for fuzzy hashes (e.g. PhotoDNA and [PDQ](https://github.com/facebook/ThreatExchange/tree/master/hashing/pdq)) within a pre-defined [hamming distance](https://math.ryerson.ca/~danziger/professor/MTH108/Handouts/codes.pdf).
Instead of a linear search through every record, multiple indices are made for separate windows/slots *within* each hash.
The threatexchange document (linked above) provides a good, 'plain English' description for those who (like me) struggle with mathematical terminology and notation.

***
### Installation
TODO: Pending upload/addition to nuget

***
### Usage
The constructor can be used for custom implementations of match thresholds, window sizes etc, but otherwise, for PDQ, 
Default values for MIHIndex constructor and train() method are set for PDQ hash, using 32 bit match threshold (30 being non divisible by 8).

```c#
using netMIH

// Uses pre-configured values for PDQ hash
var index = new Index(Index.Configuration.PDQ);
var hashes = new string[] {"358c86641a5269ab5b0db5f1b2315c1642cef9652c39b6ced9f646d91f071927"};

//add entries to the index
index.Update(hashes, "ignorable");

// TRAIN the index - YOU NEED TO DO THIS BEFORE QUERYING!
index.train()

//query index for identical hashes
var results = index.Query("fc4d8e2130177f8f6ce2a03bd27fa8e6b1067a1ac8f0068037215df6491eee1f", 0);

//query index for entries with hamming distance of 1 or less
var results = index.Query("fc4d8e2130177f8f6ce2a03bd27fa8e6b1067a1ac8f0068037215df6491eee1f", 1);

// query index for entries with hamming distance of 34
// this is bigger than default match threshold of 32 for PDQ, so will utilise linear lookup. 
// This will be slower than MIH and performance will get worse the bigger the dataset gets 
var results = index.Query("fc4d8e2130177f8f6ce2a03bd27fa8e6b1067a1ac8f0068037215df6491eee1f", 34);
```

```cs




# For alternate hash sizes, enter bit size at declaration
x = MIHIndex(512)

# Example - load hashes from file.
# Update function only accepts hex strings (4 bits per char)
PDQs = set()
with open('ignorable.PDQ') as fi:
    for line in fi:
        PDQs.add(line.replace('\n', ''))

# Add to index - hashes plus category name (non-string types should work, but aren't recommended)
x.update(PDQs, 'ignorable')

# Train index. Word length is internal slot/word size for individual indices (see afore mentioned documentation for more info)
# Threshold is maximum hamming distance (inclusive). Defaults to values shown here.
x.train(wordLength=16,threshold=32)

# To query, pass in a hex string
for y in x.query('358c86641a5269ab5b0db5f1b2315c1642cef9652c39b6ced9f646d91f071927'):
    # hits are returned as a tuple of hash value and hamming distance. 
    print(y)
# ('358c86641a5269ab5b0db5f1b2315c1642cef9652c39b6ced9f646d91f071927', ['ignorable'], 0)
# ('358c86641a5269ab5b0db5f1b2315c1642cef9652c39b6ced9f646d91f071928', ['ignorable'], 4)

``` 

***
 ### Licensing
This is released under an MIT licence.  
***
