using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Linq;

using Xunit;
using Xunit.Abstractions;

namespace NReco.Text
{

	public class AhoCorasickDoubleArrayTrieTests : IDisposable {

		ITestOutputHelper output;
		TextWriter origConsoleOut;
		StringWriter consoleOut;

		public AhoCorasickDoubleArrayTrieTests(ITestOutputHelper output) {
			this.output = output;

			// catch Console.WriteLine if used for debug purposes
			origConsoleOut = Console.Out;
			consoleOut = new StringWriter();
			Console.SetOut(consoleOut);
		}
		
		public void Dispose() {
			var consoleOut = this.consoleOut;
			if (consoleOut != null) {
				this.consoleOut = null;
				output.WriteLine(consoleOut.ToString());
				Console.SetOut(origConsoleOut);
			}
		}

		private void WriteLine(string s, params object[] args) {
			if (output != null)
				output.WriteLine(s, args);
		}

		private AhoCorasickDoubleArrayTrie<string> buildASimpleAhoCorasickDoubleArrayTrie(params string[] keywords) {
			// Collect test data set
			var map = new Dictionary<String, String>();
			foreach (var key in keywords) {
				map[key] = key;
			}
			var acdat = new AhoCorasickDoubleArrayTrie<string>();
			acdat.Build(map);
			return acdat;
		}

		private void AssertSeqEqual(string[] expected, IEnumerable<string> actual) {
			int idx = 0;
			foreach (var s in actual) {
				Assert.True(idx < expected.Length);
				Assert.Equal(expected[idx], s);
				idx++;
			}
			Assert.Equal(expected.Length, idx);
		}

		private void validateASimpleAhoCorasickDoubleArrayTrie(AhoCorasickDoubleArrayTrie<String> acdat, string text, string[] expected) {
			// Test it
			acdat.ParseText(text, (hit) => {
				WriteLine("[{0}:{1}]={2}", hit.Begin, hit.End, hit.Value);
				Assert.Equal(text.Substring(hit.Begin, hit.Length), hit.Value);
			});
			// Or simply use
			var wordList = acdat.ParseText(text);
			AssertSeqEqual(expected, wordList.Select(h => h.Value));
		}

		[Fact]
		public void testBuildAndParseSimply() {
			var acdat = buildASimpleAhoCorasickDoubleArrayTrie("hers","his","she","he");
			validateASimpleAhoCorasickDoubleArrayTrie(acdat, "uhers", new[] { "he", "hers" });

			var acdat2 = buildASimpleAhoCorasickDoubleArrayTrie("he", "she", "his", "her");
			validateASimpleAhoCorasickDoubleArrayTrie(acdat2, "herhehis", new[] {"he", "her", "he", "his" });
			validateASimpleAhoCorasickDoubleArrayTrie(acdat2, "hisher", new[] { "his", "she", "he", "her" });

			Assert.Equal("she", acdat2["she"]);
		}

		[Fact]
		public void testSaveLoad() {
			var acdat = buildASimpleAhoCorasickDoubleArrayTrie("hers", "his", "she", "he");
			var memStream = new MemoryStream();
			acdat.Save(memStream, true);

			WriteLine($"4 keywords, saved {memStream.Length} bytes");

			var acdat2 = new AhoCorasickDoubleArrayTrie<string>();
			memStream.Position = 0;
			acdat2.Load(memStream);

			Assert.Equal(acdat.Count, acdat2.Count);
			Assert.Equal("his", acdat2["his"]);
			validateASimpleAhoCorasickDoubleArrayTrie(acdat2, "uhers", new[] { "he", "hers" });

			// large dictionary
			var dictionary = loadDictionary("dictionary.txt");
			var keywords = dictionary.Select(k => new KeyValuePair<string, string>(k, k));
			var acdat3 = new AhoCorasickDoubleArrayTrie<string>(keywords);
			var memStream2 = new MemoryStream();
			acdat3.Save(memStream2, false);
			WriteLine($"{dictionary.Count} keywords, saved {memStream2.Length} bytes (without values)");
		}

