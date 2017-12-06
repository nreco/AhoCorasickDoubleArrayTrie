/*
 *  Copyright 2017 Vitalii Fedorchenko
 *
 *  This program is free software; you can redistribute it and/or modify
 *  it under the terms of the Apache License version 2.
 *
 *  This C# implementation is a port of hankcs's https://github.com/hankcs/AhoCorasickDoubleArrayTrie (java) 
 *  that licensed under the Apache 2.0 License (see http://www.apache.org/licenses/LICENSE-2.0).
 *
 *  Unless required by applicable law or agreed to in writing, software distributed on an
 *  "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace NReco.Text
{

	/// <summary>
	/// An implementation of Aho Corasick algorithm based on Double Array Trie.
	/// </summary>
	public partial class AhoCorasickDoubleArrayTrie<V>  {

		/// <summary>
		/// Check array of the Double Array Trie structure
		/// </summary>
		protected int[] check;

		/// <summary>
		/// Base array of the Double Array Trie structure
		/// </summary>
		protected int[] @base;

		/// <summary>
		/// Fail table of the Aho Corasick automata
		/// </summary>
		protected int[] fail;

		/// <summary>
		/// Output table of the Aho Corasick automata
		/// </summary>
		protected int[][] output;

		/// <summary>
		/// Outer value array
		/// </summary>
		protected V[] v;

		/// <summary>
		/// The length of every key.
		/// </summary>
		protected int[] l;

		/// <summary>
		/// The size of base and check array
		/// </summary>
		protected int size;

		public AhoCorasickDoubleArrayTrie() {

		}

		public AhoCorasickDoubleArrayTrie(IEnumerable<KeyValuePair<string,V>> keywords) {
			Build(keywords);
		}

		/// <summary>
		/// Parse text and match all substrings.
		/// </summary>
		/// <param name="text">The text</param>
		/// <returns>a list of matches</returns>
		public List<Hit> ParseText(string text) {
			var collectedEmits = new List<Hit>();
			ParseText(text, (hit) => {
				collectedEmits.Add(hit);
				return true;
			});
			return collectedEmits;
		}

		/// <summary>
		/// Parse text and match substrings (cancellable).
		/// </summary>
		/// <param name="text">The text.</param>
		/// <param name="processor">A processor which handles matches (returns 'continue' flag).</param>
		public void ParseText(string text, Func<Hit,bool> processor) {
			int position = 1;
			int currentState = 0;
			for (int chIdx = 0; chIdx < text.Length; ++chIdx) {
				currentState = getState(currentState, text[chIdx]);
				int[] hitArray = output[currentState];
				if (hitArray != null) {
					for (int i = 0; i < hitArray.Length; i++) {
						var hit = hitArray[i];
						// begin, end, value
						if (!processor(new Hit(position - l[hit], position, v[hit], hit)))
							return;
					}
				}
				++position;
			}
		}

		/// <summary>
		/// Parse text and match all substrings with a handler.
		/// </summary>
		/// <param name="text">The text.</param>
		/// <param name="processor">A processor which handles matches.</param>
		public void ParseText(String text, Action<Hit> processor) {
			ParseText(text, (hit) => { processor(hit); return true; });
		}

		/// <summary>
		/// Parse text represented as char array.
		/// </summary>
		/// <param name="text">The text</param>
		/// <param name="processor">A processor which handles matches (returns 'continue' flag).</param>
		public void ParseText(char[] text, Func<Hit, bool> processor) {
			int position = 1;
			int currentState = 0;
			for (int chIdx=0; chIdx < text.Length; chIdx++) {
				char c = text[chIdx];
				currentState = getState(currentState, c);
				int[] hitArray = output[currentState];
				if (hitArray != null) {
					for (int i=0; i<hitArray.Length; i++) {
						var hit = hitArray[i];
						if (!processor( new Hit( position - l[hit], position, v[hit], hit ) ))
							return;
					}
				}
				++position;
			}
		}

		/// <summary>
		/// Save automata state into binary stream.
		/// </summary>
		public void Save(Stream input, bool saveValues) {
			throw new NotImplementedException();
			/*out.writeObject(base);
			out.writeObject(check);
			out.writeObject(fail);
			out.writeObject(output);
			out.writeObject(l);
			out.writeObject(v);*/
		}

		/// <summary>
		/// Load automata state from specified binary stream.
		/// </summary>
		public void Load(Stream input, bool loadValues) {
			throw new NotImplementedException();
			/*base = (int[]) in.readObject();
			check = (int[]) in.readObject();
			fail = (int[]) in.readObject();
			output = (int[][]) in.readObject();
			l = (int[]) in.readObject();
			v = (V[]) in.readObject();*/
		}

		/// <summary>
		/// Gets the size of the keywords that could be matched by automata.
		/// </summary>
		public int Count {
			get {
				return v.Length;
			}
		}

		/// <summary>
		/// Gets value by a string key.
		/// </summary>
		/// <param name="key">The key (substring that can be matched by automata).</param>
		/// <returns>The value.</returns>
		public V this[string key] {
			get {
				int index = ExactMatchSearch(key);
				if (index >= 0) {
					return v[index];
				}
				return default(V);
			}
		}

		/// <summary>
		/// Pick the value by index in value array.
		/// </summary>
		/// <param name="index">The index.</param>
		/// <returns>The value.</returns>
		/// <remarks>Notice that to be more efficiently, this method DONOT check the parameter.</remarks>
		public V this[int index] {
			get {
				return v[index];
			}
		}

		// transmit state, supports failure function
		private int getState(int currentState, char character) {
			int newCurrentState = transitionWithRoot(currentState, character);
			while (newCurrentState == -1) {
				currentState = fail[currentState];
				newCurrentState = transitionWithRoot(currentState, character);
			}
			return newCurrentState;
		}


		/// <summary>
		/// transition of a state
		/// </summary>
		protected int transition(int current, char c) {
			int b = current;
			int p;

			p = b + c + 1;
			if (b == check[p])
				b = @base[p];
			else
				return -1;

			p = b;
			return p;
		}

		/// <summary>
		/// transition of a state, if the state is root and it failed, then returns the root
		/// </summary>
		protected int transitionWithRoot(int nodePos, char c) {
			int b = @base[nodePos];
			int p;

			p = b + c + 1;
			if (b != check[p]) {
				if (nodePos == 0) return 0;
				return -1;
			}

			return p;
		}

		/// <summary>
		/// Build a AhoCorasickDoubleArrayTrie from a sequence of string key -> value pairs.
		/// </summary>
		public void Build(IEnumerable<KeyValuePair<String, V>> map) {
			new Builder(this).build(map);
		}

		/// <summary>
		/// Match exactly by a key
		/// </summary>
		/// <param name="key">the key</param>
		/// <returns>the index of the key, you can use it as a perfect hash function.</returns>
		public int ExactMatchSearch(String key) {
			return exactMatchSearch(key, 0, 0, 0);
		}

		private int exactMatchSearch(string key, int pos, int len, int nodePos) {
			if (len <= 0)
				len = key.Length;
			if (nodePos <= 0)
				nodePos = 0;

			return exactMatchSearch(key.ToCharArray(), pos, len, nodePos);
		}

		private int exactMatchSearch(char[] keyChars, int pos, int len, int nodePos) {
			int result = -1;

			int b = @base[nodePos];
			int p;

			for (int i = pos; i < len; i++) {
				p = b + (int)(keyChars[i]) + 1;
				if (b == check[p])
					b = @base[p];
				else
					return result;
			}

			p = b;
			int n = @base[p];
			if (b == check[p] && n < 0) {
				result = -n - 1;
			}
			return result;
		}

	}



}
