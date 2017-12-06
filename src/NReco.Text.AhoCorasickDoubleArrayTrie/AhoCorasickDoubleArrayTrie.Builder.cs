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

	public partial class AhoCorasickDoubleArrayTrie<V> {

		// A builder to build the AhoCorasickDoubleArrayTrie
		private class Builder {

			// the root state of trie
			private State rootState = new State();

			// whether the position has been used
			private bool[] used;

			// the allocSize of the dynamic array
			private int allocSize;

			// a parameter controls the memory growth speed of the dynamic array
			private int progress;

			// the next position to check unused memory
			private int nextCheckPos;

			// the size of the key-pair sets
			private int keySize;

			AhoCorasickDoubleArrayTrie<V> trie;

			internal Builder(AhoCorasickDoubleArrayTrie<V> trie) {
				this.trie = trie;
			}

			public void build(IEnumerable<KeyValuePair<string, V>> input) {
				addAllKeyword(input);
				buildDoubleArrayTrie(trie.v.Length);
				used = null;
				constructFailureStates();
				rootState = null;
				loseWeight();
			}

			/// <summary>
			/// fetch siblings of a parent node
			/// </summary>
			/// <param name="parent">parent node</param>
			/// <param name="siblings">siblings parent node's child nodes, i . e . the siblings</param>
			/// <returns>the amount of the siblings</returns>
			private int fetch(State parent, List<KeyValuePair<int, State>> siblings) {
				if (parent.isAcceptable()) {
					State fakeNode = new State(-(parent.getDepth() + 1));
					fakeNode.addEmit(parent.getLargestValueId());
					siblings.Add(new KeyValuePair<int, State>(0, fakeNode));
				}
				foreach (var entry in parent.getSuccess()) {
					siblings.Add(new KeyValuePair<int, State>(entry.Key + 1, entry.Value));
				}
				return siblings.Count;
			}

			// add a keyword
			private void addKeyword(String keyword, int index) {
				State currentState = this.rootState;
				for (int i = 0; i < keyword.Length; i++) {
					char character = keyword[i];
					currentState = currentState.addState(character);
				}
				currentState.addEmit(index);
			}

			// add a collection of keywords
			private void addAllKeyword(IEnumerable<KeyValuePair<string, V>> keywordSet) {
				// if collection size is known, lets add it more efficiently
				if (keywordSet is ICollection<KeyValuePair<string,V>> keywordCollection) {
					addAllKeyword(keywordCollection);
					return;
				}
				var l = new List<int>();
				var v = new List<V>();
				int i = 0;
				foreach (var entry in keywordSet) {
					addKeyword(entry.Key, i);
					l.Add(entry.Key.Length);
					v.Add(entry.Value);
					i++;
				}
				trie.l = l.ToArray();
				trie.v = v.ToArray();
			}

			private void addAllKeyword(ICollection<KeyValuePair<string, V>> keywordSet) {
				trie.l = new int[keywordSet.Count];
				trie.v = new V[keywordSet.Count];
				int i = 0;
				foreach (var entry in keywordSet) {
					addKeyword(entry.Key, i);
					trie.l[i] = entry.Key.Length;
					trie.v[i] = entry.Value;
					i++;
				}
			}

			// construct failure table
			private void constructFailureStates() {
				trie.fail = new int[trie.size + 1];
				trie.fail[1] = trie.@base[0];
				trie.output = new int[trie.size + 1][];
				Queue<State> queue = new Queue<State>(); // in java version was: new LinkedBlockingDeque<State>();

				foreach (State depthOneState in this.rootState.getStates()) {
					depthOneState.SetFailure(this.rootState, trie.fail);
					queue.Enqueue(depthOneState);
					constructOutput(depthOneState);
				}

				while (queue.Count > 0) {
					State currentState = queue.Dequeue();

					foreach (var transition in currentState.getTransitions()) {
						State targetState = currentState.nextState(transition);
						queue.Enqueue(targetState);

						State traceFailureState = currentState.Failure;
						while (traceFailureState.nextState(transition) == null) {
							traceFailureState = traceFailureState.Failure;
						}
						State newFailureState = traceFailureState.nextState(transition);
						targetState.SetFailure(newFailureState, trie.fail);
						targetState.addEmit(newFailureState.emit());
						constructOutput(targetState);
					}
				}
			}

			// construct output table
			private void constructOutput(State targetState) {
				var emit = targetState.emit();
				if (emit == null || emit.Count == 0) return;
				int[] output = new int[emit.Count];
				int i = 0;
				foreach (var entry in emit) {
					output[i] = entry;
					++i;
				}
				trie.output[targetState.Index] = output;
			}

			private void buildDoubleArrayTrie(int keySize) {
				progress = 0;
				this.keySize = keySize;

				int totalKeysLen = 0;
				for (int i = 0; i < trie.l.Length; i++)
					totalKeysLen += trie.l[i];
				resize(65536 + totalKeysLen*2 + 1);  // originally was 65536*32  -- it seems too large in most cases

				trie.@base[0] = 1;
				nextCheckPos = 0;

				State root_node = this.rootState;

				var siblings = new List<KeyValuePair<int, State>>(root_node.getSuccess().Count);
				fetch(root_node, siblings);
				insert(siblings);
			}

			// allocate the memory of the dynamic array
			private int resize(int newSize) {
				int[] base2 = new int[newSize];
				int[] check2 = new int[newSize];
				bool[] used2 = new bool[newSize];
				if (allocSize > 0) {
					Array.Copy(trie.@base, 0, base2, 0, allocSize);
					Array.Copy(trie.check, 0, check2, 0, allocSize);
					Array.Copy(used, 0, used2, 0, allocSize);
				}

				trie.@base = base2;
				trie.check = check2;
				used = used2;

				return allocSize = newSize;
			}

			/// <summary>
			/// insert the siblings to double array trie
			/// </summary>
			/// <param name="siblings">siblings the siblings being inserted</param>
			/// <returns>the position to insert them</returns>
			private int insert(List<KeyValuePair<int, State>> siblings) {
				int begin = 0;
				int pos = Math.Max(siblings[0].Key + 1, nextCheckPos) - 1;
				int nonzero_num = 0;
				int first = 0;

				if (allocSize <= pos)
					resize(pos + 1);

				outer:
				while (true) {
					pos++;

					if (allocSize <= pos) {
						resize(pos + 1);
					}

					if (trie.check[pos] != 0) {
						nonzero_num++;
						continue;
					} else if (first == 0) {
						nextCheckPos = pos;
						first = 1;
					}

					begin = pos - siblings[0].Key;
					if (allocSize <= (begin + siblings[siblings.Count - 1].Key) ) {
						// progress can be zero
						double l = (1.05 > 1.0 * keySize / (progress + 1)) ? 1.05 : 1.0 * keySize / (progress + 1);

						resize((int)(allocSize * l));
					}

					if (used[begin])
						continue;

					for (int i = 1; i < siblings.Count; i++)
						if (trie.check[begin + siblings[i].Key] != 0)
							goto outer;

					break;
				}

				// -- Simple heuristics --
				// if the percentage of non-empty contents in check between the
				// index
				// 'next_check_pos' and 'check' is greater than some constant value
				// (e.g. 0.9),
				// new 'next_check_pos' index is written by 'check'.
				if (1.0 * nonzero_num / (pos - nextCheckPos + 1) >= 0.95)
					nextCheckPos = pos;
				used[begin] = true;

				trie.size = (trie.size > begin + siblings[siblings.Count - 1].Key + 1) ? trie.size : begin + siblings[siblings.Count - 1].Key + 1;

				foreach (var sibling in siblings) {
					trie.check[begin + sibling.Key] = begin;
				}

				foreach (var sibling in siblings) {
					List<KeyValuePair<int, State>> new_siblings = new List<KeyValuePair<int, State>>(sibling.Value.getSuccess().Count + 1);

					if (fetch(sibling.Value, new_siblings) == 0) {
						trie.@base[begin + sibling.Key] = (-sibling.Value.getLargestValueId() - 1);
						progress++;
					} else {
						int h = insert(new_siblings);   // dfs
						trie.@base[begin + sibling.Key] = h;
					}
					sibling.Value.Index = begin + sibling.Key;
				}
				return begin;
			}

			// free the unnecessary memory
			private void loseWeight() {
				//tbd: possible optimization for zero-value tail?..

				int[] nbase = new int[trie.size + 65535];
				Array.Copy(trie.@base, 0, nbase, 0, trie.size);
				trie.@base = nbase;

				int[] ncheck = new int[trie.size + 65535];
				Array.Copy(trie.check, 0, ncheck, 0, trie.size);
				trie.check = ncheck;
			}
		}


	}

}
