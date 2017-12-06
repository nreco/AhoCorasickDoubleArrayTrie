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

		internal sealed class State {

			protected readonly int depth;

			private State _failure = null;

			private ISet<int> emits = null;

			private Dictionary<char, State> success = new Dictionary<char, State>();

			private int index;

			internal State() : this(0) {
			}

			internal State(int depth) {
				this.depth = depth;
			}

			public int getDepth() {
				return this.depth;
			}

			int largestValueIdx = Int32.MinValue;

			public void addEmit(int keyword) {
				if (this.emits == null) {
					this.emits = new HashSet<int>();
				}
				if (keyword > largestValueIdx)
					largestValueIdx = keyword;
				this.emits.Add(keyword);
			}

			public int getLargestValueId() {
				if (emits == null || emits.Count == 0) return Int32.MinValue; //?? null
				return largestValueIdx;
			}

			public void addEmit(IEnumerable<int> emits) {
				foreach (int emit in emits) {
					addEmit(emit);
				}
			}

			static int[] emptyIntArray = new int[0];

			public ICollection<int> emit() {
				return this.emits == null ? (ICollection<int>)emptyIntArray : (ICollection<int>)this.emits;
			}

			public bool isAcceptable() {
				return this.depth > 0 && this.emits != null;
			}

			public State Failure {
				get { return _failure; }
			}

			public void SetFailure(State failState, int[] fail) {
				this._failure = failState;
				fail[index] = failState.index;
			}

			private State nextState(char character, bool ignoreRootState) {
				State nextState = null;
				this.success.TryGetValue(character, out nextState);
				if (!ignoreRootState && nextState == null && this.depth == 0) {
					nextState = this;
				}
				return nextState;
			}

			public State nextState(char character) {
				return nextState(character, false);
			}

			public State nextStateIgnoreRootState(char character) {
				return nextState(character, true);
			}

			public State addState(char character) {
				State nextState = nextStateIgnoreRootState(character);
				if (nextState == null) {
					nextState = new State(this.depth + 1);
					this.success[character] = nextState;
				}
				return nextState;
			}

			public IEnumerable<State> getStates() {
				return this.success.Values;
			}

			public IEnumerable<char> getTransitions() {
				return this.success.Keys;
			}

			public override string ToString() {
				StringBuilder sb = new StringBuilder("State{");
				sb.Append("depth=").Append(depth);
				sb.Append(", ID=").Append(index);
				sb.Append(", emits=").Append(emits);
				sb.Append(", success=").Append(success.Keys);  //??
				sb.Append(", failureID=").Append(_failure == null ? -1 : _failure.index);
				sb.Append(", failure=").Append(_failure);
				sb.Append('}');
				return sb.ToString();
			}

			public Dictionary<char, State> getSuccess() {
				return success;
			}

			public int Index {
				get { return index; }
				set { index = value;} 
			}

		}


	}

}
