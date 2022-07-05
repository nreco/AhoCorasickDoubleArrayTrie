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

namespace NReco.Text {

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

			private readonly AhoCorasickDoubleArrayTrie<V> trie;

			internal Builder(AhoCorasickDoubleArrayTrie<V> trie) {
				this.trie = trie;
			}

			internal void Build(IEnumerable<KeyValuePair<string, V>> input) {
				AddAllKeyword(input);
				BuildDoubleArrayTrie(trie.v.Length);
				used = null;
				ConstructFailureStates();
				rootState = null;
				LoseWeight();
			}

			/// <summary>
			/// fetch siblings of a parent node
			/// </summary>
			/// <param name="parent">parent node</param>
			/// <param name="siblings">siblings parent node's child nodes, i . e . the siblings</param>
			/// <returns>the amount of the siblings</returns>
			private static int Fetch(State parent, IList<KeyValuePair<int, State>> siblings) {
				if (parent.IsAcceptable) {
					State fakeNode = new State(-(parent.Depth + 1));
					fakeNode.AddEmit(parent.LargestValueId);
					siblings.Add(new KeyValuePair<int, State>(0, fakeNode));
				}

				foreach (var entry in parent.Success) {
					siblings.Add(new KeyValuePair<int, State>(entry.Key + 1, entry.Value));
				}

				return siblings.Count;
			}

			// add a keyword
			private void AddKeyword(string keyword, int index) {
				State currentState = this.rootState;
				if (currentState == null) {
					throw new InvalidOperationException("Cannot add keyword after Build");
				}

				for (int i = 0; i < keyword.Length; i++) {
					char character = keyword[i];
					currentState = currentState.AddState(character);
				}

				currentState.AddEmit(index);
			}

			// add a collection of keywords
			private void AddAllKeyword(IEnumerable<KeyValuePair<string, V>> keywordSet) {
				// if collection size is known, let's add it more efficiently
				if (keywordSet is ICollection<KeyValuePair<string, V>> keywordCollection) {
					AddAllKeyword(keywordCollection);
					return;
				}

				var l = new List<int>();
				var v = new List<V>();
				int i = 0;
				foreach (var entry in keywordSet) {
					AddKeyword(entry.Key, i);
					l.Add(entry.Key.Length);
					v.Add(entry.Value);
					i++;
				}

				trie.l = l.ToArray();
				trie.v = v.ToArray();
			}

			private void AddAllKeyword(ICollection<KeyValuePair<string, V>> keywordSet) {
				trie.l = new int[keywordSet.Count];
				trie.v = new V[keywordSet.Count];
				int i = 0;
				foreach (var entry in keywordSet) {
					AddKeyword(entry.Key, i);
					trie.l[i] = entry.Key.Length;
					trie.v[i] = entry.Value;
					i++;
				}
			}

			// construct failure table
			private void ConstructFailureStates() {
				if (this.rootState == null) {
					throw new InvalidOperationException("Cannot ConstructFailureStates after Build");
				}

				trie.fail = new int[trie.size + 1];
				trie.output = new int[trie.size + 1][];
				var queue = new Queue<State>();

				foreach (State depthOneState in this.rootState.States) {
					depthOneState.SetFailure(this.rootState, trie.fail);
					queue.Enqueue(depthOneState);
					ConstructOutput(depthOneState);
				}

				while (queue.Count > 0) {
					State currentState = queue.Dequeue();

					foreach (var transition in currentState.Transitions) {
						State targetState = currentState.NextState(transition);
						queue.Enqueue(targetState);

						State traceFailureState = currentState.Failure;
						while (traceFailureState.NextState(transition) == null) {
							traceFailureState = traceFailureState.Failure;
						}

						State newFailureState = traceFailureState.NextState(transition);
						targetState.SetFailure(newFailureState, trie.fail);
						targetState.AddEmit(newFailureState.Emit);
						ConstructOutput(targetState);
					}
				}
			}

			// construct output table
			private void ConstructOutput(State targetState) {
				var emit = targetState.Emit;
				if (emit == null || emit.Count == 0)
					return;
				int[] output = new int[emit.Count];
				int i = 0;
				foreach (var entry in emit) {
					output[i] = entry;
					++i;
				}
				trie.output[targetState.Index] = output;
			}

			private void BuildDoubleArrayTrie(int keySize) {
				if (this.rootState == null) {
					throw new InvalidOperationException("Cannot BuildDoubleArrayTrie after Build");
				}

				this.progress = 0;
				this.keySize = keySize;

				this.Resize(65536 * 32);

				this.trie.@base[0] = 1;
				this.nextCheckPos = 0;

				State rootNode = this.rootState;

				var siblings = new List<KeyValuePair<int, State>>(rootNode.Success.Count);
				Fetch(rootNode, siblings);
				if (siblings.Count == 0) {
					for (int i = 0; i < this.trie.check.Length; i++) {
						this.trie.check[i] = -1;
					}
				} else {
					this.Insert(siblings);
				}
			}