		[Fact]
		public void testBuildAndParseWithBigFile() {
			// Load test data from disk
			var dictionary = loadDictionary("dictionary.txt");
			var text = loadText("text.txt");
			// You can use any type of Map to hold data
			var map = new Dictionary<String, String>();
			foreach (var key in dictionary) { map[key] = key; }

			// Build an AhoCorasickDoubleArrayTrie
			var acdat = new AhoCorasickDoubleArrayTrie<String>();
			acdat.Build(map);
			// Test it
			acdat.ParseText(text, (hit)=> {
				Assert.Equal(text.Substring(hit.Begin, hit.Length), hit.Value);
			});
		}

		[Fact]
		public void testCancellation() {
			// Collect test data set
			var map = new Dictionary<String, String>() {
				{"foo", "foo" },
				{"bar", "bar" }
			};
			// Build an AhoCorasickDoubleArrayTrie
			AhoCorasickDoubleArrayTrie<String> acdat = new AhoCorasickDoubleArrayTrie<String>();
			acdat.Build(map);
			// count matches
			String haystack = "sfwtfoowercwbarqwrcq";
			int count = 0;
			int countCancel = 0;
			Func<AhoCorasickDoubleArrayTrie<string>.Hit, bool> cancellingMatcher = (hit) => { countCancel++; return false; };
			Func<AhoCorasickDoubleArrayTrie<string>.Hit, bool> countingMatcher = (hit) => { count++; return true; };
			acdat.ParseText(haystack, cancellingMatcher);
			acdat.ParseText(haystack, countingMatcher);

			Assert.Equal(1, countCancel);
			Assert.Equal(2, count);
		}


		private void runTest(String dictionaryPath, String textPath) {
			HashSet<String> dictionary = loadDictionary(dictionaryPath);
			String text = loadText(textPath);

			var ahoCorasickDoubleArrayTrie = new AhoCorasickDoubleArrayTrie<String>();
			var dictionaryMap = new Dictionary<String, String>();
			foreach (String word in dictionary) {
				dictionaryMap[word] = word;  // we use the same text as the property of a word
			}

			var swBuild = new Stopwatch();
			ahoCorasickDoubleArrayTrie.Build(dictionaryMap);
			swBuild.Stop();
			WriteLine("Automata build time: {0}ms.\n", swBuild.ElapsedMilliseconds);

			// Let's test the speed of the two Aho-Corasick automata
			WriteLine("Parsing document which contains {0} characters, with a dictionary of {1} words.\n", text.Length, dictionary.Count);
			var sw = new Stopwatch();
			sw.Start();
			int hitCount = 0;
			ahoCorasickDoubleArrayTrie.ParseText(text, (hit) => { hitCount++; });
			sw.Stop();
			Assert.True(hitCount > 0);
			WriteLine("{0}ms, speed {1:0.##} char/s", sw.ElapsedMilliseconds, text.Length / (sw.ElapsedMilliseconds / 1000.0));
		}

		[Fact]
		public void testBenchmark() {
			runTest("dictionary.txt", "text.txt");
		}


		private string loadText(String path) {
			var resPrefix = "NReco.Text.testdata.";
			var resStream = typeof(AhoCorasickDoubleArrayTrieTests).Assembly.GetManifestResourceStream(resPrefix + path);
			using (var rdr = new StreamReader(resStream)) {
				return rdr.ReadToEnd();
			}
		}

		private HashSet<string> loadDictionary(string path) {
			var resPrefix = "NReco.Text.testdata.";
			HashSet<String> dictionary = new HashSet<String>();

			var resStream = typeof(AhoCorasickDoubleArrayTrieTests).Assembly.GetManifestResourceStream(resPrefix + path);
			using (var rdr = new StreamReader(resStream)) {
				string line;
				while ((line = rdr.ReadLine()) != null) {
					dictionary.Add(line);
				}
			}
			return dictionary;
		}
	}
}
