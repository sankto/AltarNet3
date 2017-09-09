using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AltarNet {
	/// <summary>
	/// Represent an helper class for thread safety. This one is static and only deal with string keys.
	/// </summary>
	public static class ThreadSafeHelper {
		#region Statics

		private static readonly Dictionary<string, SemaphoreSlim> StaticMuts;
		private static readonly Dictionary<string, short> StaticMutsRefs;
		private static readonly SemaphoreSlim StaticSema;

		static ThreadSafeHelper() {
			StaticMuts = new Dictionary<string, SemaphoreSlim>();
			StaticMutsRefs = new Dictionary<string, short>();
			StaticSema = new SemaphoreSlim(1);
		}

		/// <summary>
		/// This will wait until the ressource labelled as 'key' is freed, then lock on it.
		/// </summary>
		/// <param name="key">The key to wait on</param>
		public static void Wait(string key) {
			SemaphoreSlim mut;
			StaticSema.Wait();
			try {
				if (StaticMuts.ContainsKey(key) == false) {
					StaticMutsRefs.Add(key, 0);
					StaticMuts.Add(key, new SemaphoreSlim(1));
				}
				mut = StaticMuts[key];
				StaticMutsRefs[key]++;
			} finally {
				StaticSema.Release();
			}
			mut.Wait();
		}

		/// <summary>
		/// This will wait until the ressource labelled as 'key' is freed, then lock on it.
		/// </summary>
		/// <param name="key">The key to wait on</param>
		/// <returns>A Task</returns>
		public static async Task WaitAsync(string key) {
			SemaphoreSlim mut;
			await StaticSema.WaitAsync();
			try {
				if (StaticMuts.ContainsKey(key) == false) {
					StaticMutsRefs.Add(key, 0);
					StaticMuts.Add(key, new SemaphoreSlim(1));
				}
				mut = StaticMuts[key];
				StaticMutsRefs[key]++;
			} finally {
				StaticSema.Release();
			}
			await mut.WaitAsync();
		}

		/// <summary>
		/// This will release the ressource labelled as 'key'.
		/// </summary>
		/// <param name="key">The key to release</param>
		public static void Release(string key) {
			StaticSema.Wait();
			try {
				StaticMuts[key].Release();
				if (--StaticMutsRefs[key] == 0) {
					StaticMuts[key].Dispose();
					StaticMuts.Remove(key);
					StaticMutsRefs.Remove(key);
				}
			} catch {} finally {
				StaticSema.Release();
			}
		}

		#endregion
	}

	/// <summary>
	/// Represent an helper class for thread safety. It accept equatable keys, such as int, byte, string, etc.
	/// </summary>
	public class ThreadSafeHelper<T> where T : IEquatable<T> {
		#region Instance

		private readonly Dictionary<T, SemaphoreSlim> InstMuts;
		private readonly Dictionary<T, short> InstMutsRefs;
		private readonly SemaphoreSlim InstSema;

		/// <summary>
		/// Create a ThreadSafeHelper/
		/// </summary>
		public ThreadSafeHelper() {
			InstMuts = new Dictionary<T, SemaphoreSlim>();
			InstMutsRefs = new Dictionary<T, short>();
			InstSema = new SemaphoreSlim(1);
		}

		/// <summary>
		/// This will wait until the ressource labelled as 'key' is freed, then lock on it.
		/// </summary>
		/// <param name="key">The key to wait on</param>
		public void Wait(T key) {
			SemaphoreSlim mut;
			InstSema.Wait();
			try {
				if (InstMuts.ContainsKey(key) == false) {
					InstMutsRefs.Add(key, 0);
					InstMuts.Add(key, new SemaphoreSlim(1));
				}
				mut = InstMuts[key];
				InstMutsRefs[key]++;
			} finally {
				InstSema.Release();
			}
			mut.Wait();
		}

		/// <summary>
		/// This will wait until the ressource labelled as 'key' is freed, then lock on it.
		/// </summary>
		/// <param name="key">The key to wait on</param>
		/// <returns>A Task</returns>
		public async Task WaitAsync(T key) {
			SemaphoreSlim mut;
			await InstSema.WaitAsync();
			try {
				if (InstMuts.ContainsKey(key) == false) {
					InstMutsRefs.Add(key, 0);
					InstMuts.Add(key, new SemaphoreSlim(1));
				}
				mut = InstMuts[key];
				InstMutsRefs[key]++;
			} finally {
				InstSema.Release();
			}
			await mut.WaitAsync();
		}

		/// <summary>
		/// This will release the ressource labelled as 'key'.
		/// </summary>
		/// <param name="key">The key to release</param>
		public void Release(T key) {
			InstSema.Wait();
			try {
				InstMuts[key].Release();
				if (--InstMutsRefs[key] == 0) {
					InstMuts[key].Dispose();
					InstMuts.Remove(key);
					InstMutsRefs.Remove(key);
				}
			} catch {} finally {
				InstSema.Release();
			}
		}

		#endregion
	}
}