			// allocate the memory of the dynamic array
			private int Resize(int newSize) {
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
			/// <param name="firstSiblings">the initial siblings being inserted</param>
			private void Insert(IList<KeyValuePair<int, State>> firstSiblings) {
				var siblingQueue = new Queue<KeyValuePair<int?, IList<KeyValuePair<int, State>>>>();
				siblingQueue.Enqueue(new KeyValuePair<int?, IList<KeyValuePair<int, State>>>(null, firstSiblings));

				while (siblingQueue.Count > 0) {
					this.Insert(siblingQueue);
				}
			}

			/// <summary>
			/// insert the siblings to double array trie
			/// </summary>
			/// <param name="siblingQueue">a queue holding all siblings being inserted and the position to insert them</param>
			private void Insert(Queue<KeyValuePair<int?, IList<KeyValuePair<int, State>>>> siblingQueue) {
				KeyValuePair<int?, IList<KeyValuePair<int, State>>> tCurrent = siblingQueue.Dequeue();
				IList<KeyValuePair<int, State>> siblings = tCurrent.Value;

				int begin = 0;
				int pos = Math.Max(siblings[0].Key + 1, nextCheckPos) - 1;
				int nonzeroNum = 0;
				int first = 0;

				if (allocSize <= pos) {
					Resize(pos + 1);
				}

				outer:
				// The goal of this loop body is to find n free space that satisfies base[begin + a1...an] == 0, a1...an is the n nodes in the siblings
				while (true) {
					pos++;

					if (allocSize <= pos) {
						Resize(pos + 1);
					}

					if (trie.check[pos] != 0) {
						nonzeroNum++;
						continue;
					} else if (first == 0) {
						nextCheckPos = pos;
						first = 1;
					}

					begin = pos - siblings[0].Key; // The distance of the current position from the first sibling node
					if (allocSize <= (begin + siblings[siblings.Count - 1].Key)) {
						// progress can be zero
						// Prevents progress from generating division-by-zero errors
						double toSize = Math.Max(1.05, 1.0 * keySize / (progress + 1)) * allocSize;
						const int maxSize = (int)(int.MaxValue * 0.95);
						if (allocSize >= maxSize) {
							throw new NotSupportedException("Double array trie is too big.");
						} else {
							Resize((int)Math.Min(toSize, maxSize));
						}
					}

					if (used[begin]) {
						continue;
					}

					for (int i = 1; i < siblings.Count; i++) {
						if (trie.check[begin + siblings[i].Key] != 0) {
							goto outer;
						}
					}

					break;
				}

				// -- Simple heuristics --
				// if the percentage of non-empty contents in check between the
				// index
				// 'next_check_pos' and 'check' is greater than some constant value
				// (e.g. 0.9),
				// new 'next_check_pos' index is written by 'check'.
				if (1.0 * nonzeroNum / (pos - nextCheckPos + 1) >= 0.95) {
					nextCheckPos = pos; // Starting from the location next_check_pos to pos, if the space occupied is more than 95%, the next time you insert the node, start the lookup directly from the pos location
				}

				used[begin] = true;

				trie.size = (trie.size > begin + siblings[siblings.Count - 1].Key + 1) ? trie.size : begin + siblings[siblings.Count - 1].Key + 1;

				foreach (var sibling in siblings) {
					trie.check[begin + sibling.Key] = begin;
				}

				foreach (var sibling in siblings) {
					IList<KeyValuePair<int, State>> newSiblings = new List<KeyValuePair<int, State>>(sibling.Value.Success.Count + 1);

					if (Fetch(sibling.Value, newSiblings) == 0)  // The termination of a word that is not a prefix for other words is actually a leaf node
					{
						trie.@base[begin + sibling.Key] = (-sibling.Value.LargestValueId - 1);
						progress++;
					} else {
						siblingQueue.Enqueue(new KeyValuePair<int?, IList<KeyValuePair<int, State>>>(begin + sibling.Key, newSiblings));
					}

					sibling.Value.Index = begin + sibling.Key;
				}

				// Insert siblings
				int? parentBaseIndex = tCurrent.Key;
				if (parentBaseIndex != null) {
					this.trie.@base[parentBaseIndex.Value] = begin;
				}
			}

			// free the unnecessary memory
			private void LoseWeight() {
				//tbd: possible optimization for zero-value tail?..

				int[] nbase = new int[trie.size + 65535];
				Array.Copy(trie.@base, 0, nbase, 0, trie.size);
				trie.@base = nbase;

				int[] ncheck = new int[trie.size + 65535];
				Array.Copy(trie.check, 0, ncheck, 0, Math.Min(trie.check.Length, ncheck.Length));
				trie.check = ncheck;
			}
		}
	}
}
