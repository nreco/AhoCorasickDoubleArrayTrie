# NReco.Text.AhoCorasickDoubleArrayTrie
Very fast C# implementation of Aho Corasick algorithm based on Double Array Trie: efficient text search of many substrings with O(n) complexity.

[![NuGet Release](https://img.shields.io/nuget/v/NReco.Text.AhoCorasickDoubleArrayTrie.svg)](https://www.nuget.org/packages/NReco.Text.AhoCorasickDoubleArrayTrie/) | ![Tests](https://github.com/nreco/AhoCorasickDoubleArrayTrie/actions/workflows/dotnet-test.yml/badge.svg)


* very fast: can be used for efficient substring search of thousands keywords with O(n) complexity.
* trie represented with double array approach to minimize memory usage
* automata state can be effectively saved/loaded to binary stream (say, file)
* supports case-insensitive search

## How to use
```
// note: keywords may be provided as enumeration of KeyValuePair
var keywords = new Dictionary<string,int>() {
  {"are", 1},
  {"is", 1},
  {"he", 2},
  {"she", 2},
  {"it", 2},
  {"we", 2}
};
var matcher = new AhoCorasickDoubleArrayTrie<int>( keywords );
var text = "we are all champions";
matcher.ParseText(text, (hit) => {
	Console.WriteLine("Matched: {0} = {1}", text.Substring(hit.Begin, hit.Length), hit.Value );
});
```

## License
Licensed under the Apache License, Version 2.0 (see LICENSE file).

This C# implementation is a port of hankcs's https://github.com/hankcs/AhoCorasickDoubleArrayTrie (java) that were licensed under the Apache 2.0 License (see http://www.apache.org/licenses/LICENSE-2.0).
Copyright 2017-2022 Vitaliy Fedorchenko (port to C#, improvements) and contributors
